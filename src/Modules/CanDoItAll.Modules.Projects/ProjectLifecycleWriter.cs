using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectLifecycleWriter(IDbContextFactory<ProjectsDbContext> factory,
    ProjectLifecycleOperationProtocol protocol, ProjectDeletionCompletionOwner completionOwner,
    CoordinatedDatabaseTransaction transactions, ProjectWriteAdmissionService admissions,
    IEnumerable<IProjectDeletionParticipant> participants, SearchIndexService search, StorageCatalogService storage,
    IClock clock, IActivityStream activity, ProjectDeletionCompletionDispatcher completionDispatcher, ILogger<ProjectLifecycleWriter> logger) {
    private readonly ProjectLifecycleEffects effects = new(participants, transactions, search, storage, clock);

    public async Task<ProjectLifecycleOutcome> SubmitAsync(ProjectLifecycleSubmission submission,
        ProjectLifecycleInvocation invocation, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(invocation);
        ProjectLifecycleAttempt attempt;
        try {
            attempt = await protocol.PrepareAsync(submission, invocation, cancellationToken);
            if (await protocol.TryReadAcceptedAsync(attempt, cancellationToken)) {
                return new ProjectLifecycleOutcome.Accepted(attempt.Accepted!, true);
            }
        } catch (ProjectLifecycleRefusedException refused) {
            return new ProjectLifecycleOutcome.Refused(submission, refused.Reason);
        } catch (ProjectLifecycleObservationUnavailableException unavailable) {
            return new ProjectLifecycleOutcome.Unconfirmed(submission, unavailable.Reason);
        } catch (Exception observationFailure) when (observationFailure is System.Data.Common.DbException or IOException or InvalidOperationException) {
            return new ProjectLifecycleOutcome.Unconfirmed(submission, ProjectLifecycleRecoveryReason.ReadUnavailable,
                !TryLog(submission, observationFailure, false));
        }
        return await ExecuteAsync(attempt, cancellationToken);
    }

    private async Task<ProjectLifecycleOutcome> ExecuteAsync(ProjectLifecycleAttempt attempt, CancellationToken cancellationToken) {
        using var mutation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var mutationDeadline = protocol.MutationDeadline;
        mutation.CancelAfter(mutationDeadline);
        var mutationStarted = System.Diagnostics.Stopwatch.StartNew();
        ProjectsDbContext? context = null;
        ProjectSourceMutationScope? scope = null;
        ProjectDeletionCompletionOwner.Attempt? deletion = null;
        ProjectDeletionCompletionOwner.CloseResult? closedDeletion = null;
        Exception? failure = null;
        var ownerReleased = false;
        try {
            var selectedParticipants = attempt.Submission.Command is ProjectLifecycleCommand.Delete ? effects.CaptureParticipants() : [];
            var keys = selectedParticipants.SelectMany(participant => participant.PreparationScopeKeys.Select(key => key.Value))
                .Append(ProjectMutationScopeKeys.Hierarchy).ToArray();
            context = await factory.CreateDbContextAsync(mutation.Token);
            CoordinatedDatabaseTransaction.ForProfile(attempt.Profile).RequireOwnerProfile(context);
            protocol.RequireOriginal(attempt);
            scope = await ProjectSourceMutationScope.BeginLifecycleAsync(context, keys, attempt, protocol,
                admissions, transactions, mutation.Token);
            if (!attempt.ReplayObserved) {
                var epochDeadline = TimeSpan.FromMilliseconds(attempt.Epoch!.MutationDeadlineMilliseconds);
                var remaining = (epochDeadline < mutationDeadline ? epochDeadline : mutationDeadline) - mutationStarted.Elapsed;
                if (remaining <= TimeSpan.Zero) {
                    mutation.Cancel();
                } else {
                    mutation.CancelAfter(remaining);
                }
                mutation.Token.ThrowIfCancellationRequested();
                await ProjectLifecycleOperationProtocol.RequireCurrentWriteAsync(attempt, mutation.Token);
                ProjectLifecycleAcknowledgement acknowledgement;
                ProjectDeletionRecoveryEvidence? evidence = null;
                if (attempt.Submission.Command is ProjectLifecycleCommand.Delete command) {
                    var project = await ProjectLifecycleEffects.RequireDeletionTargetAsync(context, command.Expected, mutation.Token);
                    var original = new ProjectDeletionPreparationContext(new(attempt.Profile.Profile.Id,
                        attempt.Binding.Namespace, new(Guid.NewGuid())), command.Expected.Project);
                    evidence = new(original, new ProjectDeletionOriginEvidence.Ordinary(attempt.Submission,
                            new(attempt.Invocation.Caller.Kind, attempt.Invocation.Caller.Key)), scope.DeletionSource,
                        scope.RequiredProjects.Append(original.Project).Distinct().ToArray(),
                        selectedParticipants.Select(participant => new ProjectDeletionPreparationRegistration(
                            participant.Id, participant.RecoveryProtocolVersion)).ToArray());
                    deletion = completionOwner.Capture(context, scope, attempt.Profile, attempt.Generation, attempt.Binding, evidence);
                    var completion = await effects.PrepareDeletionAsync(context, original, selectedParticipants, mutation.Token);
                    deletion.StageParticipants(completion);
                    await effects.DeleteAsync(context, project, mutation.Token);
                    acknowledgement = new ProjectLifecycleAcknowledgement.DeletionCommitted(completion);
                } else {
                    acknowledgement = await effects.ApplyHierarchyAsync(context, attempt.Submission.Command, mutation.Token);
                }
                await protocol.StageAcceptedAsync(context, attempt, acknowledgement, mutation.Token);
                if (evidence is not null) {
                    await scope.CommitDeletionAsync(evidence, token => protocol.RequireCommitAsync(context, attempt, token), mutation.Token);
                } else {
                    await scope.CommitLifecycleAsync(token => protocol.RequireCommitAsync(context, attempt, token), mutation.Token);
                }
            }
        } catch (Exception operationFailure) {
            failure = operationFailure;
        }
        if (deletion is not null) {
            closedDeletion = await deletion.CloseAsync(CancellationToken.None);
            ownerReleased = closedDeletion.OriginalOwnerReleased;
            if (closedDeletion.Failure is { } closeFailure) {
                failure = Combine(failure, closeFailure);
            }
        } else {
            ownerReleased = scope is not null || context is null || attempt.AdmissionOwnerReleased;
            if (scope is not null) {
                try {
                    await scope.DisposeAsync();
                } catch (Exception closeFailure) {
                    ownerReleased = false;
                    failure = Combine(failure, closeFailure);
                }
            }
            if (context is not null) {
                try {
                    await context.DisposeAsync();
                } catch (Exception closeFailure) {
                    ownerReleased = false;
                    failure = Combine(failure, closeFailure);
                }
            }
        }
        if (scope?.IsCommitted == true) {
            if (ownerReleased && closedDeletion is not { ReleaseAcknowledged: false }) {
                var postCommitFailure = await CompleteKnownAsync(attempt, cancellationToken);
                if (postCommitFailure is not null) {
                    failure = Combine(failure, postCommitFailure);
                }
            }
            var issue = !ownerReleased ? ProjectLifecycleCompletionIssue.OwnerReleaseUnconfirmed
                : closedDeletion is { ReleaseAcknowledged: false } ? ProjectLifecycleCompletionIssue.ReleaseRecordUnconfirmed
                : failure is not null ? ProjectLifecycleCompletionIssue.PostCommitProcessing : (ProjectLifecycleCompletionIssue?)null;
            var diagnosticsUnavailable = failure is not null && !TryLog(attempt, failure, true);
            if (diagnosticsUnavailable) {
                issue ??= ProjectLifecycleCompletionIssue.DiagnosticsUnavailable;
            }
            return new ProjectLifecycleOutcome.Accepted(attempt.Accepted
                ?? throw new InvalidOperationException("An acknowledged lifecycle commit has no staged accepted receipt."), false, issue, diagnosticsUnavailable);
        }
        if (!ownerReleased) {
            return new ProjectLifecycleOutcome.Unconfirmed(attempt.Submission, ProjectLifecycleRecoveryReason.OwnerReleaseUnconfirmed,
                failure is not null && !TryLog(attempt, failure, false));
        }
        using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try {
            if (await protocol.TryReadAcceptedAsync(attempt, recovery.Token)) {
                var diagnosticsUnavailable = failure is not null && !TryLog(attempt, failure, true);
                return new ProjectLifecycleOutcome.Accepted(attempt.Accepted!, true,
                    closedDeletion is { ReleaseAcknowledged: false } ? ProjectLifecycleCompletionIssue.ReleaseRecordUnconfirmed
                    : diagnosticsUnavailable ? ProjectLifecycleCompletionIssue.DiagnosticsUnavailable : null, diagnosticsUnavailable);
            }
        } catch (Exception recoveryFailure) {
            return new ProjectLifecycleOutcome.Unconfirmed(attempt.Submission, ProjectLifecycleRecoveryReason.CommitUnconfirmed,
                !TryLog(attempt, Combine(failure, recoveryFailure), false));
        }
        var diagnosticFailure = failure is not null && !TryLog(attempt, failure, false);
        if (scope?.CommitAttempted == true || attempt.ReplayObserved) {
            return new ProjectLifecycleOutcome.Unconfirmed(attempt.Submission, ProjectLifecycleRecoveryReason.CommitUnconfirmed, diagnosticFailure);
        }
        return new ProjectLifecycleOutcome.Refused(attempt.Submission, failure is ProjectLifecycleRefusedException refused
            ? refused.Reason : failure is FiniteOperationAdmissionException ? ProjectLifecycleRefusal.AdmissionClosed
            : ProjectLifecycleRefusal.TransactionConflict, diagnosticFailure);
    }

    private bool TryLog(ProjectLifecycleAttempt attempt, Exception failure, bool committed)
        => TryLog(attempt.Submission, failure, committed);

    private async Task<Exception?> CompleteKnownAsync(ProjectLifecycleAttempt attempt, CancellationToken cancellationToken) {
        var failures = new List<Exception>();
        try {
            protocol.RequireOriginal(attempt);
            await ProjectLifecycleOperationProtocol.RequirePostReleaseWriteAsync(attempt, cancellationToken);
            protocol.RequireOriginal(attempt);
            await activity.RecordAsync(new ActivityWriteRequest("projects", ActivityAction(attempt.Submission.Command.Kind),
                "Project lifecycle change committed", attempt.Target.ProjectId.ToString("D"),
                ProjectId: attempt.Target.ProjectId, ArtifactKind: "project", ArtifactId: attempt.Target.ProjectId,
                Route: "/projects", IdempotencyKey: $"projects:lifecycle:{attempt.Submission.Operation.Namespace.Value:N}:{attempt.Submission.Operation.Id:N}"), cancellationToken);
            protocol.RequireOriginal(attempt);
        } catch (Exception activityFailure) {
            failures.Add(activityFailure);
        }
        if (attempt.Accepted?.Acknowledgement is ProjectLifecycleAcknowledgement.DeletionCommitted deletion) {
            foreach (var obligation in deletion.Completion.Obligations) {
                try {
                    await completionDispatcher.CompleteAsync(deletion.Completion.Original, obligation,
                        attempt.Invocation.PostReleaseWrite, cancellationToken);
                } catch (Exception completionFailure) {
                    failures.Add(completionFailure);
                }
            }
        }
        return failures.Count switch {
            0 => null,
            1 => failures[0],
            _ => new AggregateException("The committed Project lifecycle change has incomplete postcommit processing.", failures)
        };
    }

    private static string ActivityAction(ProjectLifecycleCommandKind kind) => kind switch {
        ProjectLifecycleCommandKind.Delete => DeleteActivity,
        ProjectLifecycleCommandKind.EnsureLink => AttachActivity,
        ProjectLifecycleCommandKind.RemoveLink => DetachActivity,
        ProjectLifecycleCommandKind.ReconnectLink => ReconnectActivity,
        _ => throw new NotSupportedException("The lifecycle activity kind is unsupported.")
    };

    private const string DeleteActivity = "delete";
    private const string AttachActivity = "attach-subproject";
    private const string DetachActivity = "detach-subproject";
    private const string ReconnectActivity = "reconnect-subproject";

    private bool TryLog(ProjectLifecycleSubmission original, Exception failure, bool committed) {
        try {
            logger.LogError("Project lifecycle operation {OperationId}, dataset {NamespaceId}, epoch {Epoch}, kind {Kind} failed with {FailureType}; commit acknowledged: {Committed}.",
                original.Operation.Id, original.Operation.Namespace.Value, original.Operation.Epoch.Value,
                original.Command.Kind, failure.GetType().FullName, committed);
            return true;
        } catch (Exception) {
            return false;
        }
    }

    private static Exception Combine(Exception? first, Exception second) => first is null ? second : new AggregateException(first, second);
}
