using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkbenchCommandService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    PromptFactoryService promptFactoryService,
    ProjectStructureAssemblyService projectStructureAssemblyService)
{
    public async Task<ArtifactReference?> ExecuteNodeCommandAsync(
        Guid projectId,
        string nodeKey,
        ProjectStructureCommandKind commandKind,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var node = await projectStructureAssemblyService.FindNodeAsync(dbContext, projectId, nodeKey, cancellationToken);
        if (node is null)
        {
            return null;
        }

        if (node.ObjectType == ProjectObjectType.PromptFlow &&
            commandKind is ProjectStructureCommandKind.Open or ProjectStructureCommandKind.Wizard)
        {
            var artifact = await EnsurePromptFlowWizardAsync(dbContext, projectId, node, cancellationToken);
            if (!node.IsSystemManaged)
            {
                await ProjectNodeBindingStorage.PersistAsync(dbContext, node, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return artifact;
        }

        if (string.Equals(node.ExternalArtifactKind, "prompt-node", StringComparison.OrdinalIgnoreCase) &&
            node.ExternalArtifactId.HasValue)
        {
            var promptNode = await dbContext.Set<PromptRunNode>()
                .FirstOrDefaultAsync(item => item.Id == node.ExternalArtifactId.Value, cancellationToken);
            if (promptNode is null)
            {
                return null;
            }

            switch (commandKind)
            {
                case ProjectStructureCommandKind.Branch:
                    var branchNode = new PromptRunNode
                    {
                        PromptRunId = promptNode.PromptRunId,
                        PromptBlockDefinitionId = promptNode.PromptBlockDefinitionId,
                        ParentPromptRunNodeId = promptNode.Id,
                        Title = $"{promptNode.Title} follow-up",
                        BranchKey = $"branch-{clock.GetUtcNow():yyyyMMddHHmmss}",
                        BranchLabel = "Workbench follow-up",
                        Sequence = promptNode.Sequence + 1,
                        State = PromptRunNodeState.Pending,
                        Notes = "Created from the structure canvas branch action."
                    };
                    await dbContext.Set<PromptRunNode>().AddAsync(branchNode, cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return new ArtifactReference(
                        "prompt-session",
                        branchNode.PromptRunId,
                        "Prompt Session",
                        $"/prompt-factory?runId={branchNode.PromptRunId}",
                        "Prompt branch session",
                        projectId,
                        $"prompt-session:{branchNode.PromptRunId:N}",
                        TabKind: WorkbenchTabKinds.PromptWizardSession);
                case ProjectStructureCommandKind.Skip:
                    promptNode.State = PromptRunNodeState.Skipped;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return null;
                case ProjectStructureCommandKind.MarkUsed:
                    promptNode.State = PromptRunNodeState.Used;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return null;
            }
        }

        return commandKind switch
        {
            ProjectStructureCommandKind.Validate => new ArtifactReference("validation", null, "Validation Center", $"/validation?projectId={projectId}", "Project validation workspace", projectId),
            ProjectStructureCommandKind.Test => new ArtifactReference("test-plan", null, "Test Lab", $"/test-lab?projectId={projectId}", "Project test planning workspace", projectId),
            ProjectStructureCommandKind.Open => BuildArtifactReference(node, projectId),
            _ => BuildArtifactReference(node, projectId)
        };
    }

    public async Task<ArtifactReference?> EnsurePromptFlowWizardAsync(
        AppDbContext dbContext,
        Guid projectId,
        ProjectObjectRecord node,
        CancellationToken cancellationToken)
    {
        var effectiveRoute = ResolveEffectiveRoute(node);
        if (!string.IsNullOrWhiteSpace(effectiveRoute) &&
            effectiveRoute.StartsWith("/prompt-factory", StringComparison.OrdinalIgnoreCase))
        {
            var resolvedSessionId = node.Binding.ExternalArtifactId ?? node.ExternalArtifactId;
            if (!resolvedSessionId.HasValue &&
                TryResolvePromptFactorySessionId(effectiveRoute, out var routeSessionId))
            {
                resolvedSessionId = routeSessionId;
            }

            ApplyPromptSessionBinding(node, effectiveRoute, resolvedSessionId);
            node.UpdatedAtUtc = clock.GetUtcNow();
            return BuildArtifactReference(node, projectId);
        }

        var phase = await ResolvePromptFlowPhaseAsync(dbContext, projectId, node, cancellationToken);
        var sessionId = await promptFactoryService.CreateBlankProjectSessionAsync(projectId, node.Title, phase, cancellationToken);
        ApplyPromptSessionBinding(node, $"/prompt-factory?sessionId={sessionId}", sessionId);
        node.UpdatedAtUtc = clock.GetUtcNow();
        return BuildArtifactReference(node, projectId);
    }

    private static string ResolveEffectiveRoute(ProjectObjectRecord node)
    {
        return !string.IsNullOrWhiteSpace(node.Binding.Route)
            ? node.Binding.Route
            : node.Route;
    }

    private static void ApplyPromptSessionBinding(ProjectObjectRecord node, string route, Guid? sessionId)
    {
        node.Binding = node.Binding with
        {
            Route = route,
            ExternalArtifactKind = "prompt-session",
            ExternalArtifactId = sessionId
        };
    }

    private static ArtifactReference? BuildArtifactReference(ProjectObjectRecord node, Guid projectId)
    {
        var binding = ProjectNodeBindingStorage.ResolveForRuntime(node);
        if (string.IsNullOrWhiteSpace(binding.Route))
        {
            return null;
        }

        var tabKind = node.ObjectType switch
        {
            ProjectObjectType.ProjectRoot => WorkbenchTabKinds.ProjectOverview,
            ProjectObjectType.PromptFlow or ProjectObjectType.PromptSession or ProjectObjectType.PromptStep => WorkbenchTabKinds.PromptWizardSession,
            ProjectObjectType.ValidationRun => WorkbenchTabKinds.ValidationRun,
            ProjectObjectType.TestPlan or ProjectObjectType.TestEvidence => WorkbenchTabKinds.TestPlan,
            _ when binding.Route.EndsWith("/structure", StringComparison.OrdinalIgnoreCase) => WorkbenchTabKinds.ProjectStructure,
            _ when binding.Route.EndsWith("/calendar", StringComparison.OrdinalIgnoreCase) => WorkbenchTabKinds.ProjectCalendar,
            _ => WorkbenchTabKinds.Page
        };

        return new ArtifactReference(
            binding.ExternalArtifactKind,
            binding.ExternalArtifactId,
            node.Title,
            binding.Route,
            node.Notes,
            projectId,
            node.NodeKey,
            TabKind: tabKind);
    }

    private static bool TryResolvePromptFactorySessionId(string route, out Guid sessionId)
    {
        sessionId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(route))
        {
            return false;
        }

        const string marker = "sessionId=";
        var start = route.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return false;
        }

        start += marker.Length;
        var end = route.IndexOf('&', start);
        var rawValue = end >= start ? route[start..end] : route[start..];
        return Guid.TryParse(rawValue, out sessionId);
    }

    private static async Task<string> ResolvePromptFlowPhaseAsync(
        AppDbContext dbContext,
        Guid projectId,
        ProjectObjectRecord node,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(node.ParentNodeKey))
        {
            var records = await dbContext.Set<ProjectObjectRecord>()
                .Where(item => item.ProjectId == projectId)
                .ToDictionaryAsync(item => item.NodeKey, cancellationToken);
            var currentParentKey = node.ParentNodeKey;
            var visited = new HashSet<string>(StringComparer.Ordinal);

            while (!string.IsNullOrWhiteSpace(currentParentKey) &&
                   visited.Add(currentParentKey) &&
                   records.TryGetValue(currentParentKey, out var parentNode))
            {
                if (parentNode.ObjectType == ProjectObjectType.Phase &&
                    !string.IsNullOrWhiteSpace(parentNode.Title))
                {
                    return parentNode.Title.Trim();
                }

                currentParentKey = parentNode.ParentNodeKey;
            }
        }

        return (await dbContext.Set<Project>()
            .Where(item => item.Id == projectId)
            .Select(item => item.CurrentPhase)
            .FirstOrDefaultAsync(cancellationToken))?.Trim() ?? string.Empty;
    }
}
