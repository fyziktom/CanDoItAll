using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkAssignmentService(
    IDbContextFactory<WorkbenchDbContext> factory,
    DbContextOptions<WorkbenchDbContext> options,
    CoordinatedDatabaseTransaction transactions,
    ProjectRecordQueryService projects,
    IProjectWorkAssignmentPartyFacts parties,
    IProjectWorkAssignmentQueries queries,
    IProjectWorkItemAssignmentMutationBridge revisions) : IProjectWorkAssignmentCommands {
    public Task<IReadOnlyList<ProjectWorkAssignmentFact>> ListForProjectsAsync(
        IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default) => queries.ListForProjectsAsync(projectIds, cancellationToken);

    public Task<IReadOnlyList<ProjectWorkAssignmentFact>> ListForPartiesAsync(
        IReadOnlyCollection<Guid> partyIds, CancellationToken cancellationToken = default) => queries.ListForPartiesAsync(partyIds, cancellationToken);

    public async Task<IReadOnlyList<ProjectWorkAssignmentFact>> ListForProjectsForMutationAsync(
        IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(projectIds);
        await using var context = await EnlistAsync(cancellationToken);
        return (await context.Set<ProjectWorkAssignmentRecord>().AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId)).ToListAsync(cancellationToken))
            .Select(item => item.ToFact()).ToArray();
    }

    public Task<ProjectWorkAssignmentFact?> GetAsync(Guid assignmentId, CancellationToken cancellationToken = default) =>
        queries.GetAsync(assignmentId, cancellationToken);

    public async Task<ProjectWorkAssignmentFact?> GetForMutationAsync(Guid assignmentId, CancellationToken cancellationToken = default) {
        await using var context = await EnlistAsync(cancellationToken);
        return (await context.Set<ProjectWorkAssignmentRecord>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken))?.ToFact();
    }

    public async Task<IReadOnlyList<Guid>> ListPartyMergeProjectsAsync(Guid retainedPartyId, Guid mergedPartyId,
        IReadOnlyCollection<Guid> affiliationIds, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(affiliationIds);
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<ProjectWorkAssignmentRecord>().AsNoTracking().Where(item => item.PartyId == retainedPartyId ||
            item.PartyId == mergedPartyId || (item.PartyOrganizationAffiliationId.HasValue &&
            affiliationIds.Contains(item.PartyOrganizationAffiliationId.Value))).Select(item => item.ProjectId).Distinct().ToListAsync(cancellationToken);
    }

    public async Task<Result<Guid>> SaveAsync(ProjectPartyAssignmentUpsertRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        var newAssignmentId = Guid.NewGuid();
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var scope = await SerializableMutationScope.BeginAsync(context,
            MutationKeys(request.ProjectId, request.AssignmentId.HasValue ? [request.AssignmentId.Value, newAssignmentId] : [newAssignmentId]), cancellationToken);
        using var entry = transactions.Enter(context);
        var result = await SaveCoreAsync(context, request, newAssignmentId, null, cancellationToken);
        if (result.IsSuccess) {
            await scope.CommitAsync(cancellationToken);
        }
        return result;
    }

    public async Task<Result<Guid>> StageSaveAsync(ProjectPartyAssignmentUpsertRequest request, Guid newAssignmentId,
        ProjectWorkAssignmentCarryOver? carryOver = null, CancellationToken cancellationToken = default) {
        await using var context = await EnlistAsync(cancellationToken);
        return await SaveCoreAsync(context, request, newAssignmentId, carryOver, cancellationToken);
    }

    private async Task<Result<Guid>> SaveCoreAsync(WorkbenchDbContext context,
        ProjectPartyAssignmentUpsertRequest request, Guid newAssignmentId, ProjectWorkAssignmentCarryOver? carryOver, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        var nodeKey = request.NodeKey?.Trim() ?? string.Empty;
        var error = await ValidateAsync(context, request.ProjectId, nodeKey, [request], cancellationToken);
        if (error is not null) {
            return Result<Guid>.Failure(error);
        }
        var entity = request.AssignmentId.HasValue
            ? await context.Set<ProjectWorkAssignmentRecord>().SingleOrDefaultAsync(item => item.Id == request.AssignmentId.Value, cancellationToken)
            : null;
        if (entity is null && carryOver is null) {
            entity = await context.Set<ProjectWorkAssignmentRecord>().SingleOrDefaultAsync(item =>
                item.ProjectId == request.ProjectId && item.PartyId == request.PartyId && item.NodeKey == nodeKey, cancellationToken);
        }
        if (entity is not null && entity.ProjectId != request.ProjectId) {
            return Result<Guid>.Failure(Validation("The assignment does not belong to the requested project.", "project-mismatch"));
        }
        if (request.AssignmentId is { } requestedId &&
            await parties.FindParticipationProjectForMutationAsync(requestedId, cancellationToken) is not null) {
            return Result<Guid>.Failure(Validation("An existing participation assignment requires a coordinated ownership transition.", "ownership-transition-required"));
        }
        if (carryOver is not null && (entity is not null || request.AssignmentId != carryOver.Id)) {
            throw new InvalidOperationException("An assignment ownership transition must preserve one unoccupied assignment identity.");
        }
        var affectedNodes = new HashSet<string>(StringComparer.Ordinal) { nodeKey };
        if (entity is not null) {
            affectedNodes.Add(entity.NodeKey);
        }
        await context.Set<ProjectWorkAssignmentRecord>().Where(item => item.ProjectId == request.ProjectId &&
            affectedNodes.Contains(item.NodeKey)).LoadAsync(cancellationToken);
        if (entity is null) {
            if (newAssignmentId == Guid.Empty || await parties.FindParticipationProjectForMutationAsync(newAssignmentId, cancellationToken) is not null) {
                return Result<Guid>.Failure(Validation("An assignment identity is already in use or is empty.", "identity-in-use"));
            }
            entity = new ProjectWorkAssignmentRecord { Id = newAssignmentId };
            if (carryOver is not null) {
                entity.Id = carryOver.Id;
                entity.PhaseName = carryOver.PhaseName;
                entity.OpportunityId = carryOver.OpportunityId;
            }
            context.Add(entity);
        }
        Apply(entity, request, request.ProjectId, nodeKey, request.IsPrimary);
        if (entity.IsPrimary) {
            foreach (var other in Current(context, request.ProjectId, nodeKey).Where(item => item.Id != entity.Id)) {
                other.IsPrimary = false;
            }
        }
        foreach (var affectedNode in affectedNodes) {
            await StageRevisionAsync(context, request.ProjectId, affectedNode, null, null, cancellationToken);
        }
        await context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task<Result> ReplaceAsync(Guid projectId, ProjectNodeReference node,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>? expectedAssignments = null,
        ProjectWorkItemDirectAssignmentRevision? expectedRevision = null, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(desiredAssignments);
        var newIds = desiredAssignments.Select(_ => Guid.NewGuid()).ToArray();
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var scope = await SerializableMutationScope.BeginAsync(context,
            MutationKeys(projectId, desiredAssignments.Where(item => item.AssignmentId.HasValue).Select(item => item.AssignmentId!.Value).Concat(newIds)), cancellationToken);
        using var entry = transactions.Enter(context);
        var result = await ReplaceCoreAsync(context, projectId, node, desiredAssignments, newIds, expectedAssignments, expectedRevision, cancellationToken);
        if (result.IsSuccess) {
            await scope.CommitAsync(cancellationToken);
        }
        return result;
    }

    public async Task<Result> StageReplaceAsync(Guid projectId, ProjectNodeReference node,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<Guid> newAssignmentIds,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>? expectedAssignments = null,
        ProjectWorkItemDirectAssignmentRevision? expectedRevision = null, CancellationToken cancellationToken = default) {
        await using var context = await EnlistAsync(cancellationToken);
        return await ReplaceCoreAsync(context, projectId, node, desiredAssignments, newAssignmentIds, expectedAssignments, expectedRevision, cancellationToken);
    }

    private async Task<Result> ReplaceCoreAsync(WorkbenchDbContext context, Guid projectId, ProjectNodeReference node,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<Guid> newAssignmentIds,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>? expectedAssignments,
        ProjectWorkItemDirectAssignmentRevision? expectedRevision, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(desiredAssignments);
        if (newAssignmentIds.Count != desiredAssignments.Count || newAssignmentIds.Any(id => id == Guid.Empty) ||
            newAssignmentIds.Distinct().Count() != newAssignmentIds.Count) {
            throw new ArgumentException("Every replacement requires its own preallocated assignment identity.", nameof(newAssignmentIds));
        }
        if (desiredAssignments.GroupBy(item => item.PartyId).Any(group => group.Count() > 1)) {
            return Result.Failure(Validation("Desired assignments contain the same party and role more than once.", "duplicate"));
        }
        var error = await ValidateAsync(context, projectId, node.NodeKey, desiredAssignments, cancellationToken);
        if (error is not null) {
            return Result.Failure(error);
        }
        var existing = await context.Set<ProjectWorkAssignmentRecord>().Where(item =>
            item.ProjectId == projectId && item.NodeKey == node.NodeKey).ToListAsync(cancellationToken);
        if (expectedAssignments is not null) {
            var facts = await parties.ReadForMutationAsync(existing.Select(item => item.PartyId).Distinct().ToArray(), cancellationToken);
            var actual = existing.Where(item => facts.ContainsKey(item.PartyId)).Select(item =>
                new ProjectPartyAssignmentConcurrencySnapshot(item.Id, item.PartyId, facts[item.PartyId].PartyType,
                    item.IsPrimary, item.PartyOrganizationAffiliationId)).ToHashSet();
            if (actual.Count != existing.Count || !actual.SetEquals(expectedAssignments)) {
                return Stale();
            }
        }
        var replacements = new List<ProjectWorkAssignmentRecord>();
        var hasExplicitPrimary = desiredAssignments.Any(item => item.IsPrimary);
        var emittedPrimary = false;
        foreach (var (request, index) in desiredAssignments.Select((request, index) => (request, index))) {
            var id = request.AssignmentId.HasValue && existing.All(item => item.Id != request.AssignmentId.Value)
                ? request.AssignmentId.Value : newAssignmentIds[index];
            if (await parties.FindParticipationProjectForMutationAsync(id, cancellationToken) is not null ||
                replacements.Any(item => item.Id == id) ||
                await context.Set<ProjectWorkAssignmentRecord>().AnyAsync(item => item.Id == id, cancellationToken)) {
                return Result.Failure(Validation("An assignment identity is already in use.", "identity-in-use"));
            }
            var primary = !emittedPrimary && (request.IsPrimary || !hasExplicitPrimary);
            emittedPrimary |= primary;
            var replacement = new ProjectWorkAssignmentRecord { Id = id };
            Apply(replacement, request, projectId, node.NodeKey, primary);
            replacements.Add(replacement);
        }
        context.RemoveRange(existing);
        context.AddRange(replacements);
        var revision = await StageRevisionAsync(context, projectId, node.NodeKey, expectedRevision, null, cancellationToken);
        if (revision.Status != ProjectWorkItemDirectAssignmentMutationStatus.Applied) {
            return Stale();
        }
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task DeleteAsync(Guid assignmentId, CancellationToken cancellationToken = default) {
        var original = await GetAsync(assignmentId, cancellationToken);
        if (original is null) {
            return;
        }
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var scope = await SerializableMutationScope.BeginAsync(context,
            MutationKeys(original.ProjectId, [assignmentId]), cancellationToken);
        using var entry = transactions.Enter(context);
        var current = await GetForMutationAsync(assignmentId, cancellationToken);
        if (current is not null && current.ProjectId != original.ProjectId) {
            throw new InvalidOperationException("The assignment project changed while it was being deleted.");
        }
        await StageDeleteAsync(assignmentId, cancellationToken);
        await scope.CommitAsync(cancellationToken);
    }

    public async Task StageDeleteAsync(Guid assignmentId, CancellationToken cancellationToken = default) {
        await using var context = await EnlistAsync(cancellationToken);
        var entity = await context.Set<ProjectWorkAssignmentRecord>().SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
        if (entity is null) {
            return;
        }
        await context.Set<ProjectWorkAssignmentRecord>().Where(item => item.ProjectId == entity.ProjectId &&
            item.NodeKey == entity.NodeKey).LoadAsync(cancellationToken);
        context.Remove(entity);
        await StageRevisionAsync(context, entity.ProjectId, entity.NodeKey, null, null, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task StageDeleteForNodesAsync(Guid projectId, IReadOnlyCollection<ProjectNodeReference> nodes,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(nodes);
        await using var context = await EnlistAsync(cancellationToken);
        var keys = nodes.Select(item => item.NodeKey).Distinct().ToArray();
        var rows = await context.Set<ProjectWorkAssignmentRecord>().Where(item => item.ProjectId == projectId &&
            keys.Contains(item.NodeKey)).ToListAsync(cancellationToken);
        context.RemoveRange(rows);
        foreach (var key in rows.Select(item => item.NodeKey).Distinct()) {
            await StageRevisionAsync(context, projectId, key, null, null, cancellationToken);
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task StageDeleteForProjectAsync(Guid projectId, CancellationToken cancellationToken = default) {
        await using var context = await EnlistAsync(cancellationToken);
        context.RemoveRange(await context.Set<ProjectWorkAssignmentRecord>().Where(item => item.ProjectId == projectId).ToListAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task StageMoveAsync(Guid sourceProjectId, Guid targetProjectId, IReadOnlyCollection<ProjectNodeReference> nodes,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(nodes);
        await using var context = await EnlistAsync(cancellationToken);
        if (await projects.GetForMutationAsync(targetProjectId, cancellationToken) is null) {
            throw new InvalidOperationException("The target project was not found.");
        }
        var keys = nodes.Select(item => item.NodeKey).Distinct().ToArray();
        var rows = await context.Set<ProjectWorkAssignmentRecord>().Where(item => keys.Contains(item.NodeKey) &&
            (item.ProjectId == sourceProjectId || item.ProjectId == targetProjectId)).ToListAsync(cancellationToken);
        foreach (var row in rows) {
            if (row.ProjectId == targetProjectId) {
                context.Remove(row);
            } else {
                row.ProjectId = targetProjectId;
            }
        }
        foreach (var key in rows.Select(item => item.NodeKey).Distinct()) {
            await StageRevisionAsync(context, sourceProjectId, key, null, null, cancellationToken);
            await StageRevisionAsync(context, targetProjectId, key, null, null, cancellationToken);
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task StagePartyMergeAsync(ProjectWorkAssignmentPartyMerge merge, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(merge);
        ArgumentNullException.ThrowIfNull(merge.ReplacedAffiliations);
        await using var context = await EnlistAsync(cancellationToken);
        var affiliationIds = merge.ReplacedAffiliations.Keys.ToArray();
        var rows = await context.Set<ProjectWorkAssignmentRecord>().Where(item => item.PartyId == merge.MergedPartyId ||
            item.PartyId == merge.RetainedParty.PartyId || (item.PartyOrganizationAffiliationId.HasValue &&
            affiliationIds.Contains(item.PartyOrganizationAffiliationId.Value))).ToListAsync(cancellationToken);
        if (rows.Any(item => !merge.LockedProjectIds.Contains(item.ProjectId))) {
            throw new DbUpdateConcurrencyException("Work assignment scope changed while the Party merge acquired its project locks. Retry the merge.");
        }
        foreach (var row in rows) {
            if (row.PartyId == merge.MergedPartyId) {
                row.PartyId = merge.RetainedParty.PartyId;
            }
            if (row.PartyOrganizationAffiliationId is { } previous && merge.ReplacedAffiliations.TryGetValue(previous, out var replacement)) {
                row.PartyOrganizationAffiliationId = replacement;
            }
        }
        foreach (var group in rows.Select(item => (item.ProjectId, item.NodeKey)).Distinct()) {
            await context.Set<ProjectWorkAssignmentRecord>().Where(item => item.ProjectId == group.ProjectId &&
                item.NodeKey == group.NodeKey).LoadAsync(cancellationToken);
            await StageRevisionAsync(context, group.ProjectId, group.NodeKey, null, merge.RetainedParty, cancellationToken);
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Error?> ValidateAsync(WorkbenchDbContext context, Guid projectId, string nodeKey,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> requests, CancellationToken cancellationToken) {
        if (projectId == Guid.Empty) {
            return Validation("Project is required.", "project-required");
        }
        foreach (var request in requests) {
            if (request.Role != ProjectPartyAssignmentRole.WorkItemAssignee) {
                return Validation("The work-assignment owner only accepts work-item assignees.", "target-role-mismatch");
            }
            if (request.PartyId == Guid.Empty) {
                return Validation("Party is required.", "party-required");
            }
            if (request.ProjectId != Guid.Empty && request.ProjectId != projectId) {
                return Validation("Desired assignments must target the same project.", "project-mismatch");
            }
            if (!string.IsNullOrWhiteSpace(request.NodeKey) && request.NodeKey.Trim() != nodeKey) {
                return Validation("Desired assignments must target the same node.", "node-mismatch");
            }
            if (request.AllocationPercent is <= 0m or > 100m) {
                return Validation("Allocation must be greater than 0 and no more than 100 percent.", "allocation-range");
            }
            if (request.StartsOn > request.EndsOn) {
                return Validation("Assignment end date must be on or after the start date.", "date-range-invalid");
            }
        }
        if (string.IsNullOrWhiteSpace(nodeKey)) {
            return Validation("A node is required for this assignment.", "node-required");
        }
        if (await projects.GetForMutationAsync(projectId, cancellationToken) is null) {
            return Validation("Project was not found.", "project-not-found");
        }
        var node = await context.Set<ProjectObjectRecord>().AsNoTracking().SingleOrDefaultAsync(item =>
            item.ProjectId == projectId && item.NodeKey == nodeKey, cancellationToken);
        if (node is null) {
            return await context.Set<ProjectObjectRecord>().AnyAsync(item => item.NodeKey == nodeKey, cancellationToken)
                ? Validation("The selected node belongs to another project.", "node-project-mismatch")
                : Validation("The selected node was not found.", "node-not-found");
        }
        if (node.ObjectType != ProjectObjectType.WorkItem) {
            return Validation("The selected node does not allow this assignment role.", "node-role-not-allowed");
        }
        var facts = await parties.ReadForMutationAsync(requests.Select(item => item.PartyId).Distinct().ToArray(), cancellationToken);
        foreach (var request in requests) {
            if (!facts.TryGetValue(request.PartyId, out var fact)) {
                return Validation("Party was not found.", "party-not-found");
            }
            if (fact.PartyType is not (ProjectPartyType.Person or ProjectPartyType.AiAgent)) {
                return Validation("A work-item assignee must be a person or AI agent.", "work-item-assignee-party-type-invalid");
            }
        }
        return await parties.ValidateAffiliationsForMutationAsync(requests.Select(item =>
            new ProjectWorkAssignmentAffiliationRequirement(item.PartyId, item.PartyAffiliationId,
                ToUtcDate(item.StartsOn), ToUtcDate(item.EndsOn))).ToArray(), cancellationToken);
    }

    private async Task<ProjectWorkItemDirectAssignmentMutationResult> StageRevisionAsync(WorkbenchDbContext context,
        Guid projectId, string nodeKey, ProjectWorkItemDirectAssignmentRevision? expectedRevision,
        ProjectWorkAssignmentPartyFact? retainedParty, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(nodeKey)) {
            return new(ProjectWorkItemDirectAssignmentMutationStatus.WorkItemNotFound, null);
        }
        var rows = Current(context, projectId, nodeKey);
        var facts = await parties.ReadForMutationAsync(rows.Select(item => item.PartyId).Distinct().ToArray(), cancellationToken);
        var final = rows.Select(item => {
            var fact = retainedParty?.PartyId == item.PartyId ? retainedParty : facts.GetValueOrDefault(item.PartyId);
            if (fact is null || fact.PartyType is not (ProjectPartyType.Person or ProjectPartyType.AiAgent)) {
                throw new InvalidOperationException("A direct task-assignment revision requires valid Person or Agent identities.");
            }
            return new ProjectWorkItemDirectAssignmentState(fact.PartyType, item.PartyId, item.IsPrimary, fact.DisplayName);
        }).ToArray();
        return await revisions.StageMutationAsync(projectId, new ProjectNodeReference(nodeKey), final, expectedRevision, cancellationToken);
    }

    private static List<ProjectWorkAssignmentRecord> Current(WorkbenchDbContext context, Guid projectId, string nodeKey) =>
        context.ChangeTracker.Entries<ProjectWorkAssignmentRecord>().Where(entry =>
            entry.State is not (EntityState.Deleted or EntityState.Detached) && entry.Entity.ProjectId == projectId &&
            entry.Entity.NodeKey == nodeKey).Select(entry => entry.Entity).ToList();

    private static void Apply(ProjectWorkAssignmentRecord entity, ProjectPartyAssignmentUpsertRequest request,
        Guid projectId, string nodeKey, bool isPrimary) {
        entity.ProjectId = projectId;
        entity.PartyId = request.PartyId;
        entity.PartyOrganizationAffiliationId = request.PartyAffiliationId;
        entity.NodeKey = nodeKey;
        entity.IsPrimary = isPrimary;
        entity.AllocationPercent = request.AllocationPercent;
        entity.StartsAtUtc = ToUtcDate(request.StartsOn);
        entity.EndsAtUtc = ToUtcDate(request.EndsOn);
        entity.Source = string.IsNullOrWhiteSpace(request.Source) ? "crm-hr-ui" : request.Source.Trim();
        entity.Notes = request.Notes?.Trim() ?? string.Empty;
    }

    private Task<WorkbenchDbContext> EnlistAsync(CancellationToken cancellationToken) =>
        transactions.CreateEnlistedAsync(options, static configured => new WorkbenchDbContext(configured), cancellationToken);

    private static string[] MutationKeys(Guid projectId, IEnumerable<Guid> assignmentIds) =>
        assignmentIds.Select(ProjectAssignmentMutationKeys.ForAssignment).Append(ProjectMutationScopeKeys.ForProject(projectId)).ToArray();

    private static DateTimeOffset? ToUtcDate(DateOnly? date) => date.HasValue
        ? new DateTimeOffset(date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)) : null;

    private static Error Validation(string message, string code) => Error.Validation(message, $"crmhr.project-assignment.{code}");

    private static Result Stale() => Result.Failure(Error.Failure(
        "Project assignments changed before the requested replacement could be applied.", ProjectPartyIntegrationErrorCodes.StaleAssignmentSnapshot));
}
