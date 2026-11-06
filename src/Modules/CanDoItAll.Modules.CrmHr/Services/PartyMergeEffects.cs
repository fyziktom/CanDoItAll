using System.Linq.Expressions;
using CanDoItAll.Editing;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CanDoItAll.Modules.CrmHr;

internal sealed class PartyMergeEffects {
    private readonly HashSet<Guid> profiles = [];
    private readonly HashSet<Guid> accounts = [];
    private readonly HashSet<Guid> workforce = [];
    private readonly HashSet<Guid> applications = [];
    private readonly HashSet<Guid> interviews = [];
    private readonly HashSet<Guid> tasks = [];
    private readonly HashSet<(Guid Id, Guid? Project)> staffing = [];
    private readonly HashSet<(Guid Id, Guid Project)> assignments = [];

    internal void Capture(CrmHrDbContext context) {
        profiles.UnionWith(Owners<WorkforceProfile>(context, row => row.PartyId));
        profiles.UnionWith(Owners<AiAgentProfile>(context, row => row.PartyId));
        profiles.UnionWith(Owners<CrmAccountProfile>(context, row => row.AccountPartyId));
        accounts.UnionWith(Owners<CrmAccountConnection>(context, row => row.AccountPartyId));
        accounts.UnionWith(Owners<InteractionPartyLink>(context, row => row.PartyId));
        workforce.UnionWith(Owners<PartySkill>(context, row => row.PartyId));
        workforce.UnionWith(Owners<CapacityBlock>(context, row => row.PartyId));
        applications.UnionWith(Changed<RecruitmentApplication>(context).Select(entry => entry.Entity.Id));
        interviews.UnionWith(Changed<RecruitmentInterview>(context).Select(entry => entry.Entity.ApplicationId));
        tasks.UnionWith(Owners<OnboardingTask>(context, row => row.PartyId));
        staffing.UnionWith(Changed<StaffingRequest>(context).Select(entry => (entry.Entity.Id, entry.Entity.ProjectId)));
        assignments.UnionWith(Changed<ProjectPartyAssignment>(context).Select(entry => (entry.Entity.Id, entry.Entity.ProjectId)));
    }

    internal async Task AdvanceVersionsAsync(CrmHrDbContext context, DateTimeOffset now, CancellationToken cancellationToken) {
        foreach (var entry in Changed<RecruitmentApplication>(context)) {
            entry.Entity.UpdatedAtUtc = CrmHrEditVersions.Next(entry.Entity.UpdatedAtUtc, now);
        }
        var interactionIds = Changed<InteractionRecord>(context).Select(entry => entry.Entity.Id)
            .Concat(Changed<InteractionPartyLink>(context).Select(entry => entry.Entity.InteractionId)).Distinct().ToArray();
        var interactions = await context.Set<InteractionRecord>().Where(row => interactionIds.Contains(row.Id)).ToListAsync(cancellationToken);
        foreach (var interaction in interactions) {
            interaction.UpdatedAtUtc = CrmHrEditVersions.Next(interaction.UpdatedAtUtc, now);
        }
        var parties = await context.Set<InteractionPartyLink>().AsNoTracking().Where(row => interactionIds.Contains(row.InteractionId))
            .Select(row => row.PartyId).ToArrayAsync(cancellationToken);
        accounts.UnionWith(parties);
        Capture(context);
    }

    internal void Publish(CrmHrChanges changes, ProjectAssignmentChanges projectChanges, Guid retainedId, Guid sourceId,
        IReadOnlyList<ProjectWorkAssignmentPartyMergeChange> workChanges) {
        foreach (var id in profiles.Where(id => id != retainedId && id != sourceId)) {
            changes.PublishParty(id);
        }
        foreach (var id in accounts) {
            changes.PublishAccountRecords(id, id == sourceId ? EditingChangeKind.Deleted : EditingChangeKind.EditableContentChanged);
        }
        foreach (var id in workforce) {
            changes.PublishWorkforceRecords(id, id == sourceId ? EditingChangeKind.Deleted : EditingChangeKind.EditableContentChanged);
        }
        foreach (var id in applications) {
            changes.PublishRecruitment(id);
        }
        foreach (var id in interviews) {
            changes.PublishRecruitmentRecords(id);
        }
        foreach (var id in tasks) {
            changes.PublishRecruitmentTasks(id);
        }
        foreach (var (id, project) in staffing) {
            changes.PublishStaffing(id, project);
        }
        foreach (var (id, project) in assignments.Concat(workChanges.Select(row => (row.AssignmentId, row.ProjectId))).Distinct()) {
            projectChanges.Publish(id, project);
        }
    }

    private static IEnumerable<EntityEntry<T>> Changed<T>(CrmHrDbContext context) where T : class =>
        context.ChangeTracker.Entries<T>().Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

    private static IEnumerable<Guid> Owners<T>(CrmHrDbContext context, Expression<Func<T, Guid>> owner) where T : class =>
        Changed<T>(context).SelectMany(entry => new[] { entry.Property(owner).OriginalValue, entry.Property(owner).CurrentValue });
}
