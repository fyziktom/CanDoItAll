using System.Text;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectLifecycleEffects(IEnumerable<IProjectDeletionParticipant> registrations,
    CoordinatedDatabaseTransaction transactions, SearchIndexService search, StorageCatalogService storage, IClock clock) {
    internal IReadOnlyList<IProjectDeletionParticipant> CaptureParticipants() {
        var participants = registrations.Take(ProjectDeletionCompletionBinding.MaximumParticipants + 1).ToArray();
        if (participants.Length > ProjectDeletionCompletionBinding.MaximumParticipants ||
                participants.Any(participant => participant is null) ||
                participants.Select(participant => participant.Id).Distinct().Count() != participants.Length) {
            throw new InvalidOperationException("Project deletion requires a bounded registration with distinct participants.");
        }
        var utf8 = new UTF8Encoding(false, true);
        foreach (var participant in participants) {
            var id = participant.Id.Value;
            if (string.IsNullOrWhiteSpace(id) || id != id.Trim() ||
                    utf8.GetByteCount(id) > ProjectDeletionObligation.MaximumParticipantIdBytes || participant.RecoveryProtocolVersion <= 0 ||
                    participant.PreparationScopeKeys.Any(key => string.IsNullOrWhiteSpace(key.Value))) {
                throw new InvalidOperationException("Project deletion participant registration contains an invalid identity or exclusion key.");
            }
        }
        return participants.OrderBy(participant => participant.Id.Value, StringComparer.Ordinal).ToArray();
    }

    internal static async Task<Project> RequireDeletionTargetAsync(ProjectsDbContext context,
        ProjectDefinitionExpectation expected, CancellationToken cancellationToken) {
        var project = await context.Set<Project>().SingleOrDefaultAsync(item => item.Id == expected.Project.ProjectId, cancellationToken);
        if (project is null || project.LifetimeId != expected.Project.LifetimeId || project.ConcurrencyToken != expected.Revision) {
            throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.TargetChanged);
        }
        return project;
    }

    internal async Task<ProjectDeletionCompletionBinding> PrepareDeletionAsync(ProjectsDbContext context,
        ProjectDeletionPreparationContext original, IReadOnlyList<IProjectDeletionParticipant> participants,
        CancellationToken cancellationToken) {
        if (await context.Set<ProjectCreationReservationRecord>().AnyAsync(record =>
                (record.ProjectId == original.Project.ProjectId || record.ParentProjectId == original.Project.ProjectId) &&
                record.State == ProjectCreationReservationState.Reserved && record.ImportedHistory == null, cancellationToken)) {
            throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.ActiveCreationReservation);
        }
        return await PrepareParticipantsAsync(context, original, participants,
            participant => participant.PrepareAsync(original, cancellationToken));
    }

    internal Task<ProjectDeletionCompletionBinding> PrepareReservedCancellationAsync(ProjectsDbContext context,
        ProjectReservationCancellationPreparationContext original, IReadOnlyList<IProjectDeletionParticipant> participants,
        CancellationToken cancellationToken)
        => PrepareParticipantsAsync(context, original.Completion, participants,
            participant => participant.PrepareAsync(original, cancellationToken));

    private async Task<ProjectDeletionCompletionBinding> PrepareParticipantsAsync(ProjectsDbContext context,
        ProjectDeletionPreparationContext original, IReadOnlyList<IProjectDeletionParticipant> participants,
        Func<IProjectDeletionParticipant, Task<ProjectDeletionParticipantPreparation?>> prepare) {
        var obligations = new List<ProjectDeletionObligation>(participants.Count);
        using (transactions.Enter(context)) {
            foreach (var participant in participants) {
                var prepared = await prepare(participant);
                if (prepared is null) {
                    continue;
                }
                if (prepared.ProjectId != original.Project.ProjectId) {
                    throw new InvalidOperationException("A deletion participant prepared another original Project.");
                }
                obligations.Add(new(participant.Id, prepared.RecoveryId));
            }
        }
        return new(original, obligations);
    }

    internal async Task DeleteAsync(ProjectsDbContext context, Project project, CancellationToken cancellationToken) {
        using (transactions.Enter(context)) {
            await context.Set<ProjectPhase>().Where(item => item.ProjectId == project.Id).ExecuteDeleteAsync(cancellationToken);
            await context.Set<ProjectOptionSelection>().Where(item => item.ProjectId == project.Id).ExecuteDeleteAsync(cancellationToken);
            await context.Set<ProjectHierarchyLink>().Where(item => item.ParentProjectId == project.Id || item.ChildProjectId == project.Id)
                .ExecuteDeleteAsync(cancellationToken);
            context.Add(new ProjectRetirementRecord {
                ProjectId = project.Id,
                LifetimeId = project.LifetimeId,
                RetiredAtUtc = clock.GetUtcNow()
            });
            context.Remove(project);
            await search.DeleteProjectSearchForMutationAsync(project.Id, cancellationToken);
            await storage.DeleteProjectRoutingForMutationAsync(project.Id, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    internal async Task<ProjectLifecycleAcknowledgement> ApplyHierarchyAsync(ProjectsDbContext context,
        ProjectLifecycleCommand command, CancellationToken cancellationToken) {
        switch (command) {
            case ProjectLifecycleCommand.EnsureLink ensure: {
                var (edge, created) = await EnsureAsync(context, ensure.Parent.ProjectId, ensure.Child.ProjectId, cancellationToken);
                return new ProjectLifecycleAcknowledgement.LinkEnsured(edge.Id, created);
            }
            case ProjectLifecycleCommand.RemoveLink remove: {
                context.Remove(await RequireEdgeAsync(context, remove.Expected, cancellationToken));
                return new ProjectLifecycleAcknowledgement.LinkRemoved();
            }
            case ProjectLifecycleCommand.ReconnectLink reconnect: {
                var original = await RequireEdgeAsync(context, reconnect.Expected, cancellationToken);
                var (destination, created) = await EnsureAsync(context, reconnect.NewParent.ProjectId,
                    reconnect.Expected.Child.ProjectId, cancellationToken);
                context.Remove(original);
                return new ProjectLifecycleAcknowledgement.LinkReconnected(destination.Id, created);
            }
            default:
                throw new NotSupportedException("The lifecycle command is not a supported hierarchy mutation.");
        }
    }

    private static async Task<ProjectHierarchyLink> RequireEdgeAsync(ProjectsDbContext context,
        ProjectHierarchyEdgeExpectation expected, CancellationToken cancellationToken) {
        var edge = await context.Set<ProjectHierarchyLink>().SingleOrDefaultAsync(item => item.Id == expected.EdgeId, cancellationToken);
        if (edge is null || edge.ParentProjectId != expected.Parent.ProjectId || edge.ChildProjectId != expected.Child.ProjectId) {
            throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.EdgeChanged);
        }
        return edge;
    }

    private async Task<(ProjectHierarchyLink Edge, bool Created)> EnsureAsync(ProjectsDbContext context,
        Guid parent, Guid child, CancellationToken cancellationToken) {
        var existing = await context.Set<ProjectHierarchyLink>().SingleOrDefaultAsync(
            item => item.ParentProjectId == parent && item.ChildProjectId == child, cancellationToken);
        if (existing is not null) {
            return (existing, false);
        }
        var cycle = await context.Database.SqlQuery<bool>($"""
            WITH RECURSIVE reachable("Id") AS (
                SELECT {child}::uuid
                UNION
                SELECT link."ChildProjectId" FROM "Projects_ProjectHierarchyLinks" AS link
                JOIN reachable AS parent ON link."ParentProjectId" = parent."Id"
            )
            SELECT EXISTS (SELECT 1 FROM reachable WHERE "Id" = {parent}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (cycle) {
            throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.Cycle);
        }
        var edge = new ProjectHierarchyLink { ParentProjectId = parent, ChildProjectId = child, CreatedAtUtc = clock.GetUtcNow() };
        context.Add(edge);
        return (edge, true);
    }
}
