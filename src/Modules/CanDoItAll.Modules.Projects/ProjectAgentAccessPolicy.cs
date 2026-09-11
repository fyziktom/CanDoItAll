using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Projects;

public static class ProjectAgentAccessPolicy {
    public static IReadOnlyList<ContextualAgentAccessSummary> Resolve(IEnumerable<AgentDefinition> agents, Guid? projectId = null)
        => ContextualAgentAccessResolver.Resolve(agents, agent => Resolve(agent, projectId));

    public static ContextualAgentAccessSummary? Resolve(
        AgentDefinition agent,
        Guid? projectId) {
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        var accessLevel = ContextualAgentAccessResolver.ResolveAccessLevel(access.CanRead, access.CanWrite);
        if (access.CanWriteNonTaskStructure) {
            accessLevel |= ContextualAgentAccessLevel.Read | ContextualAgentAccessLevel.NonTaskStructureWrite;
        }
        if (access.CanWriteTasks) {
            accessLevel |= ContextualAgentAccessLevel.Read | ContextualAgentAccessLevel.TaskWrite;
        }
        if (access.CanCreateProjects) {
            accessLevel |= ContextualAgentAccessLevel.Read | ContextualAgentAccessLevel.ProjectCreate;
        }
        if (access.CanCreateSubprojects) {
            accessLevel |= ContextualAgentAccessLevel.Read | ContextualAgentAccessLevel.SubprojectCreate;
        }
        if (accessLevel == ContextualAgentAccessLevel.None) {
            return null;
        }

        if (!access.AllowAllProjects) {
            if (projectId.HasValue && !access.AllowedProjectIds.Contains(projectId.Value)) {
                return null;
            }

            if (!projectId.HasValue &&
                access.AllowedProjectIds.Count == 0 &&
                !access.CanCreateProjects) {
                return null;
            }
        }

        var scopeLabel = access.AllowAllProjects
            ? "All projects"
            : projectId.HasValue
                ? "This project"
                : access.AllowedProjectIds.Count > 0
                    ? access.AllowedProjectIds.Count == 1 ? "1 project" : $"{access.AllowedProjectIds.Count} projects"
                    : "Project creation only";

        return new ContextualAgentAccessSummary(agent, accessLevel, scopeLabel);
    }

    public static AgentExecutionSourceAuthorityDecision ResolveExecutionAuthority(
        AgentDefinition agent,
        Guid projectId,
        WorkspaceScopeDescriptor? observedScope) {
        ArgumentNullException.ThrowIfNull(agent);
        if (projectId == Guid.Empty) {
            throw new ArgumentException("A project id is required.", nameof(projectId));
        }

        var canonicalScope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"));
        if (observedScope is not null && observedScope != canonicalScope) {
            throw new AgentExecutionAuthorityMismatchException(
                $"The published workspace scope '{observedScope.DisplayName}' does not match the canonical project scope '{canonicalScope.DisplayName}'.");
        }

        var summary = Resolve([agent], projectId)
            .FirstOrDefault();
        if (summary is null || !summary.CanRead) {
            throw new AgentChatContextAccessDeniedException(agent.Id, default);
        }

        return new AgentExecutionSourceAuthorityDecision(
            canonicalScope,
            ReadAllowed: true,
            MutationAllowed: summary.CanMutate,
            AgentExecutionAuthorityPolicyVersions.Canonical);
    }
}
