using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectCreationCompensationOwner(IDbContextFactory<ProjectsDbContext> contexts,
    ProjectWriteAdmissionService admissions, ProjectDeletionCompletionOwner completionOwner,
    ProjectCreationCompensationReader receipts, IProjectCreationCompensationGuard compensationGuard,
    CoordinatedDatabaseTransaction transactions, IEnumerable<IProjectDeletionParticipant> participants,
    SearchIndexService search, StorageCatalogService storage, IClock clock, IActivityStream activity,
    ProjectDeletionCompletionDispatcher dispatcher, ProjectCreationCleanupAuthority cleanupAuthority,
    IOptions<ProjectDefinitionOperationOptions> options,
    ILogger<ProjectCreationCompensationOwner> logger) {
    private readonly ProjectLifecycleEffects effects = new(participants, transactions, search, storage, clock);

    public async Task<ProjectCreationCompensationOutcome> CompensateAsync(ProjectCreationCompensationRequest original,
        CancellationToken cancellationToken = default)
        => await CompensateAsync(original, await cleanupAuthority.CaptureLocalAsync(original.Original, cancellationToken), cancellationToken);

    public async Task<ProjectCreationCompensationOutcome> CompensateAsync(ProjectCreationCompensationRequest original,
        IProjectLifecycleCurrentWriteAccess postReleaseWrite, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(postReleaseWrite);
        var observed = await receipts.ObserveAsync(original, cancellationToken);
        if (observed is ProjectCreationCompensationObservation.Committed found) {
            return new ProjectCreationCompensationOutcome.Committed(original, found.Completion, true);
        }
        if (observed is ProjectCreationCompensationObservation.Unavailable unavailable) {
            return new ProjectCreationCompensationOutcome.Unconfirmed(original, ProjectCreationCompensationRecoveryReason.ReadUnavailable,
                unavailable.DiagnosticsUnavailable);
        }
        using var mutation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var configured = options.Value;
        if (!configured.IsValid()) {
            throw new InvalidOperationException("Creation compensation requires the validated original owner mutation budget.");
        }
        mutation.CancelAfter(configured.MutationDeadline);
        ProjectsDbContext? context = null;
        ProjectSourceMutationScope? scope = null;
        ProjectDeletionCompletionOwner.Attempt? completion = null;
        ProjectDeletionCompletionOwner.CloseResult? closed = null;
        ProjectDeletionCompletionBinding? retained = null;
        ProjectCreationDatasetBinding? binding = null;
        Exception? failure = null;
        var declined = false;
        var replayed = false;
        var ownerReleased = false;
        try {
            binding = await admissions.CaptureCreationBindingAsync(mutation.Token);
            if (binding.Profile.Profile.Id != original.Receipt.Project.DatabaseProfileId ||
                    binding.Binding.Namespace != original.Receipt.Namespace) {
                throw new InvalidOperationException("Creation compensation cannot change its original profile or dataset.");
            }
            var selected = effects.CaptureParticipants();
            var receipt = original.Receipt;
            var expected = receipt.Authorization?.ExpectedProjects.ToArray() ?? (receipt.Reservation?.ParentProjectId is { } parent
                ? [new ProjectWriteAdmission(receipt.Project.DatabaseProfileId, parent, receipt.Reservation.ParentLifetimeId!.Value)] : []);
            var keys = selected.SelectMany(participant => participant.PreparationScopeKeys.Select(key => key.Value))
                .Append(ProjectMutationScopeKeys.ForProject(receipt.Project.ProjectId)).Append(ProjectMutationScopeKeys.Hierarchy).ToArray();
            context = await contexts.CreateDbContextAsync(mutation.Token);
            CoordinatedDatabaseTransaction.ForProfile(binding.Profile).RequireOwnerProfile(context);
            scope = await ProjectSourceMutationScope.BeginDeletionAsync(context, keys,
                receipt.Reservation?.ParentProjectId is not null ? ProjectMutationPurpose.CreateChild : ProjectMutationPurpose.CreateRoot,
                receipt.Project.ProjectId, expected.Select(project => project.ProjectId).ToArray(), receipt.Authorization,
                admissions, transactions, binding.Binding, () => admissions.RequireCreationOwner(binding), mutation.Token,
                receipt.Reservation, expected);
            var existing = await ProjectCreationCompensationReader.ReadEnlistedAsync(context, binding.Binding, original, mutation.Token);
            if (existing is ProjectCreationCompensationObservation.Committed committed) {
                retained = committed.Completion;
                replayed = true;
            } else if (existing is not ProjectCreationCompensationObservation.NotObserved) {
                throw new InvalidDataException("The original compensation completion cannot be admitted safely.");
            } else {
                var target = await RequireOriginalCreationAsync(context, original, mutation.Token);
                if (target is null) {
                    declined = true;
                } else {
                    if (receipt.Authorization is null) {
                        await RequireCurrentWriteAsync(postReleaseWrite, mutation.Token);
                    }
                    var evidence = new ProjectDeletionRecoveryEvidence(original.Original,
                        new ProjectDeletionOriginEvidence.Compensation(receipt.Project, receipt.Fingerprint, receipt.Reservation),
                        scope.DeletionSource, scope.RequiredProjects.Append(receipt.Project).Distinct().ToArray(),
                        selected.Select(participant => new ProjectDeletionPreparationRegistration(
                            participant.Id, participant.RecoveryProtocolVersion)).ToArray());
                    completion = completionOwner.Capture(context, scope, binding.Profile, binding.Generation, binding.Binding, evidence);
                    retained = await effects.PrepareDeletionAsync(context, original.Original, selected, mutation.Token);
                    completion.StageParticipants(retained);
                    if (receipt.Reservation is { } reservation) {
                        await admissions.CancelConsumedCreationForCompensationAsync(context, reservation, receipt.Namespace, mutation.Token);
                    }
                    await effects.DeleteAsync(context, target, mutation.Token);
                    await scope.CommitDeletionAsync(evidence, async token => {
                        await admissions.RequireCreationBindingAsync(binding, token);
                        if (receipt.Authorization is null) {
                            await RequireCurrentWriteAsync(postReleaseWrite, token);
                        }
                    }, mutation.Token);
                }
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
                throw new InvalidOperationException("Known creation compensation has no exact completion binding.");
            }
            if (!replayed && ownerReleased && closed?.ReleaseAcknowledged == true) {
                var followUp = await CompleteKnownAsync(original, retained, binding!, postReleaseWrite, cancellationToken);
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
            return new ProjectCreationCompensationOutcome.Committed(original, retained, replayed, issue, diagnosticsUnavailable);
        }
        if (!ownerReleased) {
            return new ProjectCreationCompensationOutcome.Unconfirmed(original, ProjectCreationCompensationRecoveryReason.OwnerReleaseUnconfirmed,
                failure is not null && !TryLog(original, failure, false));
        }
        using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try {
            var recovered = await receipts.ObserveAsync(original, recovery.Token);
            if (recovered is ProjectCreationCompensationObservation.Committed committed) {
                return new ProjectCreationCompensationOutcome.Committed(original, committed.Completion, true,
                    DiagnosticsUnavailable: failure is not null && !TryLog(original, failure, true));
            }
            if (recovered is ProjectCreationCompensationObservation.Unavailable recoveryUnavailable) {
                return new ProjectCreationCompensationOutcome.Unconfirmed(original, ProjectCreationCompensationRecoveryReason.ReadUnavailable,
                    recoveryUnavailable.DiagnosticsUnavailable || failure is not null && !TryLog(original, failure, false));
            }
        } catch (Exception recoveryFailure) {
            return new ProjectCreationCompensationOutcome.Unconfirmed(original, ProjectCreationCompensationRecoveryReason.CommitUnconfirmed,
                !TryLog(original, Combine(failure, recoveryFailure), false));
        }
        var diagnosticsFailed = failure is not null && !TryLog(original, failure, false);
        return scope?.CommitAttempted == true
            ? new ProjectCreationCompensationOutcome.Unconfirmed(original, ProjectCreationCompensationRecoveryReason.CommitUnconfirmed, diagnosticsFailed)
            : new ProjectCreationCompensationOutcome.Declined(original, declined
                ? ProjectCreationCompensationRefusal.OriginalCreationChanged : ProjectCreationCompensationRefusal.TransactionConflict, diagnosticsFailed);
    }

    private async Task<Project?> RequireOriginalCreationAsync(ProjectsDbContext context,
        ProjectCreationCompensationRequest original, CancellationToken cancellationToken) {
        var receipt = original.Receipt;
        if (receipt.Reservation is { } reservation && !await admissions.IsExactConsumedCreationAsync(context,
                reservation, receipt.Namespace, cancellationToken)) {
            return null;
        }
        var project = await context.Set<Project>().SingleOrDefaultAsync(value => value.Id == receipt.Project.ProjectId, cancellationToken);
        if (project is null || project.LifetimeId != receipt.Project.LifetimeId) {
            return null;
        }
        await admissions.RequireAsync(context, receipt.Project, cancellationToken);
        if (!string.Equals(await ProjectCreationFingerprint.ReadAsync(context, receipt.Project, cancellationToken),
                receipt.Fingerprint, StringComparison.Ordinal)) {
            return null;
        }
        using var coordination = transactions.Enter(context);
        return await compensationGuard.CanCompensateForMutationAsync(receipt.Project, cancellationToken) ? project : null;
    }

    private async Task<Exception?> CompleteKnownAsync(ProjectCreationCompensationRequest original,
        ProjectDeletionCompletionBinding completion, ProjectCreationDatasetBinding binding,
        IProjectLifecycleCurrentWriteAccess currentWrite, CancellationToken cancellationToken) {
        var failures = new List<Exception>();
        currentWrite = cleanupAuthority.AfterRelease(binding, completion, original.Receipt.Authorization, original.Receipt.Reservation, currentWrite);
        try {
            await admissions.RequireCreationBindingAsync(binding, cancellationToken);
            await RequireCurrentWriteAsync(currentWrite, cancellationToken);
            await activity.RecordAsync(new ActivityWriteRequest("projects", "compensate-creation", "Project creation compensated",
                original.Receipt.Project.ProjectId.ToString("D"), ProjectId: original.Receipt.Project.ProjectId,
                ArtifactKind: "project", ArtifactId: original.Receipt.Project.ProjectId, Route: "/projects",
                IdempotencyKey: $"projects:creation-compensation:{completion.Original.Completion.Namespace.Value:N}:{completion.Original.Completion.Id.Value:N}"), cancellationToken);
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
            _ => new AggregateException("Committed creation compensation has incomplete postcommit processing.", failures)
        };
    }

    private static async ValueTask RequireCurrentWriteAsync(IProjectLifecycleCurrentWriteAccess current,
        CancellationToken cancellationToken) {
        if (await current.ObserveAsync(cancellationToken) != ProjectLifecycleWriteAccessDecision.Allowed) {
            throw new InvalidOperationException("Current original creation compensation authority could not be confirmed.");
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private bool TryLog(ProjectCreationCompensationRequest original, Exception failure, bool committed) {
        try {
            logger.LogError("Creation compensation {CompletionId} in dataset {NamespaceId} failed with {FailureType}; commit acknowledged: {Committed}.",
                original.Original.Completion.Id.Value, original.Original.Completion.Namespace.Value, failure.GetType().FullName, committed);
            return true;
        } catch (Exception) {
            return false;
        }
    }

    private static Exception Combine(Exception? first, Exception second) => first is null ? second : new AggregateException(first, second);
}
