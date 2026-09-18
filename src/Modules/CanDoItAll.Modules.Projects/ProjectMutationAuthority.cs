using System.Collections.Immutable;
using System.Data;
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
    ProjectWriteAdmissionService admissions, CoordinatedDatabaseTransaction transactions) : IAsyncDisposable {
    private bool committed;
    private int disposed;

    internal static async Task<ProjectSourceMutationScope> BeginAsync(ProjectsDbContext context,
        IReadOnlyCollection<string> keys, ProjectMutationPurpose purpose, Guid projectId,
        IReadOnlyCollection<Guid> existingProjectIds, ProjectMutationAuthorization? authorization,
        ProjectWriteAdmissionService admissions, CoordinatedDatabaseTransaction transactions,
        CancellationToken cancellationToken, ProjectCreationReservation? reservation = null, Guid? requesterId = null) {
        IProjectMutationSourceLease? source = null;
        SerializableMutationScope? mutation = null;
        IDbContextTransaction? transaction = null;
        var expected = authorization?.ExpectedProjects ?? [];
        if (authorization is not null && !existingProjectIds.ToHashSet().SetEquals(expected.Select(project => project.ProjectId))) {
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
            if (source is not null && context.Database.IsNpgsql()) {
                transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                using var coordination = transactions.Enter(context);
                await source.RequireForMutationAsync(cancellationToken);
                await SerializableMutationScope.AcquireRelationalScopeLocksAsync(context, orderedKeys, cancellationToken);
            } else {
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
            return new(context, mutation, transaction, source, required, admissions, transactions);
        } catch {
            try {
                if (transaction is not null) {
                    await transaction.DisposeAsync();
                }
                if (mutation is not null) {
                    await mutation.DisposeAsync();
                }
            } finally {
                if (source is not null) {
                    await source.DisposeAsync();
                }
            }
            throw;
        }
    }

    internal async Task CommitAsync(CancellationToken cancellationToken) {
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
        if (transaction is not null) {
            await transaction.CommitAsync(cancellationToken);
        } else {
            await mutation!.CommitAsync(cancellationToken);
        }
        committed = true;
        if (source is not null) {
            await source.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref disposed, 1) != 0) {
            return;
        }
        try {
            if (transaction is not null) {
                await transaction.DisposeAsync();
            }
            if (mutation is not null) {
                await mutation.DisposeAsync();
            }
        } finally {
            if (source is not null) {
                await source.DisposeAsync();
            }
        }
    }
}
