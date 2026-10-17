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
    Guid? RequesterId = null);

public interface IProjectMutationSourceAuthority {
    Task<IProjectMutationSourceLease> AcquireAsync(ProjectMutationTarget target, CancellationToken cancellationToken = default);
}

public interface IProjectMutationSourceLease : IAsyncDisposable {
    IReadOnlyCollection<ProjectWriteAdmission> SourceProjects { get; }
    Task RequireForMutationAsync(CancellationToken cancellationToken = default);
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
    Action? requireCurrentOwner = null) : IAsyncDisposable {
    private bool committed;
    private bool commitAttempted;
    private int disposed;
    private int sourceReleased;

    internal bool IsCommitted => committed;
    internal bool CommitAttempted => commitAttempted;

    internal static async Task<ProjectSourceMutationScope> BeginDeletionAsync(ProjectsDbContext context,
        IReadOnlyCollection<string> keys, ProjectMutationPurpose purpose, Guid projectId,
        IReadOnlyCollection<Guid> existingProjectIds, ProjectMutationAuthorization? authorization,
        ProjectWriteAdmissionService admissions, CoordinatedDatabaseTransaction transactions,
        DatasetActivationBinding binding, Action requireCurrentOwner, CancellationToken cancellationToken,
        ProjectCreationReservation? reservation = null) {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(requireCurrentOwner);
        using var admission = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        admission.CancelAfter(TimeSpan.FromSeconds(5));
        return await BeginCoreAsync(context, keys, purpose, projectId, existingProjectIds, authorization,
            admissions, transactions, admission.Token, reservation, null, null, definitionWrite: false,
            deletionBinding: binding, requireCurrentOwner: requireCurrentOwner);
    }

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
        DatasetActivationBinding? deletionBinding = null, Action? requireCurrentOwner = null) {
        IProjectMutationSourceLease? source = null;
        SerializableMutationScope? mutation = null;
        IDbContextTransaction? transaction = null;
        var expected = expectedProjectAdmissions?.ToImmutableArray() ?? authorization?.ExpectedProjects ?? [];
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
                source = await authorization.SourceAuthority.AcquireAsync(new(purpose, projectId, expected, reservation, requesterId), cancellationToken)
                    ?? throw new InvalidOperationException("The explicit project source policy did not return a held authority lease.");
            }
            var required = expected.Concat(source?.SourceProjects ?? []).Distinct().ToImmutableArray();
            if (required.GroupBy(project => project.ProjectId).Any(group => group.Count() > 1)) {
                throw new InvalidOperationException("The owner target and original source identify different lifetimes of the same project.");
            }
            var orderedKeys = keys.Concat(required.Select(project => ProjectMutationScopeKeys.ForProject(project.ProjectId)))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (deletionBinding is not null) {
                transactions.RequireOwnerProfile(context);
                transaction = await ProjectDeletionTransactionEntry.BeginAsync(context, deletionBinding, orderedKeys, cancellationToken);
                requireCurrentOwner!();
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
            return new(context, mutation, transaction, source, required, admissions, transactions, requireCurrentOwner);
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
            throw;
        }
    }

    internal Task CommitAsync(CancellationToken cancellationToken) => CommitCoreAsync(null, cancellationToken);

    internal Task CommitDefinitionAsync(ProjectDefinitionOperationAttempt attempt, CancellationToken cancellationToken)
        => CommitCoreAsync(attempt, cancellationToken);

    private async Task CommitCoreAsync(ProjectDefinitionOperationAttempt? attempt, CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (committed) {
            throw new InvalidOperationException("The project source mutation already committed.");
        }
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

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref disposed, 1) != 0) {
            return;
        }
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

    private async Task ReleaseSourceAsync() {
        if (source is null || Interlocked.Exchange(ref sourceReleased, 1) != 0) {
            return;
        }
        await source.DisposeAsync();
    }
}
