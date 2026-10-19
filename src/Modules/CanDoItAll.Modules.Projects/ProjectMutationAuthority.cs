using System.Collections.Immutable;
using System.Data;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Modules.Projects;

public enum ProjectMutationPurpose { ExistingWrite, HierarchyWrite, ReserveRoot, ReserveChild, CreateRoot, CreateChild }

public sealed record ProjectMutationTarget(ProjectMutationPurpose Purpose, Guid ProjectId,
    ImmutableArray<ProjectWriteAdmission> ExistingProjects, ProjectCreationReservation? Reservation = null,
    Guid? RequesterId = null) {
    public void RequireReservedCancellation(ProjectReservationCancellationTarget original) {
        ArgumentNullException.ThrowIfNull(original);
        var expected = new ProjectCreationReservation(original.ReservationId, original.Project.DatabaseProfileId,
            original.Project.ProjectId, original.Project.LifetimeId, original.RequesterId,
            original.Parent?.ProjectId, original.Parent?.LifetimeId);
        if (Purpose != (original.Parent is null ? ProjectMutationPurpose.ReserveRoot : ProjectMutationPurpose.ReserveChild) ||
                ProjectId != original.Project.ProjectId || RequesterId != original.RequesterId || Reservation != expected ||
                original.Parent is not null && !ExistingProjects.Contains(original.Parent)) {
            throw new InvalidOperationException("Final cancellation changed the exact original reserved target or parent lifetime.");
        }
    }
}

public interface IProjectMutationSourceAuthority {
    Task<IProjectMutationSourceLease> AcquireAsync(ProjectMutationTarget target, CancellationToken cancellationToken = default);
}

