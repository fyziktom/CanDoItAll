using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectTaskPlanStagedTask(int Ordinal, ProjectTaskPlanTaskFact Fact,
    string MetadataJson, double PositionX, double PositionY, DateTimeOffset CreatedAtUtc);
internal sealed record ProjectTaskPlanStagedBatch(IReadOnlyList<ProjectTaskPlanStagedTask> Tasks);

internal static class ProjectTaskPlanTaskStaging {
    internal static ProjectTaskPlanStagedBatch StageNewPlainTasks(WorkbenchDbContext context,
        ProjectWriteAdmission target, ProjectTaskPlanProposal proposal, ProjectTaskPlanCurrentMaterial current,
        DateTimeOffset acceptedAtUtc) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(current);
        if (current.Basis.Project.Expectation.Project != target || acceptedAtUtc == default) {
            throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
        }
        ProjectTaskPlanBasisLimits.RequireUtc(acceptedAtUtc);
        ProjectTaskPlanBasisLimits.RequireBytes((long)current.CandidateCount + proposal.Tasks.Count,
            ProjectTaskPlanBasisLimits.MaximumCandidates);
        var entries = context.ChangeTracker.Entries().ToArray();
        if (entries.Any(entry => entry.Entity is ProjectObjectRecord row && row.ProjectId == target.ProjectId &&
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)) {
            throw new InvalidOperationException("Task staging requires an unchanged current-material scope.");
        }
        var ids = entries.Where(entry => entry.Entity is ProjectObjectRecord)
            .Select(entry => ((ProjectObjectRecord)entry.Entity).Id).ToHashSet();
        var keys = entries.Where(entry => entry.Entity is ProjectObjectRecord row && row.ProjectId == target.ProjectId)
            .Select(entry => ((ProjectObjectRecord)entry.Entity).NodeKey).ToHashSet(StringComparer.Ordinal);
        var bindingTargets = entries.Where(entry => entry.Entity is ProjectNodeBindingRecord)
            .Select(entry => ((ProjectNodeBindingRecord)entry.Entity).ProjectObjectId).ToHashSet();
        foreach (var fact in current.Basis.Tasks) {
            ids.Add(fact.RecordId);
            keys.Add(fact.NodeKey);
        }
        var nodes = new ProjectObjectRecord[proposal.Tasks.Count];
        var plans = new ProjectNodeBindingStorage.PreparedPlainTaskBinding[nodes.Length];
        var staged = new ProjectTaskPlanStagedTask[nodes.Length];
        var facts = new List<ProjectTaskPlanTaskFact>(current.Basis.Tasks.Count + nodes.Length);
        facts.AddRange(current.Basis.Tasks);
        var sourceBytes = current.SourceBytes;
        for (var index = 0; index < nodes.Length; index++) {
            var node = Create(target.ProjectId, proposal.Tasks[index], current.CandidateCount + index, acceptedAtUtc);
            if (!ids.Add(node.Id) || !keys.Add(node.NodeKey) || !bindingTargets.Add(node.Id)) {
                throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
            }
            var plan = ProjectNodeBindingStorage.PrepareNewPlainTaskBinding(node);
            var row = Source(node, plan.Plan.SanitizedMetadataJson);
            sourceBytes += ProjectTaskPlanMaterialProjection.ValidateSourceBytes(row);
            ProjectTaskPlanBasisLimits.RequireBytes(sourceBytes, ProjectTaskPlanBasisLimits.MaximumSourceBytes);
            var fact = ProjectTaskPlanMaterialProjection.Project(row)
                ?? throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
            nodes[index] = node;
            plans[index] = plan;
            facts.Add(fact);
            staged[index] = new(index, fact, plan.Plan.SanitizedMetadataJson, node.PositionX, node.PositionY, acceptedAtUtc);
        }
        var combined = new ProjectTaskPlanBasis(current.Basis.Namespace, current.Basis.Project, acceptedAtUtc, facts);
        _ = ProjectTaskPlanBasisCodec.Encode(combined);
        context.AddRange(nodes);
        for (var index = 0; index < nodes.Length; index++) {
            ProjectNodeBindingStorage.StageNewPlainTaskBinding(context, nodes[index], plans[index]);
        }
        return new(Array.AsReadOnly(staged));
    }

    private static ProjectObjectRecord Create(Guid projectId, ProjectTaskPlanDraftItem item, int position,
        DateTimeOffset acceptedAtUtc) {
        var id = Guid.NewGuid();
        var nodeKey = $"{ProjectWorkbenchGraphConventions.CustomNodePrefix}{id:N}";
        var parent = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);
        new ProjectStructureInvariantService().ValidateParentAssignment(projectId, nodeKey, parent, []);
        var progress = ProjectWorkbenchObjectModeling.ResolveStatusBackedProgress("Draft");
        var layout = ProjectWorkbenchGraphConventions.GetDefaultPosition(ProjectObjectType.WorkItem, position);
        var estimate = ProjectTaskEstimate.Empty();
        var metadata = new ProjectObjectMetadataEnvelope {
            WorkItem = new() {
                WorkItemKind = ProjectWorkItemKind.Task,
                Description = item.Description,
                ExpectedEffortHours = estimate.ExpectedEffortHours,
                ExpectedEffortUnit = estimate.ExpectedEffortUnit,
                ExpectedCostAmount = estimate.ExpectedCostAmount,
                ExpectedCostCurrencyCode = estimate.ExpectedCostCurrencyCode,
                ExecutionState = ProjectTaskExecutionState.NotStarted
            }
        };
        return new() {
            Id = id, ProjectId = projectId, NodeKey = nodeKey, ParentNodeKey = parent,
            ObjectType = ProjectObjectType.WorkItem, ObjectSubtype = ProjectObjectSubtypePolicy.Task,
            Title = item.Title, Subtitle = "Task", Notes = item.Notes, Status = "Draft", Priority = item.Priority,
            ProgressMode = progress.Mode, ProgressPercent = progress.Percent,
            PositionX = layout.X, PositionY = layout.Y, CreatedAtUtc = acceptedAtUtc, UpdatedAtUtc = acceptedAtUtc,
            NodeReferences = new(),
            MetadataJson = ProjectStructureCanonicalTaskCreationPolicy.NormalizeMetadataJson(ProjectObjectType.WorkItem,
                ProjectObjectSubtypePolicy.Task, ProjectObjectMetadataSerializer.Serialize(metadata),
                ProjectObjectTaskPricingInitialization.ClearAuthoritativePricing)
        };
    }

    private static ProjectTaskPlanSourceRow Source(ProjectObjectRecord node, string metadata) => new() {
        Id = node.Id, NodeKey = node.NodeKey, ParentNodeKey = node.ParentNodeKey, ObjectType = node.ObjectType,
        ObjectSubtype = node.ObjectSubtype, IsSystemManaged = node.IsSystemManaged, Title = node.Title,
        Subtitle = node.Subtitle, Notes = node.Notes, Status = node.Status, Priority = node.Priority,
        ProgressMode = node.ProgressMode, ProgressPercent = node.ProgressPercent, StartUtc = node.StartUtc,
        EndUtc = node.EndUtc, DurationSeconds = node.DurationSeconds, MetadataJson = metadata
    };
}
