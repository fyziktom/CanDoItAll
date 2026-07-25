using System.Text.Json;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureProcessProjectionContributor(
    IDbContextFactory<ProcessPersistenceDbContext> processDbContextFactory,
    ProcessDefinitionCatalogProjectionService definitionCatalogProjectionService,
    IWorkspacePathResolver workspacePathResolver,
    ILogger<ProjectStructureProcessProjectionContributor> logger,
    ProjectStructureProcessRunRecordProjector runRecordProjector) : IProjectStructureProjectionContributor
{
    private const string ProjectIdVariableName = "ProjectId";
    private const string ProjectNodeIdVariableName = "ProjectNodeId";
    private const string ProcessRunIdVariableName = "ProcessRunId";
    private const string CurrentProcessRunIdVariableName = "CurrentProcessRunId";
    private const string ProcessRunNodeIdVariableName = "ProcessRunNodeId";
    private const string CurrentProcessRunNodeIdVariableName = "CurrentProcessRunNodeId";
    private const string ProductRootVariableName = "ProductRoot";
    private const string OutputRootVariableName = "OutputRoot";
    private const string ExternalTargetRootVariableName = "ExternalTargetRoot";
    private const string ProcessRunOutputFolderArtifactKind = "process-run-output-folder";
    private const string ProcessRunSummaryArtifactKind = "process-run-summary";
    private const string ProcessRunScreenshotArtifactKind = "process-run-screenshot";
    private const string ProcessRunRuntimeArtifactKind = "process-run-runtime";
    private const int MaxProjectedScreenshotCount = 6;
    private const int MaxProjectedRuntimeProjectCount = 3;
    private const int MaxRuntimeProjectSearchDepth = 6;
    private const int MaxRuntimeProjectSearchEntries = 2000;
    private static readonly HashSet<string> ScreenshotExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    private static readonly HashSet<string> SkippedRuntimeProjectDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        "bin",
        "node_modules",
        "obj",
        "TestResults"
    };

    public async Task ContributeAsync(ProjectStructureProjectionContext context, CancellationToken cancellationToken)
    {
        var runRecordProjectionsTask = runRecordProjector.LoadAsync(
            context.ProjectId,
            cancellationToken);
        var userAuthoredLinks = await context.DbContext.Set<ProjectObjectLinkRecord>()
            .AsNoTracking()
            .Where(item =>
                item.ProjectId == context.ProjectId &&
                !item.IsSystemManaged &&
                (item.SourceNodeKey.StartsWith(ProjectStructureProcessNodeKeys.ProcessDefinitionPrefix, StringComparison.Ordinal) ||
                 item.SourceNodeKey.StartsWith(ProjectStructureProcessNodeKeys.ProcessRunPrefix, StringComparison.Ordinal) ||
                 item.TargetNodeKey.StartsWith(ProjectStructureProcessNodeKeys.ProcessDefinitionPrefix, StringComparison.Ordinal) ||
                 item.TargetNodeKey.StartsWith(ProjectStructureProcessNodeKeys.ProcessRunPrefix, StringComparison.Ordinal)))
            .Select(item => new ProjectStructureProcessLink(
                item.SourceNodeKey,
                item.TargetNodeKey,
                item.LinkKind,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);

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
        var runRecordProjectionsByRunId = await runRecordProjectionsTask.ConfigureAwait(false);
        var durableRunIds = runRecordProjectionsByRunId.Keys.ToHashSet();

        await using var processDbContext = await processDbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var projectScopedRunReferences = await LoadProjectScopedRunReferencesAsync(
                processDbContext,
                context.ProjectId,
                durableRunIds,
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var reference in projectScopedRunReferences)
        {
            linkedRunIds.Add(reference.RunId);
        }

        foreach (var runId in durableRunIds)
        {
            linkedRunIds.Add(runId);
        }

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

        var runtimeDiscoveryRunIds = linkedRunIds
            .Where(runId => !durableRunIds.Contains(runId))
            .ToArray();
        var linkedRuntimeStates = runtimeDiscoveryRunIds.Length == 0
            ? []
            : await processDbContext.RuntimeStates
                .AsNoTracking()
                .Where(item => runtimeDiscoveryRunIds.Contains(item.RunId))
                .OrderByDescending(item => item.UpdatedAtUtc)
                .Select(item => new ProjectStructureProcessRuntimeState(
                    item.RunId,
                    item.RootRunId,
                    item.PlanId,
                    item.Status,
                    item.UpdatedAtUtc))
                .ToListAsync(cancellationToken);
        var projectedRuntimeRunIds = linkedRuntimeStates
            .Select(item => item.RootRunId)
            .Distinct()
            .ToArray();
        var persistedRuntimeStates = projectedRuntimeRunIds.Length == 0
            ? []
            : await processDbContext.RuntimeStates
                .AsNoTracking()
                .Where(item => projectedRuntimeRunIds.Contains(item.RunId))
                .OrderByDescending(item => item.UpdatedAtUtc)
                .Select(item => new ProjectStructureProcessRuntimeState(
                    item.RunId,
                    item.RootRunId,
                    item.PlanId,
                    item.Status,
                    item.UpdatedAtUtc))
                .ToListAsync(cancellationToken);
        var runtimeStatesByRunId = persistedRuntimeStates.ToDictionary(state => state.RunId);
        foreach (var projection in runRecordProjectionsByRunId.Values)
        {
            runtimeStatesByRunId[projection.RunId] = new ProjectStructureProcessRuntimeState(
                projection.RunId,
                projection.RootRunId,
                projection.PlanId,
                projection.RuntimeStatus,
                projection.UpdatedAtUtc);
        }

        var runtimeStates = runtimeStatesByRunId.Values
            .OrderByDescending(state => state.UpdatedAtUtc)
            .ThenByDescending(state => state.RunId)
            .ToArray();
        var projectedRunNodeKeyByLinkedRunNodeKey = linkedRuntimeStates
            .ToDictionary(
                item => ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(item.RunId),
                item => ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(item.RootRunId),
                StringComparer.Ordinal);
        var planIds = runtimeStates
            .Where(item => !runRecordProjectionsByRunId.ContainsKey(item.RunId))
            .Select(item => item.PlanId)
            .Where(planId => planId.HasValue)
            .Select(planId => planId!.Value)
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
        var projectedRunIdArray = runtimeStates
            .Select(item => item.RunId)
            .ToArray();
        var runtimeStepRunIds = projectedRunIdArray
            .Where(runId => !runRecordProjectionsByRunId.ContainsKey(runId))
            .ToArray();
        var stepStatsByRunId = runtimeStepRunIds.Length == 0
            ? new Dictionary<Guid, ProjectStructureProcessRunProjectionStats>()
            : await processDbContext.RuntimeSteps
                .AsNoTracking()
                .Where(item => runtimeStepRunIds.Contains(item.RunId))
                .GroupBy(item => item.RunId)
                .Select(group => new ProjectStructureProcessRunProjectionStats(
                    group.Key,
                    group.Count(),
                    group.Count(item => item.Status == ProcessRuntimeStepStatus.Completed),
                    group.Count(item => item.Status == ProcessRuntimeStepStatus.Blocked),
                    group.Count(item => item.Status == ProcessRuntimeStepStatus.WaitingApproval),
                    group.Count(item => item.Status == ProcessRuntimeStepStatus.Ready ||
                                        item.Status == ProcessRuntimeStepStatus.Waiting ||
                                        item.Status == ProcessRuntimeStepStatus.Running ||
                                        item.Status == ProcessRuntimeStepStatus.Claimed)))
                .ToDictionaryAsync(item => item.RunId, cancellationToken);

        foreach (var projection in runRecordProjectionsByRunId.Values)
        {
            stepStatsByRunId[projection.RunId] = projection.Stats;
        }

        foreach (var state in runtimeStates)
        {
            plansById.TryGetValue(state.PlanId ?? Guid.Empty, out var plan);
            var definitionId = ResolveDefinitionId(
                plan,
                runRecordProjectionsByRunId.GetValueOrDefault(state.RunId));
            if (definitionId.HasValue)
            {
                linkedDefinitionIds.Add(definitionId.Value);
            }
        }

        var preferredDefinitionParentByNodeKey = BuildPreferredDefinitionParentMap(userAuthoredLinks);
        var preferredRunParentByNodeKey = MergePreferredRunParentMaps(
            BuildPreferredRunParentMap(
                userAuthoredLinks,
                projectedRunNodeKeyByLinkedRunNodeKey),
            projectScopedRunReferences);
        var outputFoldersByRunId = BuildOutputFolderMap(projectScopedRunReferences);
        var projectScopedRunReferencesByRunId = projectScopedRunReferences
            .GroupBy(reference => reference.RunId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProjectScopedProcessRunReference>)group.ToArray());

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

        var placementSession = new ProjectStructureAutomaticPlacementSession(context.AllNodes);
        foreach (var run in runtimeStates.Select((state, index) => new { State = state, Index = index }))
        {
            plansById.TryGetValue(run.State.PlanId ?? Guid.Empty, out var plan);
            var runRecordProjection = runRecordProjectionsByRunId.GetValueOrDefault(run.State.RunId);
            var definitionId = ResolveDefinitionId(plan, runRecordProjection);
            var definitionItem = definitionId is null
                ? null
                : catalogItemsByDefinitionId.GetValueOrDefault(definitionId.Value);
            var runNode = AddRunNode(
                context,
                run.State,
                plan,
                definitionItem,
                stepStatsByRunId.GetValueOrDefault(
                    run.State.RunId,
                    ProjectStructureProcessRunProjectionStats.Empty(run.State.RunId)),
                runRecordProjection,
                run.Index,
                preferredRunParentByNodeKey);
            if (runNode is not null)
            {
                placementSession.Add(runNode);
            }

            AddRunOutputNodes(
                context,
                placementSession,
                run.State,
                outputFoldersByRunId.GetValueOrDefault(run.State.RunId, []));
            AddRunEvidenceNodes(
                context,
                placementSession,
                run.State,
                plan,
                definitionItem,
                stepStatsByRunId.GetValueOrDefault(
                    run.State.RunId,
                    ProjectStructureProcessRunProjectionStats.Empty(run.State.RunId)),
                projectScopedRunReferencesByRunId.GetValueOrDefault(run.State.RunId, []),
                runRecordProjection);
        }
    }

    private static Guid? ResolveDefinitionId(
        ProjectStructureProcessPlan? plan,
        ProjectStructureProcessRunRecordProjection? runRecordProjection)
    {
        if (runRecordProjection?.DefinitionId is { } definitionId)
        {
            return definitionId;
        }

        return plan?.DefinitionId;
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

    private static ProjectObjectRecord? AddRunNode(
        ProjectStructureProjectionContext context,
        ProjectStructureProcessRuntimeState state,
        ProjectStructureProcessPlan? plan,
        ProcessDefinitionCatalogItemProjection? definition,
        ProjectStructureProcessRunProjectionStats stats,
        ProjectStructureProcessRunRecordProjection? runRecordProjection,
        int index,
        IReadOnlyDictionary<string, string> preferredRunParentByNodeKey)
    {
        var runNodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(state.RunId);
        var definitionId = ResolveDefinitionId(plan, runRecordProjection);
        var definitionNodeKey = definitionId is null
            ? ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(context.ProjectId)
            : ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(definitionId.Value);
        var parentNodeKey = preferredRunParentByNodeKey.TryGetValue(runNodeKey, out var preferredParentNodeKey) &&
                            !string.IsNullOrWhiteSpace(preferredParentNodeKey)
            ? preferredParentNodeKey
            : definitionNodeKey;
        var position = ProjectWorkbenchGraphConventions.GetDefaultPosition(ProjectObjectType.ProcessRun, index);
        var definitionTitle = definition?.Name ?? "Process";
        var progressPercent = stats.TotalStepCount == 0
            ? -1
            : Math.Clamp((int)Math.Round(stats.CompletedStepCount * 100d / stats.TotalStepCount, MidpointRounding.AwayFromZero), 0, 100);

        var runNode = new ProjectObjectRecord
        {
            ProjectId = context.ProjectId,
            NodeKey = runNodeKey,
            ObjectType = ProjectObjectType.ProcessRun,
            Title = $"{definitionTitle} run",
            Subtitle = runRecordProjection?.Subtitle ?? BuildRunSubtitle(state, stats),
            Status = runRecordProjection?.Status ?? state.Status.ToString(),
            Notes = runRecordProjection is null
                ? BuildRunNotes(context.ProjectId, state, plan, definition, stats)
                : runRecordProjection.Notes,
            Binding = ProjectStructureProjectionBindingFactory.Create(
                $"/projects/{context.ProjectId:D}/processes/live?runId={state.RunId:D}",
                "process-run",
                state.RunId),
            ParentNodeKey = parentNodeKey,
            PositionX = position.X,
            PositionY = position.Y,
            ProgressMode = progressPercent >= 0 ? "progress" : string.Empty,
            ProgressPercent = progressPercent,
            CreatedAtUtc = runRecordProjection?.StartedAtUtc ??
                           runRecordProjection?.EndedAtUtc ??
                           plan?.CreatedAtUtc ??
                           state.UpdatedAtUtc,
            UpdatedAtUtc = runRecordProjection?.UpdatedAtUtc ?? state.UpdatedAtUtc
        };
        context.AddNode(runNode);

        if (!context.ContainsNode(runNodeKey))
        {
            return null;
        }

        context.AddLink(parentNodeKey, runNodeKey, ProjectObjectLinkKind.Contains);
        return runNode;
    }

    private static void AddRunOutputNodes(
        ProjectStructureProjectionContext context,
        ProjectStructureAutomaticPlacementSession placementSession,
        ProjectStructureProcessRuntimeState state,
        IReadOnlyList<ProcessRunArtifactRootResolution> outputFolders)
    {
        var runNodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(state.RunId);
        if (!context.ContainsNode(runNodeKey))
        {
            return;
        }

        var projectableFolders = outputFolders.Count == 0
            ? ProcessRunArtifactRootPolicy.ResolveCurrentRunRoots(state.RunId, [])
            : outputFolders;
        foreach (var outputFolder in projectableFolders
            .Where(folder => folder.ShouldProject)
            .GroupBy(folder => folder.DirectoryPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(folder => folder.DirectoryPath, StringComparer.OrdinalIgnoreCase))
        {
            var nodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunOutputNodeKey(state.RunId, outputFolder.DirectoryPath);
            AddRunChildNode(context, placementSession, new ProjectObjectRecord
            {
                ProjectId = context.ProjectId,
                NodeKey = nodeKey,
                ObjectType = ProjectObjectType.File,
                ObjectSubtype = "folder",
                Title = ResolveRunOutputTitle(outputFolder.Kind),
                Subtitle = ResolveRunOutputSubtitle(outputFolder.Kind),
                Status = state.Status.ToString(),
                Notes = BuildRunOutputNotes(state.RunId, outputFolder),
                MetadataJson = BuildRunOutputMetadataJson(outputFolder),
                Binding = ProjectStructureProjectionBindingFactory.Create(
                    $"/projects/{context.ProjectId:D}/structure",
                    ProcessRunOutputFolderArtifactKind,
                    state.RunId),
                ParentNodeKey = runNodeKey,
                CreatedAtUtc = state.UpdatedAtUtc,
                UpdatedAtUtc = state.UpdatedAtUtc
            });
        }
    }

    private void AddRunEvidenceNodes(
        ProjectStructureProjectionContext context,
        ProjectStructureAutomaticPlacementSession placementSession,
        ProjectStructureProcessRuntimeState state,
        ProjectStructureProcessPlan? plan,
        ProcessDefinitionCatalogItemProjection? definition,
        ProjectStructureProcessRunProjectionStats stats,
        IReadOnlyList<ProjectScopedProcessRunReference> runReferences,
        ProjectStructureProcessRunRecordProjection? runRecordProjection)
    {
        var runNodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(state.RunId);
        if (!context.ContainsNode(runNodeKey))
        {
            return;
        }

        AddRunSummaryNode(
            context,
            placementSession,
            state,
            plan,
            definition,
            stats,
            runRecordProjection);
        AddRunScreenshotNodes(context, placementSession, state);
        AddRunRuntimeNodes(context, placementSession, state, runReferences);
    }

    private static void AddRunSummaryNode(
        ProjectStructureProjectionContext context,
        ProjectStructureAutomaticPlacementSession placementSession,
        ProjectStructureProcessRuntimeState state,
        ProjectStructureProcessPlan? plan,
        ProcessDefinitionCatalogItemProjection? definition,
        ProjectStructureProcessRunProjectionStats stats,
        ProjectStructureProcessRunRecordProjection? runRecordProjection)
    {
        var runNodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(state.RunId);
        var nodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunSummaryNodeKey(state.RunId);
        AddRunChildNode(context, placementSession, new ProjectObjectRecord
        {
            ProjectId = context.ProjectId,
            NodeKey = nodeKey,
            ObjectType = ProjectObjectType.Note,
            ObjectSubtype = "process-summary",
            Title = "Run summary",
            Subtitle = runRecordProjection?.Subtitle ?? BuildRunSummarySubtitle(state, stats),
            Status = runRecordProjection?.Status ?? state.Status.ToString(),
            Notes = runRecordProjection?.Notes ??
                    BuildRunSummaryNotes(context.ProjectId, state, plan, definition, stats),
            MetadataJson = runRecordProjection?.MetadataJson ??
                           BuildRunSummaryMetadataJson(state, stats),
            Binding = ProjectStructureProjectionBindingFactory.Create(
                $"/projects/{context.ProjectId:D}/processes/live?runId={state.RunId:D}",
                ProcessRunSummaryArtifactKind,
                state.RunId),
            ParentNodeKey = runNodeKey,
            CreatedAtUtc = runRecordProjection?.StartedAtUtc ?? plan?.CreatedAtUtc ?? state.UpdatedAtUtc,
            UpdatedAtUtc = runRecordProjection?.UpdatedAtUtc ?? state.UpdatedAtUtc
        });
    }

    private void AddRunScreenshotNodes(
        ProjectStructureProjectionContext context,
        ProjectStructureAutomaticPlacementSession placementSession,
        ProjectStructureProcessRuntimeState state)
    {
        var runNodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(state.RunId);
        var screenshots = EnumerateRunScreenshots(state.RunId);
        for (var index = 0; index < screenshots.Count; index++)
        {
            var screenshot = screenshots[index];
            var nodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunScreenshotNodeKey(state.RunId, screenshot.RelativePath);
            var storageReference = StorageJson.CreateLegacyManagedFileReference(
                screenshot.RelativePath,
                screenshot.ContentType,
                screenshot.FileName,
                screenshot.Length);
            AddRunChildNode(context, placementSession, new ProjectObjectRecord
            {
                ProjectId = context.ProjectId,
                NodeKey = nodeKey,
                ObjectType = ProjectObjectType.ImageAsset,
                ObjectSubtype = "screenshot",
                Title = BuildScreenshotTitle(screenshot.FileName),
                Subtitle = "Process UI evidence",
                Status = state.Status.ToString(),
                Notes = BuildScreenshotNotes(state.RunId, screenshot),
                MetadataJson = BuildScreenshotMetadataJson(screenshot),
                Binding = new ProjectNodeBindingState(
                    StorageJson.BuildPreviewUrl(storageReference),
                    ProcessRunScreenshotArtifactKind,
                    state.RunId,
                    screenshot.RelativePath,
                    screenshot.ContentType,
                    screenshot.FileName,
                    StorageJson.SerializeReference(storageReference)),
                ParentNodeKey = runNodeKey,
                CreatedAtUtc = screenshot.LastWriteTimeUtc,
                UpdatedAtUtc = screenshot.LastWriteTimeUtc
            });
        }
    }

    private void AddRunRuntimeNodes(
        ProjectStructureProjectionContext context,
        ProjectStructureAutomaticPlacementSession placementSession,
        ProjectStructureProcessRuntimeState state,
        IReadOnlyList<ProjectScopedProcessRunReference> runReferences)
    {
        var runNodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(state.RunId);
        var runtimeProjects = EnumerateRuntimeProjects(runReferences);
        for (var index = 0; index < runtimeProjects.Count; index++)
        {
            var runtimeProject = runtimeProjects[index];
            var nodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunRuntimeNodeKey(state.RunId, runtimeProject.ProjectPath);
            var title = runtimeProjects.Count == 1
                ? "Run final app"
                : $"Run {runtimeProject.ProjectName}";
            AddRunChildNode(context, placementSession, new ProjectObjectRecord
            {
                ProjectId = context.ProjectId,
                NodeKey = nodeKey,
                ObjectType = ProjectObjectType.Environment,
                ObjectSubtype = "dotnet-watch",
                Title = title,
                Subtitle = ".NET watch runtime",
                Status = state.Status.ToString(),
                Notes = BuildRuntimeNotes(state.RunId, runtimeProject),
                MetadataJson = BuildRuntimeMetadataJson(runtimeProject),
                Binding = ProjectStructureProjectionBindingFactory.Create(
                    $"/projects/{context.ProjectId:D}/structure",
                    ProcessRunRuntimeArtifactKind,
                    state.RunId),
                ParentNodeKey = runNodeKey,
                CreatedAtUtc = state.UpdatedAtUtc,
                UpdatedAtUtc = state.UpdatedAtUtc
            });
        }
    }

    private static void AddRunChildNode(
        ProjectStructureProjectionContext context,
        ProjectStructureAutomaticPlacementSession placementSession,
        ProjectObjectRecord node)
    {
        var runNodeKey = node.ParentNodeKey ??
                         throw new InvalidOperationException($"Projected process child '{node.NodeKey}' requires a process run parent.");
        var direction = placementSession.ResolveIncomingDirection(runNodeKey);
        var position = placementSession.Resolve(
            new ProjectStructureAutomaticPlacementRequest(
                runNodeKey,
                node.ObjectType,
                node.Title,
                node.Subtitle,
                node.Notes,
                RequiredDirection: direction));
        node.PositionX = position.X;
        node.PositionY = position.Y;
        context.AddNode(node);

        if (context.ContainsNode(node.NodeKey))
        {
            placementSession.Add(node);
            context.AddLink(runNodeKey, node.NodeKey, ProjectObjectLinkKind.Contains);
        }
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
        IReadOnlyList<ProjectStructureProcessLink> userAuthoredLinks,
        IReadOnlyDictionary<string, string> projectedRunNodeKeyByLinkedRunNodeKey)
    {
        return userAuthoredLinks
            .Where(link =>
                link.LinkKind == ProjectObjectLinkKind.Uses &&
                TryResolveProcessRunId(link.TargetNodeKey).HasValue &&
                !string.IsNullOrWhiteSpace(link.SourceNodeKey))
            .Select(link =>
            {
                var projectedTargetNodeKey = projectedRunNodeKeyByLinkedRunNodeKey.TryGetValue(link.TargetNodeKey, out var mappedTargetNodeKey)
                    ? mappedTargetNodeKey
                    : link.TargetNodeKey;
                return new ProjectStructureProcessLink(
                    link.SourceNodeKey,
                    projectedTargetNodeKey,
                    link.LinkKind,
                    link.CreatedAtUtc);
            })
            .Where(link =>
                !ProjectStructureProcessNodeKeys.TryParseProcessRunNodeKey(link.SourceNodeKey, out _) &&
                !string.Equals(link.SourceNodeKey, link.TargetNodeKey, StringComparison.Ordinal))
            .OrderByDescending(link => link.CreatedAtUtc)
            .ThenBy(link => link.SourceNodeKey, StringComparer.Ordinal)
            .GroupBy(link => link.TargetNodeKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().SourceNodeKey, StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> MergePreferredRunParentMaps(
        IReadOnlyDictionary<string, string> userAuthoredParentByNodeKey,
        IReadOnlyList<ProjectScopedProcessRunReference> projectScopedRunReferences)
    {
        var merged = new Dictionary<string, string>(userAuthoredParentByNodeKey, StringComparer.Ordinal);
        foreach (var reference in projectScopedRunReferences
            .Where(reference => !string.IsNullOrWhiteSpace(reference.ProjectNodeKey))
            .OrderByDescending(reference => reference.CreatedAtUtc)
            .ThenBy(reference => reference.ProjectNodeKey, StringComparer.Ordinal)
            .GroupBy(reference => ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(reference.RunId), StringComparer.Ordinal)
            .Select(group => group.First()))
        {
            merged.TryAdd(ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(reference.RunId), reference.ProjectNodeKey);
        }

        return merged;
    }

    private static IReadOnlyDictionary<Guid, IReadOnlyList<ProcessRunArtifactRootResolution>> BuildOutputFolderMap(
        IReadOnlyList<ProjectScopedProcessRunReference> projectScopedRunReferences)
    {
        return projectScopedRunReferences
            .GroupBy(reference => reference.RunId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessRunArtifactRootResolution>)group
                    .SelectMany(reference => reference.OutputFolders)
                    .Where(folder => folder.ShouldProject)
                    .GroupBy(folder => folder.DirectoryPath, StringComparer.OrdinalIgnoreCase)
                    .Select(folderGroup => folderGroup.First())
                    .OrderBy(folder => folder.DirectoryPath, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
    }

    private static async Task<IReadOnlyList<ProjectScopedProcessRunReference>> LoadProjectScopedRunReferencesAsync(
        ProcessPersistenceDbContext processDbContext,
        Guid projectId,
        IReadOnlyCollection<Guid> excludedRunIds,
        CancellationToken cancellationToken)
    {
        var projectIdText = projectId.ToString("D");
        var projectIdSnippet = BuildLaunchVariableJsonSnippet(ProjectIdVariableName, projectIdText);
        var assignmentsQuery = processDbContext.RuntimeStepAssignments.AsNoTracking();
        if (excludedRunIds.Count > 0)
        {
            var excludedRunIdArray = excludedRunIds.ToArray();
            assignmentsQuery = assignmentsQuery.Where(assignment => !excludedRunIdArray.Contains(assignment.RunId));
        }

        var rows = await assignmentsQuery
            .Where(assignment => assignment.LaunchVariablesJson.Contains(projectIdSnippet))
            .Select(assignment => new ProjectStructureProcessAssignmentScope(
                assignment.RunId,
                assignment.LaunchVariablesJson,
                assignment.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var references = new List<ProjectScopedProcessRunReference>();
        foreach (var row in rows)
        {
            var variables = DeserializeLaunchVariables(row.LaunchVariablesJson);
            if (!IsMatchingProjectScope(projectIdText, variables) ||
                !IsRootProjectRunAssignment(row.RunId, variables) ||
                string.IsNullOrWhiteSpace(ResolveLaunchVariable(variables, ProjectNodeIdVariableName)))
            {
                continue;
            }

            references.Add(new ProjectScopedProcessRunReference(
                row.RunId,
                ResolveLaunchVariable(variables, ProjectNodeIdVariableName),
                EnumerateProjectableOutputFolders(row.RunId, variables),
                EnumerateProductRoots(variables),
                row.CreatedAtUtc));
        }

        return references
            .OrderBy(reference => reference.CreatedAtUtc)
            .ThenBy(reference => reference.RunId)
            .ToArray();
    }

    private static bool IsMatchingProjectScope(
        string projectIdText,
        IReadOnlyDictionary<string, string> variables)
        => string.Equals(ResolveLaunchVariable(variables, ProjectIdVariableName), projectIdText, StringComparison.OrdinalIgnoreCase);

    private static bool IsRootProjectRunAssignment(
        Guid runId,
        IReadOnlyDictionary<string, string> variables)
    {
        if (TryResolveLaunchVariableGuid(variables, CurrentProcessRunIdVariableName, out var currentRunId) &&
            currentRunId != runId)
        {
            return false;
        }

        if (TryResolveLaunchVariableGuid(variables, ProcessRunIdVariableName, out var processRunId) &&
            processRunId != runId)
        {
            return false;
        }

        var currentRunNodeId = ResolveLaunchVariable(variables, CurrentProcessRunNodeIdVariableName);
        var processRunNodeId = ResolveLaunchVariable(variables, ProcessRunNodeIdVariableName);
        return string.IsNullOrWhiteSpace(currentRunNodeId) ||
               string.IsNullOrWhiteSpace(processRunNodeId) ||
               string.Equals(currentRunNodeId, processRunNodeId, StringComparison.Ordinal);
    }

    private static IReadOnlyList<ProcessRunArtifactRootResolution> EnumerateProjectableOutputFolders(
        Guid runId,
        IReadOnlyDictionary<string, string> variables)
        => ProcessRunArtifactRootPolicy.ResolveCurrentRunRoots(runId, [variables]);

    private static IReadOnlyList<string> EnumerateProductRoots(IReadOnlyDictionary<string, string> variables)
    {
        return new[]
            {
                ResolveLaunchVariable(variables, ProductRootVariableName),
                ResolveLaunchVariable(variables, OutputRootVariableName),
                ResolveLaunchVariable(variables, ExternalTargetRootVariableName)
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> DeserializeLaunchVariables(string launchVariablesJson)
    {
        if (string.IsNullOrWhiteSpace(launchVariablesJson))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(launchVariablesJson);
        return parsed is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(parsed, StringComparer.Ordinal);
    }

    private static string ResolveLaunchVariable(
        IReadOnlyDictionary<string, string> variables,
        string key)
        => variables.TryGetValue(key, out var value)
            ? value.Trim()
            : string.Empty;

    private static bool TryResolveLaunchVariableGuid(
        IReadOnlyDictionary<string, string> variables,
        string key,
        out Guid value)
        => Guid.TryParse(ResolveLaunchVariable(variables, key), out value);

    private static string BuildLaunchVariableJsonSnippet(string key, string value)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [key] = value
        });
        return json.Trim('{', '}');
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

    private static string BuildRunSubtitle(
        ProjectStructureProcessRuntimeState state,
        ProjectStructureProcessRunProjectionStats stats)
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

    private static string BuildRunSummarySubtitle(
        ProjectStructureProcessRuntimeState state,
        ProjectStructureProcessRunProjectionStats stats)
    {
        return IsTerminal(state.Status)
            ? $"{state.Status} · durable summary pending"
            : BuildRunSubtitle(state, stats);
    }

    private static string BuildRunNotes(
        Guid projectId,
        ProjectStructureProcessRuntimeState state,
        ProjectStructureProcessPlan? plan,
        ProcessDefinitionCatalogItemProjection? definition,
        ProjectStructureProcessRunProjectionStats stats)
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
                state.PlanId is { } planId
                    ? $"Plan id: {planId:D}"
                    : "Plan id: unavailable",
                plan is null ? "Definition id: unknown" : $"Definition id: {plan.DefinitionId:D}",
                $"Definition key: {definitionKey}",
                $"Steps: {stats.CompletedStepCount}/{stats.TotalStepCount} completed, {stats.BlockedStepCount} blocked, {stats.WaitingApprovalStepCount} waiting approval, {stats.ActiveStepCount} active.",
                $"Updated: {state.UpdatedAtUtc:u}"
            });
    }

    private static string BuildRunSummaryNotes(
        Guid projectId,
        ProjectStructureProcessRuntimeState state,
        ProjectStructureProcessPlan? plan,
        ProcessDefinitionCatalogItemProjection? definition,
        ProjectStructureProcessRunProjectionStats stats)
    {
        var summaryState = IsTerminal(state.Status)
            ? "The durable process-run summary is pending background assembly."
            : "The process is still active; this node contains live projection data.";
        return string.Join(
            Environment.NewLine,
            new[]
            {
                summaryState,
                BuildRunNotes(projectId, state, plan, definition, stats)
            });
    }

    private static string BuildRunSummaryMetadataJson(
        ProjectStructureProcessRuntimeState state,
        ProjectStructureProcessRunProjectionStats stats)
    {
        return JsonSerializer.Serialize(new
        {
            processRunSummary = new
            {
                state.RunId,
                state.RootRunId,
                state.PlanId,
                Status = state.Status.ToString(),
                stats.TotalStepCount,
                stats.CompletedStepCount,
                stats.BlockedStepCount,
                stats.WaitingApprovalStepCount,
                stats.ActiveStepCount,
                DurableRecordStatus = IsTerminal(state.Status)
                    ? "Pending"
                    : "NotTerminal",
                state.UpdatedAtUtc
            }
        });
    }

    private static bool IsTerminal(ProcessRuntimeStatus status)
        => status is ProcessRuntimeStatus.Completed
            or ProcessRuntimeStatus.Failed
            or ProcessRuntimeStatus.Cancelled
            or ProcessRuntimeStatus.Escalated;

    private static string BuildScreenshotTitle(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        return string.IsNullOrWhiteSpace(name)
            ? "Process screenshot"
            : $"Screenshot - {name}";
    }

    private static string BuildScreenshotNotes(
        Guid runId,
        ProjectedProcessRunFile screenshot)
        => string.Join(
            Environment.NewLine,
            new[]
            {
                $"Process run screenshot for {runId:D}.",
                $"Managed path: {screenshot.RelativePath}",
                $"Content type: {screenshot.ContentType}",
                $"Size: {screenshot.Length} bytes",
                $"Captured: {screenshot.LastWriteTimeUtc:u}"
            });

    private static string BuildScreenshotMetadataJson(ProjectedProcessRunFile screenshot)
    {
        return JsonSerializer.Serialize(new
        {
            processRunScreenshot = new
            {
                screenshot.RelativePath,
                screenshot.ContentType,
                screenshot.Length,
                screenshot.LastWriteTimeUtc
            }
        });
    }

    private static string BuildRuntimeNotes(
        Guid runId,
        ProjectedRuntimeProject runtimeProject)
        => string.Join(
            Environment.NewLine,
            new[]
            {
                $"Process run runtime entry for {runId:D}.",
                $"Project path: {runtimeProject.ProjectPath}",
                $"Working directory: {runtimeProject.WorkingDirectory}",
                $"Product root: {runtimeProject.ProductRoot}",
                $"Command: dotnet watch --project \"{runtimeProject.ProjectPath}\" run"
            });

    private static string BuildRuntimeMetadataJson(ProjectedRuntimeProject runtimeProject)
    {
        return ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            Environment = new ProjectEnvironmentMetadata
            {
                EnvironmentKind = ProjectEnvironmentKind.DotNetWatch,
                ProjectPath = runtimeProject.ProjectPath,
                WorkingDirectory = runtimeProject.WorkingDirectory,
                RuntimeProtocol = ProjectRuntimeProtocol.Http
            }
        });
    }

    private static string ResolveRunOutputTitle(ProcessRunArtifactRootKind kind)
        => kind switch
        {
            ProcessRunArtifactRootKind.ManagedProductOutputRoot => "Product output",
            ProcessRunArtifactRootKind.ManagedArtifactRunRoot => "Run artifacts",
            _ => "Run output"
        };

    private static string ResolveRunOutputSubtitle(ProcessRunArtifactRootKind kind)
        => kind switch
        {
            ProcessRunArtifactRootKind.ManagedProductOutputRoot => "Managed product output folder",
            ProcessRunArtifactRootKind.ManagedArtifactRunRoot => "Managed artifact folder",
            _ => "Managed process run folder"
        };

    private static string BuildRunOutputNotes(
        Guid runId,
        ProcessRunArtifactRootResolution outputFolder)
        => string.Join(
            Environment.NewLine,
            new[]
            {
                $"Process run output for {runId:D}.",
                $"Folder kind: {outputFolder.Kind}.",
                $"Managed path: {outputFolder.DirectoryPath}"
            });

    private static string BuildRunOutputMetadataJson(ProcessRunArtifactRootResolution outputFolder)
    {
        return JsonSerializer.Serialize(new
        {
            processRunOutput = new
            {
                outputFolder.DirectoryPath,
                Kind = outputFolder.Kind.ToString()
            }
        });
    }

    private IReadOnlyList<ProjectedProcessRunFile> EnumerateRunScreenshots(Guid runId)
    {
        try
        {
            var workspaceRoot = workspacePathResolver.ResolveWorkspaceRoot();
            var runRoot = Path.Combine(workspaceRoot, "artifacts", "process-runs", runId.ToString("D"));
            if (!Directory.Exists(runRoot))
            {
                return [];
            }

            return Directory
                .EnumerateFiles(
                    runRoot,
                    "*",
                    new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        AttributesToSkip = 0
                    })
                .Where(path => ScreenshotExtensions.Contains(Path.GetExtension(path)))
                .Select(path => CreateProjectedProcessRunFile(workspaceRoot, path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Take(MaxProjectedScreenshotCount)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            logger.LogWarning(ex, "Could not enumerate process run screenshots for run {RunId}.", runId);
            return [];
        }
    }

    private IReadOnlyList<ProjectedRuntimeProject> EnumerateRuntimeProjects(
        IReadOnlyList<ProjectScopedProcessRunReference> runReferences)
    {
        if (runReferences.Count == 0)
        {
            return [];
        }

        var projects = new List<ProjectedRuntimeProject>();
        var seenProjectPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var productRoot in runReferences
            .SelectMany(reference => reference.ProductRoots)
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (projects.Count >= MaxProjectedRuntimeProjectCount)
            {
                break;
            }

            if (!TryResolveProductRootPath(productRoot, out var fullProductRoot))
            {
                continue;
            }

            foreach (var runtimeProject in FindRuntimeProjects(fullProductRoot))
            {
                if (!seenProjectPaths.Add(runtimeProject.ProjectPath))
                {
                    continue;
                }

                projects.Add(runtimeProject);
                if (projects.Count >= MaxProjectedRuntimeProjectCount)
                {
                    break;
                }
            }
        }

        return projects;
    }

    private bool TryResolveProductRootPath(string productRoot, out string fullPath)
    {
        fullPath = string.Empty;
        try
        {
            var trimmedRoot = productRoot.Trim();
            var candidatePath = Path.IsPathRooted(trimmedRoot)
                ? Path.GetFullPath(trimmedRoot)
                : Path.GetFullPath(Path.Combine(workspacePathResolver.ResolveWorkspaceRoot(), trimmedRoot.Replace('/', Path.DirectorySeparatorChar)));
            if (!Directory.Exists(candidatePath))
            {
                return false;
            }

            fullPath = candidatePath;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            logger.LogWarning(ex, "Could not resolve process run product root {ProductRoot}.", productRoot);
            return false;
        }
    }

    private IReadOnlyList<ProjectedRuntimeProject> FindRuntimeProjects(string productRoot)
    {
        var candidates = new List<string>();
        var pending = new Stack<(string DirectoryPath, int Depth)>();
        var visitedEntryCount = 0;
        pending.Push((productRoot, 0));

        while (pending.Count > 0 &&
               visitedEntryCount < MaxRuntimeProjectSearchEntries &&
               candidates.Count < MaxProjectedRuntimeProjectCount * 4)
        {
            var current = pending.Pop();
            if (!TryEnumerateFileSystemEntries(current.DirectoryPath, "*.csproj", filesOnly: true, out var projectFiles))
            {
                continue;
            }

            foreach (var projectFile in projectFiles)
            {
                candidates.Add(projectFile);
                visitedEntryCount++;
                if (visitedEntryCount >= MaxRuntimeProjectSearchEntries)
                {
                    break;
                }
            }

            if (current.Depth >= MaxRuntimeProjectSearchDepth ||
                visitedEntryCount >= MaxRuntimeProjectSearchEntries)
            {
                continue;
            }

            if (!TryEnumerateFileSystemEntries(current.DirectoryPath, "*", filesOnly: false, out var childDirectories))
            {
                continue;
            }

            foreach (var childDirectory in childDirectories
                .Where(directory => !ShouldSkipRuntimeProjectDirectory(directory))
                .OrderByDescending(directory => directory, StringComparer.OrdinalIgnoreCase))
            {
                pending.Push((childDirectory, current.Depth + 1));
                visitedEntryCount++;
                if (visitedEntryCount >= MaxRuntimeProjectSearchEntries)
                {
                    break;
                }
            }
        }

        var orderedCandidates = candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(IsLikelyTestProject)
            .ThenBy(path => CountPathSegments(Path.GetRelativePath(productRoot, path)))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var appCandidates = orderedCandidates
            .Where(path => !IsLikelyTestProject(path))
            .ToArray();
        var selectedCandidates = appCandidates.Length == 0
            ? orderedCandidates
            : appCandidates;

        return selectedCandidates
            .Take(MaxProjectedRuntimeProjectCount)
            .Select(path => new ProjectedRuntimeProject(
                productRoot,
                path,
                Path.GetDirectoryName(path) ?? productRoot,
                Path.GetFileNameWithoutExtension(path)))
            .ToArray();
    }

    private bool TryEnumerateFileSystemEntries(
        string directoryPath,
        string searchPattern,
        bool filesOnly,
        out IReadOnlyList<string> entries)
    {
        try
        {
            entries = filesOnly
                ? Directory.EnumerateFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly).ToArray()
                : Directory.EnumerateDirectories(directoryPath, searchPattern, SearchOption.TopDirectoryOnly).ToArray();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            logger.LogWarning(ex, "Could not enumerate process runtime project directory {DirectoryPath}.", directoryPath);
            entries = [];
            return false;
        }
    }

    private static bool ShouldSkipRuntimeProjectDirectory(string directoryPath)
    {
        var name = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return SkippedRuntimeProjectDirectories.Contains(name) ||
               string.Equals(name, "tests", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyTestProject(string projectPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(projectPath);
        if (fileName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith("Tests", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return projectPath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment =>
                string.Equals(segment, "tests", StringComparison.OrdinalIgnoreCase) ||
                segment.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase));
    }

    private static int CountPathSegments(string path)
    {
        return path.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;
    }

    private static ProjectedProcessRunFile CreateProjectedProcessRunFile(string workspaceRoot, string fullPath)
    {
        var info = new FileInfo(fullPath);
        var relativePath = Path.GetRelativePath(workspaceRoot, fullPath)
            .Replace('\\', '/')
            .TrimStart('/');
        return new ProjectedProcessRunFile(
            relativePath,
            Path.GetFileName(fullPath),
            ResolveImageContentType(fullPath),
            info.Length,
            new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
    }

    private static string ResolveImageContentType(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "image/png"
        };

    private static string ShortId(Guid value)
        => value.ToString("N")[..8];

    private sealed record ProjectStructureProcessAssignmentScope(
        Guid RunId,
        string LaunchVariablesJson,
        DateTimeOffset CreatedAtUtc);

    private sealed record ProjectScopedProcessRunReference(
        Guid RunId,
        string ProjectNodeKey,
        IReadOnlyList<ProcessRunArtifactRootResolution> OutputFolders,
        IReadOnlyList<string> ProductRoots,
        DateTimeOffset CreatedAtUtc);

    private sealed record ProjectStructureProcessLink(
        string SourceNodeKey,
        string TargetNodeKey,
        ProjectObjectLinkKind LinkKind,
        DateTimeOffset CreatedAtUtc);

    private sealed record ProjectStructureProcessRuntimeState(
        Guid RunId,
        Guid RootRunId,
        Guid? PlanId,
        ProcessRuntimeStatus Status,
        DateTimeOffset UpdatedAtUtc);

    private sealed record ProjectStructureProcessPlan(
        Guid PlanId,
        Guid DefinitionId,
        DateTimeOffset CreatedAtUtc);

    private sealed record ProjectedProcessRunFile(
        string RelativePath,
        string FileName,
        string ContentType,
        long Length,
        DateTimeOffset LastWriteTimeUtc);

    private sealed record ProjectedRuntimeProject(
        string ProductRoot,
        string ProjectPath,
        string WorkingDirectory,
        string ProjectName);
}
