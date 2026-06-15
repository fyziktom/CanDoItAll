using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectNodeScopeBridge(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ResourceConnectorPluginRegistry resourceConnectorPluginRegistry) : IProjectNodeScopeBridge
{
    public async Task<ProjectNodeScopeResolution> ResolveAsync(
        Guid projectId,
        ProjectNodeReference nodeReference,
        CancellationToken cancellationToken = default)
    {
        var normalizedNodeKey = nodeReference.NodeKey;

        if (string.Equals(normalizedNodeKey, $"project:{projectId}", StringComparison.Ordinal))
        {
            return new ProjectNodeScopeResolution(true, false, false, ProjectObjectType.ProjectRoot, string.Empty);
        }

        if (TryParsePrefixedGuidNodeKey(normalizedNodeKey, "project:", out var rootProjectId) ||
            TryParsePrefixedGuidNodeKey(normalizedNodeKey, "project-child:", out rootProjectId) ||
            TryParsePrefixedGuidNodeKey(normalizedNodeKey, "project-related-parent:", out rootProjectId))
        {
            return new ProjectNodeScopeResolution(
                rootProjectId == projectId,
                rootProjectId != projectId,
                false,
                ProjectObjectType.ProjectRoot,
                string.Empty);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var projectNode = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId && item.NodeKey == normalizedNodeKey)
            .Select(item => new
            {
                item.ProjectId,
                item.ObjectType,
                item.ObjectSubtype
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (projectNode is not null)
        {
            return new ProjectNodeScopeResolution(true, false, true, projectNode.ObjectType, projectNode.ObjectSubtype);
        }

        var projectedScope = await ResolveProjectedNodeAsync(dbContext, projectId, normalizedNodeKey, cancellationToken);
        if (projectedScope is not null)
        {
            return projectedScope;
        }

        var foreignNode = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.NodeKey == normalizedNodeKey)
            .Select(item => new
            {
                item.ProjectId,
                item.ObjectType,
                item.ObjectSubtype
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (foreignNode is null)
        {
            return new ProjectNodeScopeResolution(false, false, false, null, string.Empty);
        }

        return new ProjectNodeScopeResolution(
            false,
            true,
            true,
            foreignNode.ObjectType,
            foreignNode.ObjectSubtype);
    }

    private async Task<ProjectNodeScopeResolution?> ResolveProjectedNodeAsync(
        AppDbContext dbContext,
        Guid projectId,
        string nodeKey,
        CancellationToken cancellationToken)
    {
        if (TryParsePrefixedGuidNodeKey(nodeKey, "phase:", out var phaseId))
        {
            var phaseProjectId = await dbContext.Set<ProjectPhase>()
                .Where(item => item.Id == phaseId)
                .Select(item => (Guid?)item.ProjectId)
                .FirstOrDefaultAsync(cancellationToken);
            return BuildProjectedResolution(phaseProjectId, projectId, ProjectObjectType.Phase);
        }

        if (TryParsePrefixedGuidNodeKey(nodeKey, "resource:", out var resourceId))
        {
            var resource = await dbContext.Set<ProjectResource>()
                .Where(item => item.Id == resourceId)
                .Select(item => new
                {
                    item.ProjectId,
                    item.ResourceKind,
                    item.ConnectorPluginKey,
                    item.ConfigSchemaVersion
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (resource is null)
            {
                return null;
            }

            var resourceRecord = new ProjectResource
            {
                Id = resourceId,
                ProjectId = resource.ProjectId,
                ResourceKind = resource.ResourceKind,
                ConnectorPluginKey = resource.ConnectorPluginKey,
                ConfigSchemaVersion = resource.ConfigSchemaVersion
            };
            var connectorPlugin = resourceConnectorPluginRegistry.Resolve(resourceRecord);
            return BuildProjectedResolution(
                resource.ProjectId,
                projectId,
                connectorPlugin.ResolveWorkbenchObjectType(resourceRecord),
                connectorPlugin.ResolveWorkbenchObjectSubtype(resourceRecord));
        }

        if (TryParsePrefixedGuidNodeKey(nodeKey, "prompt-run:", out var promptRunId))
        {
            var runProjectId = await dbContext.Set<PromptRun>()
                .Where(item => item.Id == promptRunId)
                .Select(item => (Guid?)item.ProjectId)
                .FirstOrDefaultAsync(cancellationToken);
            return BuildProjectedResolution(runProjectId, projectId, ProjectObjectType.PromptSession);
        }

        if (TryParsePrefixedGuidNodeKey(nodeKey, "prompt-node:", out var promptNodeId))
        {
            var promptNode = await dbContext.Set<PromptRunNode>()
                .Where(item => item.Id == promptNodeId)
                .Select(item => new
                {
                    item.PromptRunId
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (promptNode is null)
            {
                return null;
            }

            var promptRunProjectId = await dbContext.Set<PromptRun>()
                .Where(item => item.Id == promptNode.PromptRunId)
                .Select(item => (Guid?)item.ProjectId)
                .FirstOrDefaultAsync(cancellationToken);
            return BuildProjectedResolution(promptRunProjectId, projectId, ProjectObjectType.PromptStep);
        }

        if (TryParsePrefixedGuidNodeKey(nodeKey, "validation:", out var validationId))
        {
            var validationProjectId = await dbContext.Set<ValidationRun>()
                .Where(item => item.Id == validationId)
                .Select(item => (Guid?)item.ProjectId)
                .FirstOrDefaultAsync(cancellationToken);
            return BuildProjectedResolution(validationProjectId, projectId, ProjectObjectType.ValidationRun);
        }

        if (TryParsePrefixedGuidNodeKey(nodeKey, "process-definition:", out var processDefinitionId))
        {
            var definitionProjectId = await dbContext.Set<ProcessDefinition>()
                .Where(item => item.Id == processDefinitionId)
                .Select(item => item.ProjectId)
                .FirstOrDefaultAsync(cancellationToken);
            if (!definitionProjectId.HasValue)
            {
                return new ProjectNodeScopeResolution(true, false, false, ProjectObjectType.ProcessDefinition, string.Empty);
            }

            return BuildProjectedResolution(definitionProjectId, projectId, ProjectObjectType.ProcessDefinition);
        }

        if (TryParsePrefixedGuidNodeKey(nodeKey, "process-run:", out var processRunId))
        {
            var runProjectId = await dbContext.Set<ProcessRun>()
                .Where(item => item.Id == processRunId)
                .Select(item => item.ProjectId)
                .FirstOrDefaultAsync(cancellationToken);
            return BuildProjectedResolution(runProjectId, projectId, ProjectObjectType.ProcessRun);
        }

        if (TryParsePrefixedGuidNodeKey(nodeKey, "test-plan:", out var testPlanId))
        {
            var testPlanProjectId = await dbContext.Set<TestPlan>()
                .Where(item => item.Id == testPlanId)
                .Select(item => (Guid?)item.ProjectId)
                .FirstOrDefaultAsync(cancellationToken);
            return BuildProjectedResolution(testPlanProjectId, projectId, ProjectObjectType.TestPlan);
        }

        return null;
    }

    private static ProjectNodeScopeResolution? BuildProjectedResolution(
        Guid? ownerProjectId,
        Guid projectId,
        ProjectObjectType objectType,
        string objectSubtype = "")
    {
        if (!ownerProjectId.HasValue)
        {
            return null;
        }

        return new ProjectNodeScopeResolution(
            ownerProjectId.Value == projectId,
            ownerProjectId.Value != projectId,
            false,
            objectType,
            objectSubtype);
    }

    private static bool TryParsePrefixedGuidNodeKey(string nodeKey, string prefix, out Guid projectId)
    {
        if (nodeKey.StartsWith(prefix, StringComparison.Ordinal) &&
            Guid.TryParse(nodeKey[prefix.Length..], out projectId))
        {
            return true;
        }

        projectId = Guid.Empty;
        return false;
    }
}
