using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectReservationCancellationOwner(IDbContextFactory<ProjectsDbContext> contexts,
    ProjectWriteAdmissionService admissions, ProjectDeletionCompletionOwner completionOwner,
    ProjectReservationCancellationReader receipts, IProjectCreationCompensationGuard compensationGuard,
    CoordinatedDatabaseTransaction transactions, IEnumerable<IProjectDeletionParticipant> participants,
    SearchIndexService search, StorageCatalogService storage, IClock clock, IActivityStream activity,
    ProjectDeletionCompletionDispatcher dispatcher, ProjectCreationCleanupAuthority cleanupAuthority,
    IOptions<ProjectDefinitionOperationOptions> options,
    ILogger<ProjectReservationCancellationOwner> logger) {
    private readonly ProjectLifecycleEffects effects = new(participants, transactions, search, storage, clock);

    public async Task<ProjectReservationCancellationOutcome> CancelAsync(ProjectReservationCancellationRequest original,
        CancellationToken cancellationToken = default, ProjectMutationAuthorization? authorization = null)
        => await CancelAsync(original, await cleanupAuthority.CaptureLocalAsync(original.Original, cancellationToken),
            cancellationToken, authorization);

    public async Task<ProjectReservationCancellationOutcome> CancelAsync(ProjectReservationCancellationRequest original,
        IProjectLifecycleCurrentWriteAccess postReleaseWrite, CancellationToken cancellationToken = default,
        ProjectMutationAuthorization? authorization = null) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(postReleaseWrite);
        var observed = await receipts.ObserveAsync(original, cancellationToken);
        if (observed is ProjectReservationCancellationObservation.Committed found) {
            return new ProjectReservationCancellationOutcome.Committed(original, found.Completion, true);
        }
        if (observed is ProjectReservationCancellationObservation.Unavailable unavailable) {
            return new ProjectReservationCancellationOutcome.Unconfirmed(original, ProjectCreationCompensationRecoveryReason.ReadUnavailable,
                unavailable.DiagnosticsUnavailable);
        }
        using var mutation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var configured = options.Value;
        if (!configured.IsValid()) {
            throw new InvalidOperationException("Reservation cancellation requires the validated original owner mutation budget.");
        }
        mutation.CancelAfter(configured.MutationDeadline);
        ProjectsDbContext? context = null;
        ProjectSourceMutationScope? scope = null;
        ProjectDeletionCompletionOwner.Attempt? completion = null;
        ProjectDeletionCompletionOwner.CloseResult? closed = null;
        ProjectDeletionCompletionBinding? retained = null;
        ProjectCreationDatasetBinding? binding = null;
        Exception? failure = null;
        var replayed = false;
        var ownerReleased = false;
        try {
            binding = await admissions.CaptureCreationBindingAsync(mutation.Token);
            if (binding.Profile.Profile.Id != original.Preparation.Target.Project.DatabaseProfileId ||
                    binding.Binding.Namespace != original.Original.Completion.Namespace) {
                throw new InvalidOperationException("Reservation cancellation cannot change its original profile or dataset.");
            }
            var selected = effects.CaptureParticipants();
            var target = original.Preparation.Target;
            var keys = selected.SelectMany(participant => participant.PreparationScopeKeys.Select(key => key.Value))
                .Append(ProjectMutationScopeKeys.ForProject(target.Project.ProjectId)).Append(ProjectMutationScopeKeys.Hierarchy).ToArray();
            context = await contexts.CreateDbContextAsync(mutation.Token);
            CoordinatedDatabaseTransaction.ForProfile(binding.Profile).RequireOwnerProfile(context);
            scope = await ProjectSourceMutationScope.BeginReservationCancellationAsync(context, keys, original, authorization,
                admissions, transactions, binding, mutation.Token);
            var existing = await ProjectReservationCancellationReader.ReadEnlistedAsync(context, binding.Binding, original, mutation.Token);
            if (existing is ProjectReservationCancellationObservation.Committed committed) {
                retained = committed.Completion;
                replayed = true;
            } else if (existing is not ProjectReservationCancellationObservation.NotObserved) {
                throw new InvalidDataException("The original cancellation completion cannot be admitted safely.");
            } else {
                using (transactions.Enter(context)) {
                    await admissions.RequireReservedCancellationForMutationAsync(original.Preparation, mutation.Token);
                    if (!await compensationGuard.CanCancelReservationForMutationAsync(original.Preparation, mutation.Token)) {
                        throw new InvalidOperationException("The original reservation has residual project state and cannot be cancelled.");
                    }
                }
                if (authorization is null) {
                    await RequireCurrentWriteAsync(postReleaseWrite, mutation.Token);
                }
                var evidence = new ProjectDeletionRecoveryEvidence(original.Original,
                    new ProjectDeletionOriginEvidence.ReservedCancellation(target), scope.DeletionSource,
                    scope.RequiredProjects.Append(target.Project).Distinct().ToArray(),
                    selected.Select(participant => new ProjectDeletionPreparationRegistration(
                        participant.Id, participant.RecoveryProtocolVersion)).ToArray());
                completion = completionOwner.Capture(context, scope, binding.Profile, binding.Generation, binding.Binding, evidence);
                retained = await effects.PrepareReservedCancellationAsync(context, original.Preparation, selected, mutation.Token);
                completion.StageParticipants(retained);
                await admissions.CancelReservedCreationAsync(context, original.Preparation, mutation.Token);
                await context.SaveChangesAsync(mutation.Token);
                await scope.CommitReservedCancellationAsync(evidence, async token => {
                    await admissions.RequireCreationBindingAsync(binding, token);
                    if (authorization is null) {
                        await RequireCurrentWriteAsync(postReleaseWrite, token);
                    }
                }, mutation.Token);
            }
        } catch (Exception operationFailure) {
            failure = operationFailure;
        }
        if (completion is not null) {
            closed = await completion.CloseAsync(CancellationToken.None);
            ownerReleased = closed.OriginalOwnerReleased;
            if (closed.Failure is { } closeFailure) {
                failure = Combine(failure, closeFailure);
            }
        } else {
            ownerReleased = scope is not null || context is null;
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
        if (scope?.IsCommitted == true || replayed) {
            if (retained is null) {
                throw new InvalidOperationException("Known reservation cancellation has no exact completion binding.");
            }
            if (!replayed && ownerReleased && closed?.ReleaseAcknowledged == true) {
                var followUp = await CompleteKnownAsync(original, retained, binding!, authorization, postReleaseWrite, cancellationToken);
                if (followUp is not null) {
                    failure = Combine(failure, followUp);
                }
            }
            var issue = !ownerReleased ? ProjectLifecycleCompletionIssue.OwnerReleaseUnconfirmed
                : !replayed && closed?.ReleaseAcknowledged != true ? ProjectLifecycleCompletionIssue.ReleaseRecordUnconfirmed
                : failure is not null ? ProjectLifecycleCompletionIssue.PostCommitProcessing : (ProjectLifecycleCompletionIssue?)null;
            var diagnosticsUnavailable = failure is not null && !TryLog(original, failure, true);
            if (diagnosticsUnavailable) {
                issue ??= ProjectLifecycleCompletionIssue.DiagnosticsUnavailable;
            }
            return new ProjectReservationCancellationOutcome.Committed(original, retained, replayed, issue, diagnosticsUnavailable);
        }
        if (!ownerReleased) {
            return new ProjectReservationCancellationOutcome.Unconfirmed(original, ProjectCreationCompensationRecoveryReason.OwnerReleaseUnconfirmed,
                failure is not null && !TryLog(original, failure, false));
        }
        using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try {
            var recovered = await receipts.ObserveAsync(original, recovery.Token);
            if (recovered is ProjectReservationCancellationObservation.Committed committed) {
                return new ProjectReservationCancellationOutcome.Committed(original, committed.Completion, true,
                    DiagnosticsUnavailable: failure is not null && !TryLog(original, failure, true));
            }
            if (recovered is ProjectReservationCancellationObservation.Unavailable recoveryUnavailable) {
                return new ProjectReservationCancellationOutcome.Unconfirmed(original, ProjectCreationCompensationRecoveryReason.ReadUnavailable,
                    recoveryUnavailable.DiagnosticsUnavailable || failure is not null && !TryLog(original, failure, false));
            }
        } catch (Exception recoveryFailure) {
            return new ProjectReservationCancellationOutcome.Unconfirmed(original, ProjectCreationCompensationRecoveryReason.CommitUnconfirmed,
                !TryLog(original, Combine(failure, recoveryFailure), false));
        }
        var diagnosticsFailed = failure is not null && !TryLog(original, failure, false);
        return scope?.CommitAttempted == true
            ? new ProjectReservationCancellationOutcome.Unconfirmed(original, ProjectCreationCompensationRecoveryReason.CommitUnconfirmed, diagnosticsFailed)
            : new ProjectReservationCancellationOutcome.Declined(original, diagnosticsFailed);
    }

    private async Task<Exception?> CompleteKnownAsync(ProjectReservationCancellationRequest original,
        ProjectDeletionCompletionBinding completion, ProjectCreationDatasetBinding binding,
        ProjectMutationAuthorization? authorization, IProjectLifecycleCurrentWriteAccess currentWrite, CancellationToken cancellationToken) {
        var failures = new List<Exception>();
        currentWrite = cleanupAuthority.AfterRelease(binding, completion, authorization, original.Reservation, currentWrite);
        try {
            await admissions.RequireCreationBindingAsync(binding, cancellationToken);
            await RequireCurrentWriteAsync(currentWrite, cancellationToken);
            await activity.RecordAsync(new ActivityWriteRequest("projects", "cancel-reservation", "Project creation reservation cancelled",
                original.Preparation.Target.Project.ProjectId.ToString("D"), ProjectId: original.Preparation.Target.Project.ProjectId,
                ArtifactKind: "project", ArtifactId: original.Preparation.Target.Project.ProjectId, Route: "/projects",
                IdempotencyKey: $"projects:reservation-cancellation:{completion.Original.Completion.Namespace.Value:N}:{completion.Original.Completion.Id.Value:N}"), cancellationToken);
        } catch (Exception activityFailure) {
            failures.Add(activityFailure);
        }
        foreach (var obligation in completion.Obligations) {
            try {
                await admissions.RequireCreationBindingAsync(binding, cancellationToken);
                await dispatcher.CompleteAsync(completion.Original, obligation, currentWrite, cancellationToken);
            } catch (Exception completionFailure) {
                failures.Add(completionFailure);
            }
        }
        return failures.Count switch {
            0 => null,
            1 => failures[0],
            _ => new AggregateException("Committed reservation cancellation has incomplete postcommit processing.", failures)
        };
    }

    private static async ValueTask RequireCurrentWriteAsync(IProjectLifecycleCurrentWriteAccess current,
        CancellationToken cancellationToken) {
        if (await current.ObserveAsync(cancellationToken) != ProjectLifecycleWriteAccessDecision.Allowed) {
            throw new InvalidOperationException("Current original reservation cancellation authority could not be confirmed.");
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private bool TryLog(ProjectReservationCancellationRequest original, Exception failure, bool committed) {
        try {
            logger.LogError("Reservation cancellation {CompletionId} in dataset {NamespaceId} failed with {FailureType}; commit acknowledged: {Committed}.",
                original.Original.Completion.Id.Value, original.Original.Completion.Namespace.Value, failure.GetType().FullName, committed);
            return true;
        } catch (Exception) {
            return false;
        }
    }

    private static Exception Combine(Exception? first, Exception second) => first is null ? second : new AggregateException(first, second);
}
