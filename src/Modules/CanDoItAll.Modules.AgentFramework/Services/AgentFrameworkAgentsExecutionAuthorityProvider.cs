using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentFrameworkAgentsExecutionAuthorityProvider
    : IAgentExecutionSourceAuthorityProvider
{
    public string SourceKind => AgentFrameworkAgentsChatContextBuilder.SourceKind;

    public ValueTask<AgentExecutionSourceAuthorityDecision> ResolveAsync(
        AgentExecutionSourceAuthorityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.ObservedWorkspaceScope is not null)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"The agents source '{request.SourceId.Value}' cannot publish workspace scope '{request.ObservedWorkspaceScope.DisplayName}'.");
        }

        return ValueTask.FromResult(new AgentExecutionSourceAuthorityDecision(
            WorkspaceScopeDescriptor.Sandbox,
            ReadAllowed: true,
            MutationAllowed: request.Agent.Permissions.CanUseTools,
            CanonicalAgentExecutionAuthorityResolver.CanonicalPolicyVersion));
    }
}
