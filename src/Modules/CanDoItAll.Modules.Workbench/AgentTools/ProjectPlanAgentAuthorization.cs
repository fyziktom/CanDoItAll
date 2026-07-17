using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench;

public static class ProjectPlanAgentCapabilityKeys
{
    public const string AnalysisSkill = "project-plan-analysis-inline-skill";
    public const string SummaryTool = "project-plan-summary-get";
}

public static class ProjectPlanAgentAuthorizationPolicy
{
    public static bool IsPlanSummaryAuthorized(
        AgentDefinition agent,
        IReadOnlyList<CapabilityCatalogItem> capabilityCatalog)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(capabilityCatalog);

        return agent.Status == AgentLifecycleStatus.Active &&
            !agent.IsTemplate &&
            agent.Permissions.CanUseTools &&
            HasExactCapability(agent, capabilityCatalog, ProjectPlanAgentCapabilityKeys.AnalysisSkill, CapabilityKind.Skill) &&
            HasExactCapability(agent, capabilityCatalog, ProjectPlanAgentCapabilityKeys.SummaryTool, CapabilityKind.Tool);
    }

    private static bool HasExactCapability(
        AgentDefinition agent,
        IReadOnlyList<CapabilityCatalogItem> capabilityCatalog,
        string capabilityKey,
        CapabilityKind kind)
    {
        AgentCapabilityAssignment? assignment = null;
        foreach (var candidate in agent.Capabilities)
        {
            if (candidate.Kind != kind ||
                !string.Equals(candidate.CapabilityKey, capabilityKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (assignment is not null)
            {
                return false;
            }

            assignment = candidate;
        }

        if (assignment is null)
        {
            return false;
        }

        var catalogMatchCount = 0;
        foreach (var capability in capabilityCatalog)
        {
            if (capability.Id != assignment.CapabilityId ||
                capability.Kind != kind ||
                !string.Equals(capability.Key, capabilityKey, StringComparison.Ordinal))
            {
                continue;
            }

            catalogMatchCount++;
            if (catalogMatchCount > 1)
            {
                return false;
            }
        }

        return catalogMatchCount == 1;
    }
}

public readonly record struct ProjectStructureNodesToSubprojectAuthorization(
    bool RequiresNonTaskWriteGuard);

public sealed class ProjectStructureAgentAuthorizationService(
    IAgentFrameworkWorkspaceService workspaceService)
{
    public async Task EnsureProjectCreationAuthorizedAsync(
        Guid agentId,
        CancellationToken cancellationToken)
    {
        var agent = await LoadActorAsync(agentId, cancellationToken);
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        if (!access.CanCreateProjects)
        {
            throw CreateDeniedException(AgentToolInvocationPolicyMetadata.ProjectStructureProjectCreate);
        }
    }

    public async Task EnsureSubprojectCreationAuthorizedAsync(
        Guid agentId,
        Guid parentProjectId,
        CancellationToken cancellationToken)
    {
        var agent = await LoadActorAsync(agentId, cancellationToken);
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        if (!access.CanCreateSubprojects || !IsProjectAllowed(access, parentProjectId))
        {
            throw CreateDeniedException(AgentToolInvocationPolicyMetadata.ProjectStructureSubprojectCreate);
        }
    }

    public async Task EnsureSubprojectLinkAuthorizedAsync(
        Guid agentId,
        Guid parentProjectId,
        Guid childProjectId,
        Guid? currentParentProjectId,
        CancellationToken cancellationToken)
    {
        var agent = await LoadActorAsync(agentId, cancellationToken);
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        if (!access.CanCreateSubprojects ||
            !ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(access) ||
            !IsProjectAllowed(access, parentProjectId) ||
            !IsProjectAllowed(access, childProjectId) ||
            currentParentProjectId.HasValue && !IsProjectAllowed(access, currentParentProjectId.Value))
        {
            throw CreateDeniedException(AgentToolInvocationPolicyMetadata.ProjectStructureSubprojectLink);
        }
    }

    public async Task<ProjectStructureNodesToSubprojectAuthorization> EnsureNodesToNewSubprojectAuthorizedAsync(
        Guid agentId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var agent = await LoadActorAsync(agentId, cancellationToken);
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        if (!access.CanCreateSubprojects ||
            !ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(access) ||
            !IsProjectAllowed(access, projectId))
        {
            throw CreateDeniedException(AgentToolInvocationPolicyMetadata.ProjectStructureNodesToNewSubproject);
        }

        return new ProjectStructureNodesToSubprojectAuthorization(
            RequiresNonTaskWriteGuard: access.CanWriteNonTaskStructure && !access.CanWrite);
    }

    public async Task GrantCreatedProjectAccessAsync(
        Guid agentId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var agent = agents.FirstOrDefault(item => item.Id == agentId)
            ?? throw CreateDeniedException("project-structure.access-grant");
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        if (access.AllowAllProjects || access.AllowedProjectIds.Contains(projectId))
        {
            return;
        }

        await workspaceService.GrantAgentProjectStructureAccessAsync(agentId, projectId, cancellationToken);
    }

    public async Task RevokeCreatedProjectAccessAsync(
        Guid agentId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var agent = agents.FirstOrDefault(item => item.Id == agentId)
            ?? throw CreateDeniedException("project-structure.access-revoke");
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        if (access.AllowAllProjects || !access.AllowedProjectIds.Contains(projectId))
        {
            return;
        }

        await workspaceService.RevokeAgentProjectStructureAccessAsync(agentId, projectId, cancellationToken);
    }

    public async Task EnsurePlanSummaryAuthorizedAsync(
        Guid agentId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var agent = await LoadActorAsync(agentId, cancellationToken);
        var capabilities = await workspaceService.ListCapabilitiesAsync(cancellationToken);
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        if (!access.CanRead ||
            !IsProjectAllowed(access, projectId) ||
            !ProjectPlanAgentAuthorizationPolicy.IsPlanSummaryAuthorized(agent, capabilities))
        {
            throw CreateDeniedException(AgentToolInvocationPolicyMetadata.ProjectPlanSummaryGet);
        }
    }

    public async Task EnsureTaskWriteAuthorizedAsync(
        Guid agentId,
        Guid projectId,
        string toolName,
        CancellationToken cancellationToken)
    {
        var agent = await LoadActorAsync(agentId, cancellationToken);
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        if ((!access.CanWrite && !access.CanWriteTasks) || !IsProjectAllowed(access, projectId))
        {
            throw CreateDeniedException(toolName);
        }
    }

    private async Task<AgentDefinition> LoadActorAsync(
        Guid agentId,
        CancellationToken cancellationToken)
    {
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var agent = agents.FirstOrDefault(item => item.Id == agentId);
        if (agent is null ||
            agent.Status != AgentLifecycleStatus.Active ||
            agent.IsTemplate ||
            !agent.Permissions.CanUseTools)
        {
            throw CreateDeniedException("project-structure");
        }

        return agent;
    }

    private static bool IsProjectAllowed(AgentProjectStructureAccessSettings access, Guid projectId)
    {
        return access.AllowAllProjects || access.AllowedProjectIds.Contains(projectId);
    }

    private static ProjectStructureAgentException CreateDeniedException(string toolName)
    {
        return new ProjectStructureAgentException(
            403,
            "AgentToolAccessDenied",
            $"The current agent is not authorized to invoke '{toolName}' for this project.");
    }
}
