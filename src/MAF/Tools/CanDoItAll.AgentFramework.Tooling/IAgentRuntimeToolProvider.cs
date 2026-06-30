using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Tooling;

public interface IAgentRuntimeToolProvider
{
    int Order { get; }

    AgentRuntimeToolProviderDescriptor? Descriptor => null;

    ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
        AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken);

    IReadOnlyList<AgentRuntimeToolMetadata> GetToolMetadata(
        AgentRuntimeToolProviderContext context)
        => [];
}