public interface IProjectMutationSourceLease : IAsyncDisposable {
    IReadOnlyCollection<ProjectWriteAdmission> SourceProjects { get; }
    ProjectDeletionSourceRecovery CaptureDeletionRecoverySource()
        => throw new NotSupportedException("This project source lease cannot capture its original deletion recovery binding.");
    Task RequireForMutationAsync(CancellationToken cancellationToken = default);
    Task RequireForDeletionCommitAsync(ProjectDeletionCommitTarget target, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This source lease cannot validate final deletion authority.");
    Task RequireForReservedCancellationCommitAsync(ProjectReservationCancellationTarget target, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This source lease cannot validate final never-created reservation cancellation.");
}

public sealed class ProjectMutationAuthorization {
    public ProjectMutationAuthorization(IProjectMutationSourceAuthority sourceAuthority,
        IReadOnlyCollection<ProjectWriteAdmission> expectedProjects) {
        ArgumentNullException.ThrowIfNull(sourceAuthority);
        ArgumentNullException.ThrowIfNull(expectedProjects);
        SourceAuthority = sourceAuthority;
        ExpectedProjects = expectedProjects.ToImmutableArray();
        if (ExpectedProjects.Select(project => project.ProjectId).Distinct().Count() != ExpectedProjects.Length) {
            throw new ArgumentException("Captured project admissions must identify distinct projects.", nameof(expectedProjects));
        }
    }

    public IProjectMutationSourceAuthority SourceAuthority { get; }
    public ImmutableArray<ProjectWriteAdmission> ExpectedProjects { get; }
}

internal sealed class ProjectSourceMutationScope(ProjectsDbContext context, SerializableMutationScope? mutation, IDbContextTransaction? transaction,
    IProjectMutationSourceLease? source, ImmutableArray<ProjectWriteAdmission> expectedProjects,
    ProjectWriteAdmissionService admissions, CoordinatedDatabaseTransaction transactions,
    Action? requireCurrentOwner = null, ProjectDeletionSourceRecovery? deletionSource = null,
    ProjectMutationTarget? deletionTarget = null) : IAsyncDisposable {
    private bool committed;
    private bool commitAttempted;
    private int disposed;
    private readonly object releaseSync = new();
    private Task? sourceRelease;
    private Task? ownerRelease;

    internal bool IsCommitted => committed;
    internal bool CommitAttempted => commitAttempted;

    internal ProjectDeletionSourceRecovery DeletionSource => deletionSource
        ?? throw new InvalidOperationException("Only an admitted deletion owner has captured source recovery evidence.");

    internal ImmutableArray<ProjectWriteAdmission> RequiredProjects => expectedProjects;

    internal static async Task<ProjectSourceMutationScope> BeginLifecycleAsync(ProjectsDbContext context,
        IReadOnlyCollection<string> keys, ProjectLifecycleAttempt attempt, ProjectLifecycleOperationProtocol protocol,
        ProjectWriteAdmissionService admissions, CoordinatedDatabaseTransaction transactions, CancellationToken cancellationToken) {
        using var admission = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        admission.CancelAfter(TimeSpan.FromSeconds(5));
        var purpose = attempt.Submission.Command is ProjectLifecycleCommand.Delete
            ? ProjectMutationPurpose.ExistingWrite : ProjectMutationPurpose.HierarchyWrite;
        return await BeginCoreAsync(context, keys.Append(attempt.OperationKey).ToArray(), purpose, attempt.Target.ProjectId,
            attempt.ExpectedProjects.Select(project => project.ProjectId).ToArray(), attempt.Invocation.Authorization,
            admissions, transactions, admission.Token, null, null, attempt.ExpectedProjects, definitionWrite: false,
            deletionBinding: attempt.Binding, requireCurrentOwner: () => protocol.RequireOriginal(attempt),
            lifecycleProtocol: protocol, lifecycleAttempt: attempt);
    }

    internal static async Task<ProjectSourceMutationScope> BeginDeletionAsync(ProjectsDbContext context,
        IReadOnlyCollection<string> keys, ProjectMutationPurpose purpose, Guid projectId,
        IReadOnlyCollection<Guid> existingProjectIds, ProjectMutationAuthorization? authorization,
        ProjectWriteAdmissionService admissions, CoordinatedDatabaseTransaction transactions,
        DatasetActivationBinding binding, Action requireCurrentOwner, CancellationToken cancellationToken,
        ProjectCreationReservation? reservation = null, IReadOnlyCollection<ProjectWriteAdmission>? expectedProjectAdmissions = null) {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(requireCurrentOwner);
        using var admission = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        admission.CancelAfter(TimeSpan.FromSeconds(5));
        return await BeginCoreAsync(context, keys, purpose, projectId, existingProjectIds, authorization,
            admissions, transactions, admission.Token, reservation, null, expectedProjectAdmissions, definitionWrite: false,
            deletionBinding: binding, requireCurrentOwner: requireCurrentOwner);
    }

    internal static async Task<ProjectSourceMutationScope> BeginReservationCancellationAsync(ProjectsDbContext context,
        IReadOnlyCollection<string> keys, ProjectReservationCancellationRequest original,
        ProjectMutationAuthorization? authorization, ProjectWriteAdmissionService admissions,
        CoordinatedDatabaseTransaction transactions, ProjectCreationDatasetBinding binding, CancellationToken cancellationToken) {
        var target = original.Preparation.Target;
        var expected = target.Parent is null ? Array.Empty<ProjectWriteAdmission>() : [target.Parent];
        using var admission = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        admission.CancelAfter(TimeSpan.FromSeconds(5));
        return await BeginCoreAsync(context, keys, target.Parent is null ? ProjectMutationPurpose.ReserveRoot : ProjectMutationPurpose.ReserveChild,
            target.Project.ProjectId, expected.Select(project => project.ProjectId).ToArray(), authorization, admissions,
            transactions, admission.Token, original.Reservation, target.RequesterId, expected, definitionWrite: false,
            deletionBinding: binding.Binding, requireCurrentOwner: () => admissions.RequireCreationOwner(binding));
    }

    internal static Task<ProjectSourceMutationScope> BeginBoundAsync(ProjectsDbContext context,
        IReadOnlyCollection<string> keys, ProjectMutationPurpose purpose, Guid projectId,
        IReadOnlyCollection<Guid> existingProjectIds, ProjectMutationAuthorization? authorization,
        ProjectWriteAdmissionService admissions, CoordinatedDatabaseTransaction transactions,
        ProjectCreationDatasetBinding original, CancellationToken cancellationToken,
        ProjectCreationReservation? reservation = null, Guid? requesterId = null,
        IReadOnlyCollection<ProjectWriteAdmission>? expectedProjectAdmissions = null)
        => BeginCoreAsync(context, keys, purpose, projectId, existingProjectIds, authorization, admissions,
            transactions, cancellationToken, reservation, requesterId, expectedProjectAdmissions, definitionWrite: false,
            requireCurrentOwner: () => admissions.RequireCreationOwner(original), boundBinding: original.Binding);

    internal static Task<ProjectSourceMutationScope> BeginDefinitionAsync(ProjectsDbContext context,
        IReadOnlyCollection<string> keys, ProjectMutationPurpose purpose, Guid projectId,
        IReadOnlyCollection<Guid> existingProjectIds, ProjectMutationAuthorization? authorization,
        ProjectWriteAdmissionService admissions, CoordinatedDatabaseTransaction transactions,
        CancellationToken cancellationToken, ProjectCreationReservation? reservation = null,
        IReadOnlyCollection<ProjectWriteAdmission>? expectedProjectAdmissions = null,
        ProjectDefinitionOperationProtocol? protocol = null, ProjectDefinitionOperationAttempt? attempt = null)
        => BeginCoreAsync(context, keys, purpose, projectId, existingProjectIds, authorization, admissions,
            transactions, cancellationToken, reservation, null, expectedProjectAdmissions, definitionWrite: true, protocol, attempt);

    internal static Task<ProjectSourceMutationScope> BeginAsync(ProjectsDbContext context,
        IReadOnlyCollection<string> keys, ProjectMutationPurpose purpose, Guid projectId,
        IReadOnlyCollection<Guid> existingProjectIds, ProjectMutationAuthorization? authorization,
        ProjectWriteAdmissionService admissions, CoordinatedDatabaseTransaction transactions,
        CancellationToken cancellationToken, ProjectCreationReservation? reservation = null, Guid? requesterId = null,
        IReadOnlyCollection<ProjectWriteAdmission>? expectedProjectAdmissions = null)
        => BeginCoreAsync(context, keys, purpose, projectId, existingProjectIds, authorization, admissions,
            transactions, cancellationToken, reservation, requesterId, expectedProjectAdmissions, definitionWrite: false);

    private static async Task<ProjectSourceMutationScope> BeginCoreAsync(ProjectsDbContext context,
        IReadOnlyCollection<string> keys, ProjectMutationPurpose purpose, Guid projectId,
        IReadOnlyCollection<Guid> existingProjectIds, ProjectMutationAuthorization? authorization,
        ProjectWriteAdmissionService admissions, CoordinatedDatabaseTransaction transactions,
        CancellationToken cancellationToken, ProjectCreationReservation? reservation, Guid? requesterId,
        IReadOnlyCollection<ProjectWriteAdmission>? expectedProjectAdmissions, bool definitionWrite,
        ProjectDefinitionOperationProtocol? protocol = null, ProjectDefinitionOperationAttempt? attempt = null,
        DatasetActivationBinding? deletionBinding = null, Action? requireCurrentOwner = null,
        ProjectLifecycleOperationProtocol? lifecycleProtocol = null, ProjectLifecycleAttempt? lifecycleAttempt = null,
        DatasetActivationBinding? boundBinding = null) {
        IProjectMutationSourceLease? source = null;
        SerializableMutationScope? mutation = null;
        IDbContextTransaction? transaction = null;
        ProjectDeletionSourceRecovery? capturedDeletionSource = null;
        var expected = expectedProjectAdmissions?.ToImmutableArray() ?? authorization?.ExpectedProjects ?? [];
        var target = new ProjectMutationTarget(purpose, projectId, expected, reservation, requesterId);
        if (authorization is not null && expectedProjectAdmissions is not null &&
            !authorization.ExpectedProjects.ToHashSet().SetEquals(expected)) {
            throw new ArgumentException("The editor and source authority must identify the same original project lifetimes.", nameof(expectedProjectAdmissions));
        }
        if ((authorization is not null || expectedProjectAdmissions is not null) &&
            (expected.Select(project => project.ProjectId).Distinct().Count() != expected.Length ||
             !existingProjectIds.ToHashSet().SetEquals(expected.Select(project => project.ProjectId)))) {
            throw new ArgumentException("The captured admissions must cover exactly the existing projects affected by this owner operation.", nameof(authorization));
        }
        if (context.Database.CurrentTransaction is not null) {
            throw new InvalidOperationException("Project source authority must be acquired before its owner transaction starts.");
        }
        try {
            if (authorization is not null) {
                source = await authorization.SourceAuthority.AcquireAsync(target, cancellationToken)
                    ?? throw new InvalidOperationException("The explicit project source policy did not return a held authority lease.");
            }
            var required = expected.Concat(source?.SourceProjects ?? []).Distinct().ToImmutableArray();
            if (required.GroupBy(project => project.ProjectId).Any(group => group.Count() > 1)) {
                throw new InvalidOperationException("The owner target and original source identify different lifetimes of the same project.");
            }
            if (deletionBinding is not null && (lifecycleAttempt is null || lifecycleAttempt.Submission.Command is ProjectLifecycleCommand.Delete)) {
                capturedDeletionSource = source is null
                    ? new ProjectDeletionSourceRecovery.NoExternalSourceLease()
                    : source.CaptureDeletionRecoverySource();
                if (capturedDeletionSource is null) {
                    throw new InvalidOperationException("The acquired source did not provide deletion recovery evidence.");
                }
                if (capturedDeletionSource is ProjectDeletionSourceRecovery.CanonicalOrganizationCatalog catalog) {
                    if (catalog.DatabaseProfileId != admissions.DatabaseProfileId ||
                            required.Any(project => project.DatabaseProfileId != catalog.DatabaseProfileId)) {
                        throw new InvalidOperationException("The acquired catalog and original project admissions belong to different profiles.");
                    }
                    catalog.RequireCurrentHost();
                }
            }
            var orderedKeys = keys.Concat(required.Select(project => ProjectMutationScopeKeys.ForProject(project.ProjectId)))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if ((deletionBinding ?? boundBinding) is { } ownerBinding) {
                transactions.RequireOwnerProfile(context);
                transaction = await ProjectDeletionTransactionEntry.BeginAsync(context, ownerBinding, orderedKeys, cancellationToken);
                requireCurrentOwner!();
                if (lifecycleAttempt is not null &&
                        await lifecycleProtocol!.EnterBeforeOwnerAdmissionAsync(context, lifecycleAttempt, cancellationToken)) {
                    return new(context, null, transaction, source, required, admissions, transactions, requireCurrentOwner,
                        capturedDeletionSource, target);
                }
                if (source is not null) {
                    using var coordination = transactions.Enter(context);
                    await source.RequireForMutationAsync(cancellationToken);
                }
            } else if ((source is not null || definitionWrite) && context.Database.IsNpgsql()) {
                transaction = await context.Database.BeginTransactionAsync(source is null ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, cancellationToken);
                if (attempt is not null && await protocol!.EnterBeforeOwnerAdmissionAsync(context, attempt, cancellationToken)) {
                    return new(context, null, transaction, source, [], admissions, transactions);
                }
                if (source is not null) {
                    using var coordination = transactions.Enter(context);
                    await source.RequireForMutationAsync(cancellationToken);
                }
                await SerializableMutationScope.AcquireRelationalScopeLocksAsync(context, orderedKeys, cancellationToken);
                if (attempt is not null) {
                    await protocol!.RequireAdmissibleAsync(context, attempt, cancellationToken);
                }
            } else {
                if (attempt is not null) {
                    throw new NotSupportedException("Protected definition operations require PostgreSQL.");
                }
                mutation = await SerializableMutationScope.BeginAsync(context, orderedKeys, cancellationToken);
                if (source is not null) {
                    using var coordination = transactions.Enter(context);
                    await source.RequireForMutationAsync(cancellationToken);
                }
            }
            if (required.Length > 0) {
                using var coordination = transactions.Enter(context);
                await admissions.RequireManyForMutationAsync(required, cancellationToken);
            }
            requireCurrentOwner?.Invoke();
            return new(context, mutation, transaction, source, required, admissions, transactions, requireCurrentOwner,
                capturedDeletionSource, deletionBinding is null ? null : target);
        } catch (Exception failure) {
            var failures = new List<Exception> { failure };
            try {
                if (transaction is not null) {
                    await transaction.DisposeAsync();
                }
                if (mutation is not null) {
                    await mutation.DisposeAsync();
                }
            } catch (Exception cleanupFailure) {
                failures.Add(cleanupFailure);
            }
            try {
                if (source is not null) {
                    await source.DisposeAsync();
                }
            } catch (Exception cleanupFailure) {
                failures.Add(cleanupFailure);
            }
            if (failures.Count > 1) {
                throw new AggregateException("Project admission and owner cleanup did not both complete.", failures);
            }
            if (lifecycleAttempt is not null) {
                lifecycleAttempt.AdmissionOwnerReleased = (authorization is null || source is not null) && failure is not AggregateException;
            }
            throw;
        }
    }

    internal Task CommitAsync(CancellationToken cancellationToken) => CommitCoreAsync(null, cancellationToken);

    internal async Task CommitLifecycleAsync(Func<CancellationToken, ValueTask> requireCurrentWrite, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(requireCurrentWrite);
        RequireCommitAvailable();
        using (transactions.Enter(context)) {
            if (source is not null) {
                await source.RequireForMutationAsync(cancellationToken);
            }
            await admissions.RequireManyForMutationAsync(expectedProjects, cancellationToken);
        }
        await requireCurrentWrite(cancellationToken);
        await CommitValidatedAsync(null, cancellationToken);
    }

    internal Task CommitDefinitionAsync(ProjectDefinitionOperationAttempt attempt, CancellationToken cancellationToken)
        => CommitCoreAsync(attempt, cancellationToken);

    internal async Task CommitDeletionAsync(ProjectDeletionRecoveryEvidence evidence,
        Func<CancellationToken, ValueTask> requireCurrentWrite, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(requireCurrentWrite);
        RequireCommitAvailable();
        if (deletionTarget is null || evidence.Source != DeletionSource ||
                evidence.Origin is ProjectDeletionOriginEvidence.LegacyInstallation ||
                !expectedProjects.Append(evidence.Original.Project).ToHashSet().SetEquals(evidence.RequiredProjects)) {
            throw new InvalidOperationException("Final deletion requires its captured original owner and complete source evidence.");
        }
        var cancelled = (evidence.Origin as ProjectDeletionOriginEvidence.Compensation)?.Reservation;
        var target = new ProjectDeletionCommitTarget(evidence.Original.Project, cancelled);
        target.RequireOriginalTarget(deletionTarget);
        if (source?.SourceProjects.Any(project => project.ProjectId == target.Project.ProjectId) == true) {
            throw new InvalidOperationException("Deletion cannot retire a live project source of its own mutation authority.");
        }
        using (transactions.Enter(context)) {
            await admissions.RequireRetiredDeletionAsync(context, target, cancellationToken);
            if (source is not null) {
                await source.RequireForDeletionCommitAsync(target, cancellationToken);
            }
            await admissions.RequireManyForMutationAsync(expectedProjects.Where(project => project != target.Project).ToArray(),
                cancellationToken);
        }
        await requireCurrentWrite(cancellationToken);
        await CommitValidatedAsync(null, cancellationToken);
    }

    internal async Task CommitReservedCancellationAsync(ProjectDeletionRecoveryEvidence evidence,
        Func<CancellationToken, ValueTask> requireCurrentWrite, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(requireCurrentWrite);
        RequireCommitAvailable();
        if (deletionTarget is null || evidence.Source != DeletionSource ||
                evidence.Origin is not ProjectDeletionOriginEvidence.ReservedCancellation cancellation ||
                !expectedProjects.Append(evidence.Original.Project).ToHashSet().SetEquals(evidence.RequiredProjects)) {
            throw new InvalidOperationException("Final reserved cancellation requires its actual original source and complete captured admissions.");
        }
        deletionTarget.RequireReservedCancellation(cancellation.Target);
        if (source?.SourceProjects.Any(project => project.ProjectId == cancellation.Target.Project.ProjectId) == true) {
            throw new InvalidOperationException("The never-created target cannot supply live source authority.");
        }
        using (transactions.Enter(context)) {
            await admissions.RequireCancelledReservationAsync(context, new(evidence.Original, cancellation.Target), cancellationToken);
            if (source is not null) {
                await source.RequireForReservedCancellationCommitAsync(cancellation.Target, cancellationToken);
            }
            await admissions.RequireManyForMutationAsync(expectedProjects, cancellationToken);
        }
        await requireCurrentWrite(cancellationToken);
        await CommitValidatedAsync(null, cancellationToken);
    }

    private async Task CommitCoreAsync(ProjectDefinitionOperationAttempt? attempt, CancellationToken cancellationToken) {
        RequireCommitAvailable();
        if (source is not null || expectedProjects.Length > 0) {
            using var coordination = transactions.Enter(context);
            if (source is not null) {
                await source.RequireForMutationAsync(cancellationToken);
            }
            if (expectedProjects.Length > 0) {
                await admissions.RequireManyForMutationAsync(expectedProjects, cancellationToken);
            }
        }
        if (attempt is not null) {
            await ProjectDefinitionCurrentWriteGuard.RequireAsync(attempt, cancellationToken);
        }
        await CommitValidatedAsync(attempt, cancellationToken);
    }

    private void RequireCommitAvailable() {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (commitAttempted) {
            throw new InvalidOperationException("The original project mutation has already attempted its commit.");
        }
    }

    private async Task CommitValidatedAsync(ProjectDefinitionOperationAttempt? attempt, CancellationToken cancellationToken) {
        RequireCommitAvailable();
        cancellationToken.ThrowIfCancellationRequested();
        requireCurrentOwner?.Invoke();
        commitAttempted = true;
        if (attempt is not null) {
            attempt.CommitAttempted = true;
        }
        if (transaction is not null) {
            await transaction.CommitAsync(cancellationToken);
        } else {
            await mutation!.CommitAsync(cancellationToken);
        }
        committed = true;
        await ReleaseSourceAsync();
    }

    public ValueTask DisposeAsync() {
        lock (releaseSync) {
            if (ownerRelease is null) {
                Volatile.Write(ref disposed, 1);
                ownerRelease = ReleaseOwnerAsync();
            }
            return new(ownerRelease);
        }
    }

    private async Task ReleaseOwnerAsync() {
        Exception? failure = null;
        try {
            if (transaction is not null) {
                await transaction.DisposeAsync();
            }
            if (mutation is not null) {
                await mutation.DisposeAsync();
            }
        } catch (Exception cleanupFailure) {
            failure = cleanupFailure;
        }
        try {
            await ReleaseSourceAsync();
        } catch (Exception cleanupFailure) {
            failure = failure is null ? cleanupFailure : new AggregateException(failure, cleanupFailure);
        }
        if (failure is not null) {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private Task ReleaseSourceAsync() {
        lock (releaseSync) {
            return sourceRelease ??= ReleaseHeldSourceAsync();
        }
    }

    private async Task ReleaseHeldSourceAsync() {
        if (source is not null) {
            await source.DisposeAsync();
        }
    }
}
