using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectAgentMutationDomain { NonTaskStructure, Tasks, ProjectCreation, SubprojectCreation, Hierarchy }

public sealed class ProjectAgentMutationAdmission {
    internal ProjectAgentMutationAdmission(Guid agentId, Guid databaseProfileId, AgentProjectStructureAccessSettings access,
        AgentExecutionGovernanceSnapshot? governance, ProjectAgentMutationDomain domain, string? operation) {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(databaseProfileId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(access);
        if (!Enum.IsDefined(domain)) {
            throw new ArgumentOutOfRangeException(nameof(domain));
        }
        AgentId = agentId;
        DatabaseProfileId = databaseProfileId;
        Domain = domain;
        CanRead = access.CanRead;
        CanCreateProjects = access.CanCreateProjects;
        CanCreateSubprojects = access.CanCreateSubprojects;
        CanWriteTasks = access.CanWrite || access.CanWriteTasks;
        CanWriteStructure = access.CanWrite || access.CanWriteNonTaskStructure;
        AllowAllProjects = access.AllowAllProjects;
        ProjectIds = access.AllowedProjectIds.ToImmutableHashSet();
        ProjectLifetimes = access.AllowedProjectLifetimes.ToImmutableHashSet();
        Governance = governance;
        Operation = operation;
    }

    internal Guid AgentId { get; }
    internal Guid DatabaseProfileId { get; }
    internal ProjectAgentMutationDomain Domain { get; }
    internal bool CanRead { get; }
    internal bool CanCreateProjects { get; }
    internal bool CanCreateSubprojects { get; }
    internal ProjectCreationReservation? CreatedProjectReservation { get; private init; }
    internal bool CanWriteTasks { get; }
    internal bool CanWriteStructure { get; }
    internal bool AllowAllProjects { get; }
    internal ImmutableHashSet<Guid> ProjectIds { get; }
    internal ImmutableHashSet<AgentProjectStructureLifetime> ProjectLifetimes { get; }
    internal AgentExecutionGovernanceSnapshot? Governance { get; }
    internal string? Operation { get; }

    internal ProjectAgentMutationAdmission WithCreatedProject(ProjectCreationReservation reservation) {
        ArgumentNullException.ThrowIfNull(reservation);
        if (reservation.DatabaseProfileId != DatabaseProfileId || reservation.RequesterId != AgentId) {
            throw new InvalidOperationException("The created project reservation belongs to another original source.");
        }
        return new(AgentId, DatabaseProfileId, new() {
            CanRead = CanRead, CanWriteTasks = CanWriteTasks, CanWriteNonTaskStructure = CanWriteStructure,
            CanCreateProjects = CanCreateProjects, CanCreateSubprojects = CanCreateSubprojects, AllowAllProjects = AllowAllProjects,
            AllowedProjectIds = ProjectIds.ToList(), AllowedProjectLifetimes = ProjectLifetimes.ToList()
        }, Governance, Domain, Operation) { CreatedProjectReservation = reservation };
    }
}

public sealed class ProjectAgentNativeMutationService(ICanonicalRuntimeDatabase database, IAgentCatalogReadLeaseStore catalog,
    IAgentExecutionProfileGenerationSource generations, ProjectWriteAdmissionService projects,
    CoordinatedDatabaseTransaction transactions) {
    private readonly ProjectAgentSourceMutationAuthority source = new(database, catalog, generations);

    internal async Task RequirePreparationAsync(ProjectAgentMutationAdmission admission, ProjectWriteAdmission expected,
        CancellationToken cancellationToken) {
        await using var held = await source.AcquireAsync(admission, new(ProjectMutationPurpose.ExistingWrite, expected.ProjectId, [expected]), cancellationToken);
        await projects.RequireCurrentAsync(expected, cancellationToken);
    }

    internal async Task<ProjectAgentNativeMutationScope> BeginAsync(WorkbenchDbContext context, IReadOnlyCollection<string> keys,
        ProjectAgentMutationAdmission admission, IReadOnlyCollection<ProjectWriteAdmission>? expectedAdmissions,
        CancellationToken cancellationToken) {
        if (expectedAdmissions is not { Count: > 0 }) {
            throw Denied("An ordinary Agent mutation requires its captured project lifetime before entering the native owner.");
        }
        var expected = expectedAdmissions.ToImmutableArray();
        if (context.Database.CurrentTransaction is not null) {
            throw new InvalidOperationException("Agent catalog authority must be acquired before the native transaction starts.");
        }
        var held = await source.AcquireAsync(admission, new(ProjectMutationPurpose.ExistingWrite, expected[0].ProjectId, expected, admission.CreatedProjectReservation), cancellationToken);
        SerializableMutationScope? mutation = null;
        try {
            mutation = await SerializableMutationScope.BeginAsync(context, keys, cancellationToken);
            using (transactions.Enter(context)) {
                await projects.RequireManyForMutationAsync(expected, cancellationToken);
                if (admission.CreatedProjectReservation is { } reservation) {
                    await projects.RequireConsumedCreationForMutationAsync(reservation, cancellationToken);
                }
            }
            var result = new ProjectAgentNativeMutationScope(context, mutation, held, admission, expected, this, transactions);
            result.ObserveNativeChanges();
            return result;
        } catch {
            try {
                if (mutation is not null) {
                    await mutation.DisposeAsync();
                }
            } finally {
                await held.DisposeAsync();
            }
            throw;
        }
    }

    internal async Task RequireCommitAsync(ProjectAgentMutationAdmission admission, ImmutableArray<ProjectWriteAdmission> expected,
        ProjectAgentSourceMutationLease held, CancellationToken cancellationToken) {
        held.RequireCurrent();
        await projects.RequireManyForMutationAsync(expected, cancellationToken);
        if (admission.CreatedProjectReservation is { } reservation) {
            await projects.RequireConsumedCreationForMutationAsync(reservation, cancellationToken);
        }
    }

    private static ProjectStructureAgentException Denied(string message) => new(403, "ProjectMutationAuthorityDenied", message);

}

internal sealed class ProjectAgentNativeMutationScope(WorkbenchDbContext context, SerializableMutationScope mutation,
    ProjectAgentSourceMutationLease held, ProjectAgentMutationAdmission admission, ImmutableArray<ProjectWriteAdmission> expected,
    ProjectAgentNativeMutationService authority, CoordinatedDatabaseTransaction transactions) : IAsyncDisposable {
    private int disposed;
    private bool committed;

    internal bool RequiresTaskTargetGuard => held.RequiresTaskTargetGuard;

    internal void ObserveNativeChanges() => context.SavingChanges += RequireNativeChangeKinds;

    internal void RequireNodeAuthority(IEnumerable<ProjectObjectRecord> nodes) {
        if (RequiresTaskTargetGuard && nodes.Any(node => ProjectStructureNonTaskWritePolicy.IsTask(node.ObjectType, node.ObjectSubtype))) {
            throw new ProjectStructureAgentException(403, "ProjectTaskWriteDenied", "The original and current Agent grants must both permit changes to task nodes.");
        }
    }

    private void RequireNativeChangeKinds(object? sender, SavingChangesEventArgs eventArgs) {
        if (!RequiresTaskTargetGuard) {
            return;
        }
        context.ChangeTracker.DetectChanges();
        foreach (var entry in context.ChangeTracker.Entries<ProjectObjectRecord>().Where(entry =>
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)) {
            if (ProjectStructureNonTaskWritePolicy.IsTask(entry.Entity.ObjectType, entry.Entity.ObjectSubtype) ||
                    entry.State != EntityState.Added && ProjectStructureNonTaskWritePolicy.IsTask(
                        entry.OriginalValues.GetValue<ProjectObjectType>(nameof(ProjectObjectRecord.ObjectType)),
                        entry.OriginalValues.GetValue<string>(nameof(ProjectObjectRecord.ObjectSubtype)))) {
                throw new ProjectStructureAgentException(403, "ProjectTaskWriteDenied", "The original and current Agent grants must both permit changes to task nodes.");
            }
        }
    }

    internal async Task CommitAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (committed) {
            throw new InvalidOperationException("The Agent native mutation has already committed.");
        }
        using (transactions.Enter(context)) {
            await authority.RequireCommitAsync(admission, expected, held, cancellationToken);
            await mutation.CommitAsync(cancellationToken);
            committed = true;
        }
        context.SavingChanges -= RequireNativeChangeKinds;
        await held.DisposeAsync();
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref disposed, 1) != 0) {
            return;
        }
        try {
            context.SavingChanges -= RequireNativeChangeKinds;
            await mutation.DisposeAsync();
        } finally {
            await held.DisposeAsync();
        }
    }
}
