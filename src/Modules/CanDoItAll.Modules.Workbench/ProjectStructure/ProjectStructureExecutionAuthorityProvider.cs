using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workbench.ProjectStructure;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureExecutionAuthorityProvider
    : IAgentExecutionSourceAuthorityProvider
{
    public string SourceKind => ProjectStructureAgentChatContextBuilder.SourceKind;

    public ValueTask<AgentExecutionSourceAuthorityDecision> ResolveAsync(
        AgentExecutionSourceAuthorityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Guid.TryParse(request.SourceId.Value, out var projectId) || projectId == Guid.Empty)
        {
            throw new AgentExecutionAuthorityMismatchException(
                "The project-structure source id is not a valid project identifier.");
        }

        return ValueTask.FromResult(ProjectScopedExecutionAuthority.Resolve(
            request.Agent,
            projectId,
            request.ObservedWorkspaceScope));
    }
}
