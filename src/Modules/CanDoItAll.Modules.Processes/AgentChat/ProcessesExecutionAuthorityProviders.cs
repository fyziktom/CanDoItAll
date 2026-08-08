using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes.AgentChat;

internal sealed class ProcessesExecutionAuthorityProvider
    : IAgentExecutionSourceAuthorityProvider
{
    public string SourceKind => ProcessAgentChatContextBuilder.WorkspaceSourceKind;

    public ValueTask<AgentExecutionSourceAuthorityDecision> ResolveAsync(
        AgentExecutionSourceAuthorityRequest request,
        CancellationToken cancellationToken = default)
        => ProcessesExecutionAuthority.ResolveAsync(request);
}

internal sealed class LiveProcessesExecutionAuthorityProvider
    : IAgentExecutionSourceAuthorityProvider
{
    public string SourceKind => ProcessAgentChatContextBuilder.LiveSourceKind;

    public ValueTask<AgentExecutionSourceAuthorityDecision> ResolveAsync(
        AgentExecutionSourceAuthorityRequest request,
        CancellationToken cancellationToken = default)
        => ProcessesExecutionAuthority.ResolveAsync(request);
}

internal static class ProcessesExecutionAuthority
{
    private const string ProjectSegmentMarker = ":project:";

    public static ValueTask<AgentExecutionSourceAuthorityDecision> ResolveAsync(
        AgentExecutionSourceAuthorityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceId = request.SourceId.Value;
        var projectMarkerIndex = sourceId.IndexOf(ProjectSegmentMarker, StringComparison.OrdinalIgnoreCase);
        if (projectMarkerIndex >= 0 &&
            Guid.TryParse(sourceId[(projectMarkerIndex + ProjectSegmentMarker.Length)..], out var projectId) &&
            projectId != Guid.Empty)
        {
            if (request.ObservedWorkspaceScope is { Kind: WorkspaceScopeKind.Process })
            {
                throw new AgentExecutionAuthorityMismatchException(
                    "A process-run workspace scope has no canonical per-run authority rule; run process work through the governed process execution path.");
            }

            return ValueTask.FromResult(ProjectScopedExecutionAuthority.Resolve(
                request.Agent,
                projectId,
                request.ObservedWorkspaceScope));
        }

        if (request.ObservedWorkspaceScope is not null)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"The processes source '{sourceId}' published workspace scope '{request.ObservedWorkspaceScope.DisplayName}', which has no canonical authority rule for a global processes surface.");
        }

        return ValueTask.FromResult(new AgentExecutionSourceAuthorityDecision(
            WorkspaceScopeDescriptor.Sandbox,
            ReadAllowed: true,
            MutationAllowed: false,
            AgentExecutionAuthorityPolicyVersions.Canonical));
    }
}
