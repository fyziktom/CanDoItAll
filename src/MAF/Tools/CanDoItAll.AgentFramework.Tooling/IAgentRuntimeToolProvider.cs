using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Tooling;

public interface IAgentRuntimeToolProvider
{
    int Order { get; }

    AgentRuntimeToolProviderDescriptor? Descriptor => null;

    AgentRuntimeConfiguredWorkspacePolicy GetConfiguredWorkspacePolicy(
        AgentWorkspaceToolAccessSettings workspaceToolAccess,
        AgentRuntimeContextIntent contextIntent)
        => throw new InvalidOperationException("This runtime tool provider does not define configured-workspace capability policy.");

    ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
        AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken);

    IReadOnlyList<AgentRuntimeToolMetadata> GetToolMetadata(
        AgentRuntimeToolProviderContext context)
        => [];
}
