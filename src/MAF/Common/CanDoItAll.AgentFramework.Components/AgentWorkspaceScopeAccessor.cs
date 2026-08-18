using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Components;

public interface IAgentWorkspaceScopeAccessor
{
    ValueTask<WorkspaceScopeDescriptor?> CaptureAsync(
        CancellationToken cancellationToken = default);
}

public sealed class AgentWorkspaceScopeUnavailableException(
    string message,
    Exception innerException) : InvalidOperationException(message, innerException);

public sealed class AgentChatWorkspaceScopeAccessor(
    IAgentChatContextRegistry contextRegistry) : IAgentWorkspaceScopeAccessor
{
    public async ValueTask<WorkspaceScopeDescriptor?> CaptureAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var context = await contextRegistry.CaptureAsync(cancellationToken);
            return context?.Scope.WorkspaceScope;
        }
        catch (Exception exception) when (exception is
            AgentChatContextUnavailableException or
            AgentChatContextPositionMismatchException or
            AgentChatContextPositionUnavailableException)
        {
            throw new AgentWorkspaceScopeUnavailableException(
                exception.Message,
                exception);
        }
    }
}
