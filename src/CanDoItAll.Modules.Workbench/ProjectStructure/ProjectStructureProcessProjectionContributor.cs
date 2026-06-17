using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureProcessProjectionContributor(
    ProcessPersistenceDbContext processDbContext,
    ProcessDefinitionCatalogProjectionService definitionCatalogProjectionService) : IProjectStructureProjectionContributor
{
    public async Task ContributeAsync(ProjectStructureProjectionContext context, CancellationToken cancellationToken)
    {
        var userAuthoredLinks = await context.DbContext.Set<ProjectObjectLinkRecord>()
            .AsNoTracking()
            .Where(item =>
                item.ProjectId == context.ProjectId &&
                !item.IsSystemManaged &&
                (item.SourceNodeKey.StartsWith(ProjectStructureProcessNodeKeys.ProcessDefinitionPrefix) ||
                 item.SourceNodeKey.StartsWith(ProjectStructureProcessNodeKeys.ProcessRunPrefix) ||
                 item.TargetNodeKey.StartsWith(ProjectStructureProcessNodeKeys.ProcessDefinitionPrefix) ||
                 item.TargetNodeKey.StartsWith(ProjectStructureProcessNodeKeys.ProcessRunPrefix)))
            .Select(item => new ProjectStructureProcessLink(
                item.SourceNodeKey,
                item.TargetNodeKey,
                item.LinkKind,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        if (userAuthoredLinks.Count == 0)
        {
            return;
        }

        var linkedDefinitionIds = userAuthoredLinks
            .SelectMany(link => new[] { link.SourceNodeKey, link.TargetNodeKey })
            .Select(TryResolveProcessDefinitionId)
            .Where(definitionId => definitionId.HasValue)
            .Select(definitionId => definitionId!.Value)
            .ToHashSet();
        var linkedRunIds = userAuthoredLinks
            .SelectMany(link => new[] { link.SourceNodeKey, link.TargetNodeKey })
            .Select(TryResolveProcessRunId)
            .Where(runId => runId.HasValue)
            .Select(runId => runId!.Value)
            .ToHashSet();

        if (linkedDefinitionIds.Count == 0 && linkedRunIds.Count == 0)
        {
            return;
        }

        var catalog = await definitionCatalogProjectionService.GetCatalogAsync(
            ProcessWorkspaceShellScope.ForProject(context.ProjectId),
            new ProcessDefinitionCatalogQueryProjection(
                SearchText: null,
                SelectedDefinitionKey: null,
                ProcessDefinitionCatalogScopeKind.All,
                Take: 200),
            cancellationToken: cancellationToken);
        var catalogItemsByDefinitionId = catalog.Items
            .ToDictionary(
                item => ProcessDefinitionCatalogProjectionService.CreateDefinitionId(item.Key).Value,
                item => item);

        var runIds = linkedRunIds.ToArray();
        var runtimeStates = runIds.Length == 0
            ? []
            : await processDbContext.RuntimeStates
                .AsNoTracking()
                .Where(item => runIds.Contains(item.RunId))
                .OrderByDescending(item => item.UpdatedAtUtc)
                .Select(item => new ProjectStructureProcessRuntimeState(
                    item.RunId,
                    item.RootRunId,
                    item.PlanId,
                    item.Status,
                    item.UpdatedAtUtc))
                .ToListAsync(cancellationToken);
        var planIds = runtimeStates
            .Select(item => item.PlanId)
            .Distinct()
            .ToArray();
        var plansById = planIds.Length == 0
            ? new Dictionary<Guid, ProjectStructureProcessPlan>()
            : await processDbContext.InstancePlans
                .AsNoTracking()
                .Where(item => planIds.Contains(item.PlanId))
                .Select(item => new ProjectStructureProcessPlan(
                    item.PlanId,
                    item.DefinitionId,
                    item.CreatedAtUtc))
                .ToDictionaryAsync(item => item.PlanId, cancellationToken);
        var stepStatsByRunId = runIds.Length == 0
            ? new Dictionary<Guid, ProcessRunProjectionStats>()
            : await processDbContext.RuntimeSteps
                .AsNoTracking()
                .Where(item => runIds.Contains(item.RunId))
                .GroupBy(item => item.RunId)
                .Select(group => new ProcessRunProjectionStats(
                    group.Key,
                    group.Count(),
                    group.Count(item => item.Status == ProcessRuntimeStepStatus.Completed),
                    group.Count(item => item.Status == ProcessRuntimeStepStatus.Blocked),
                    group.Count(item => item.Status == ProcessRuntimeStepStatus.WaitingApproval),
                    group.Count(item => item.Status == ProcessRuntimeStepStatus.Ready ||
                                        item.Status == ProcessRuntimeStepStatus.Running ||
                                        item.Status == ProcessRuntimeStepStatus.Claimed)))
                .ToDictionaryAsync(item => item.RunId, cancellationToken);

        foreach (var definitionId in runtimeStates
            .Select(state => plansById.TryGetValue(state.PlanId, out var plan) ? plan.DefinitionId : (Guid?)null)
            .Where(definitionId => definitionId.HasValue)
            .Select(definitionId => definitionId!.Value))
        {
            linkedDefinitionIds.Add(definitionId);
        }

        var preferredDefinitionParentByNodeKey = BuildPreferredDefinitionParentMap(userAuthoredLinks);
        var preferredRunParentByNodeKey = BuildPreferredRunParentMap(userAuthoredLinks);

        foreach (var definition in linkedDefinitionIds
            .OrderBy(definitionId => ResolveDefinitionSortName(definitionId, catalogItemsByDefinitionId), StringComparer.OrdinalIgnoreCase)
            .Select((definitionId, index) => new { DefinitionId = definitionId, Index = index }))
        {
            AddDefinitionNode(
                context,
                definition.DefinitionId,
                definition.Index,
                catalogItemsByDefinitionId.GetValueOrDefault(definition.DefinitionId),
                preferredDefinitionParentByNodeKey);
        }

        foreach (var run in runtimeStates.Select((state, index) => new { State = state, Index = index }))
        {
            plansById.TryGetValue(run.State.PlanId, out var plan);
            var definitionItem = plan is null
                ? null
                : catalogItemsByDefinitionId.GetValueOrDefault(plan.DefinitionId);
            AddRunNode(
                context,
                run.State,
                plan,
                definitionItem,
                stepStatsByRunId.GetValueOrDefault(run.State.RunId, ProcessRunProjectionStats.Empty(run.State.RunId)),
                run.Index,
                preferredRunParentByNodeKey);
        }
    }

    private static void AddDefinitionNode(
        ProjectStructureProjectionContext context,
        Guid definitionId,
        int index,
        ProcessDefinitionCatalogItemProjection? definition,
        IReadOnlyDictionary<string, string> preferredDefinitionParentByNodeKey)
    {
        var nodeKey = ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(definitionId);
        var position = ProjectWorkbenchGraphConventions.GetDefaultPosition(ProjectObjectType.ProcessDefinition, index);
        var parentNodeKey = preferredDefinitionParentByNodeKey.TryGetValue(nodeKey, out var preferredParentNodeKey) &&
                            !string.IsNullOrWhiteSpace(preferredParentNodeKey)
            ? preferredParentNodeKey
            : ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(context.ProjectId);
        var title = definition?.Name ?? $"Process definition {ShortId(definitionId)}";
        var status = definition?.Status.ToString() ?? "Linked";
        var summary = definition?.Summary ?? "Linked process definition.";

        context.AddNode(new ProjectObjectRecord
        {
            ProjectId = context.ProjectId,
            NodeKey = nodeKey,
            ObjectType = ProjectObjectType.ProcessDefinition,
            Title = title,
            Subtitle = definition is null
                ? "Process definition"
                : $"{definition.ScopeKind} · {definition.Status}",
            Status = status,
            Notes = BuildDefinitionNotes(definitionId, definition, summary),
            Binding = ProjectStructureProjectionBindingFactory.Create(
                $"/projects/{context.ProjectId:D}/processes?processId={definitionId:D}",
                "process-definition",
                definitionId),
            ParentNodeKey = parentNodeKey,
            PositionX = position.X,
            PositionY = position.Y,
            CreatedAtUtc = definition?.UpdatedAtUtc ?? context.AssembledAtUtc,
            UpdatedAtUtc = context.AssembledAtUtc
        });

        if (context.ContainsNode(nodeKey) &&
            string.Equals(parentNodeKey, ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(context.ProjectId), StringComparison.Ordinal))
        {
            context.AddLink(parentNodeKey, nodeKey, ProjectObjectLinkKind.Contains);
        }
    }

    private static void AddRunNode(
        ProjectStructureProjectionContext context,
        ProjectStructureProcessRuntimeState state,
        ProjectStructureProcessPlan? plan,
        ProcessDefinitionCatalogItemProjection? definition,
        ProcessRunProjectionStats stats,
        int index,
        IReadOnlyDictionary<string, string> preferredRunParentByNodeKey)
    {
        var runNodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(state.RunId);
        var definitionNodeKey = plan is null
            ? ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(context.ProjectId)
            : ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(plan.DefinitionId);
        var parentNodeKey = preferredRunParentByNodeKey.TryGetValue(runNodeKey, out var preferredParentNodeKey) &&
                            !string.IsNullOrWhiteSpace(preferredParentNodeKey)
            ? preferredParentNodeKey
            : definitionNodeKey;
        var position = ProjectWorkbenchGraphConventions.GetDefaultPosition(ProjectObjectType.ProcessRun, index);
        var definitionTitle = definition?.Name ?? "Process";
        var progressPercent = stats.TotalStepCount == 0
            ? -1
            : Math.Clamp((int)Math.Round(stats.CompletedStepCount * 100d / stats.TotalStepCount, MidpointRounding.AwayFromZero), 0, 100);

        context.AddNode(new ProjectObjectRecord
        {
            ProjectId = context.ProjectId,
            NodeKey = runNodeKey,
            ObjectType = ProjectObjectType.ProcessRun,
            Title = $"{definitionTitle} run",
            Subtitle = BuildRunSubtitle(state, stats),
            Status = state.Status.ToString(),
            Notes = BuildRunNotes(context.ProjectId, state, plan, definition, stats),
            Binding = ProjectStructureProjectionBindingFactory.Create(
                $"/projects/{context.ProjectId:D}/processes/live?runId={state.RunId:D}",
                "process-run",
                state.RunId),
            ParentNodeKey = parentNodeKey,
            PositionX = position.X,
            PositionY = position.Y,
            ProgressMode = progressPercent >= 0 ? "progress" : string.Empty,
            ProgressPercent = progressPercent,
            CreatedAtUtc = plan?.CreatedAtUtc ?? state.UpdatedAtUtc,
            UpdatedAtUtc = state.UpdatedAtUtc
        });

        if (!context.ContainsNode(runNodeKey))
        {
            return;
        }

        context.AddLink(parentNodeKey, runNodeKey, ProjectObjectLinkKind.Contains);
    }

    private static IReadOnlyDictionary<string, string> BuildPreferredDefinitionParentMap(
        IReadOnlyList<ProjectStructureProcessLink> userAuthoredLinks)
    {
        return userAuthoredLinks
            .Where(link =>
                link.LinkKind == ProjectObjectLinkKind.Uses &&
                TryResolveProcessDefinitionId(link.TargetNodeKey).HasValue &&
                !string.IsNullOrWhiteSpace(link.SourceNodeKey))
            .OrderByDescending(link => link.CreatedAtUtc)
            .ThenBy(link => link.SourceNodeKey, StringComparer.Ordinal)
            .GroupBy(link => link.TargetNodeKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().SourceNodeKey, StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> BuildPreferredRunParentMap(
        IReadOnlyList<ProjectStructureProcessLink> userAuthoredLinks)
    {
        return userAuthoredLinks
            .Where(link =>
                link.LinkKind == ProjectObjectLinkKind.Uses &&
                TryResolveProcessRunId(link.TargetNodeKey).HasValue &&
                !string.IsNullOrWhiteSpace(link.SourceNodeKey))
            .OrderByDescending(link => ProjectStructureProcessNodeKeys.TryParseProcessRunNodeKey(link.SourceNodeKey, out _) ? 1 : 0)
            .ThenByDescending(link => link.CreatedAtUtc)
            .ThenBy(link => link.SourceNodeKey, StringComparer.Ordinal)
            .GroupBy(link => link.TargetNodeKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().SourceNodeKey, StringComparer.Ordinal);
    }

    private static Guid? TryResolveProcessDefinitionId(string nodeKey)
        => ProjectStructureProcessNodeKeys.TryParseProcessDefinitionNodeKey(nodeKey, out var definitionId)
            ? definitionId
            : null;

    private static Guid? TryResolveProcessRunId(string nodeKey)
        => ProjectStructureProcessNodeKeys.TryParseProcessRunNodeKey(nodeKey, out var runId)
            ? runId
            : null;

    private static string ResolveDefinitionSortName(
        Guid definitionId,
        IReadOnlyDictionary<Guid, ProcessDefinitionCatalogItemProjection> catalogItemsByDefinitionId)
        => catalogItemsByDefinitionId.TryGetValue(definitionId, out var definition)
            ? definition.Name
            : definitionId.ToString("N");

    private static string BuildDefinitionNotes(
        Guid definitionId,
        ProcessDefinitionCatalogItemProjection? definition,
        string summary)
    {
        var key = definition?.Key.Value ?? "unknown";
        return string.Join(
            Environment.NewLine,
            new[]
            {
                summary,
                $"Definition id: {definitionId:D}",
                $"Definition key: {key}"
            });
    }

    private static string BuildRunSubtitle(ProjectStructureProcessRuntimeState state, ProcessRunProjectionStats stats)
    {
        var stepSummary = stats.TotalStepCount == 0
            ? "No runtime steps"
            : $"{stats.CompletedStepCount}/{stats.TotalStepCount} steps complete";
        var issueSummary = stats.BlockedStepCount > 0
            ? $" · {stats.BlockedStepCount} blocked"
            : stats.WaitingApprovalStepCount > 0
                ? $" · {stats.WaitingApprovalStepCount} waiting approval"
                : stats.ActiveStepCount > 0
                    ? $" · {stats.ActiveStepCount} active"
                    : string.Empty;
        return $"{state.Status} · {stepSummary}{issueSummary}";
    }

    private static string BuildRunNotes(
        Guid projectId,
        ProjectStructureProcessRuntimeState state,
        ProjectStructureProcessPlan? plan,
        ProcessDefinitionCatalogItemProjection? definition,
        ProcessRunProjectionStats stats)
    {
        var definitionName = definition?.Name ?? "Unknown definition";
        var definitionKey = definition?.Key.Value ?? "unknown";
        return string.Join(
            Environment.NewLine,
            new[]
            {
                $"{definitionName} runtime state for project {projectId:D}.",
                $"Run id: {state.RunId:D}",
                $"Root run id: {state.RootRunId:D}",
                $"Plan id: {state.PlanId:D}",
                plan is null ? "Definition id: unknown" : $"Definition id: {plan.DefinitionId:D}",
                $"Definition key: {definitionKey}",
                $"Steps: {stats.CompletedStepCount}/{stats.TotalStepCount} completed, {stats.BlockedStepCount} blocked, {stats.WaitingApprovalStepCount} waiting approval, {stats.ActiveStepCount} active.",
                $"Updated: {state.UpdatedAtUtc:u}"
            });
    }

    private static string ShortId(Guid value)
        => value.ToString("N")[..8];

    private sealed record ProjectStructureProcessLink(
        string SourceNodeKey,
        string TargetNodeKey,
        ProjectObjectLinkKind LinkKind,
        DateTimeOffset CreatedAtUtc);

    private sealed record ProjectStructureProcessRuntimeState(
        Guid RunId,
        Guid RootRunId,
        Guid PlanId,
        ProcessRuntimeStatus Status,
        DateTimeOffset UpdatedAtUtc);

    private sealed record ProjectStructureProcessPlan(
        Guid PlanId,
        Guid DefinitionId,
        DateTimeOffset CreatedAtUtc);

    private sealed record ProcessRunProjectionStats(
        Guid RunId,
        int TotalStepCount,
        int CompletedStepCount,
        int BlockedStepCount,
        int WaitingApprovalStepCount,
        int ActiveStepCount)
    {
        public static ProcessRunProjectionStats Empty(Guid runId)
        {
            return new ProcessRunProjectionStats(runId, 0, 0, 0, 0, 0);
        }
    }
}
