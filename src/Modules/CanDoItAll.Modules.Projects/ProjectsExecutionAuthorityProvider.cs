using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectsExecutionAuthorityProvider
    : IAgentExecutionSourceAuthorityProvider
{
    public string SourceKind => ProjectsAgentChatContextBuilder.SourceKind;

    public ValueTask<AgentExecutionSourceAuthorityDecision> ResolveAsync(
        AgentExecutionSourceAuthorityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Guid.TryParse(request.SourceId.Value, out var projectId) && projectId != Guid.Empty)
        {
            return ValueTask.FromResult(ProjectScopedExecutionAuthority.Resolve(
                request.Agent,
                projectId,
                request.ObservedWorkspaceScope));
        }

        if (request.ObservedWorkspaceScope is not null)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"The projects source '{request.SourceId.Value}' published workspace scope '{request.ObservedWorkspaceScope.DisplayName}' without a selected project.");
        }

        return ValueTask.FromResult(new AgentExecutionSourceAuthorityDecision(
            WorkspaceScopeDescriptor.Sandbox,
            ReadAllowed: true,
            MutationAllowed: false,
            AgentExecutionAuthorityPolicyVersions.Canonical));
    }
}
