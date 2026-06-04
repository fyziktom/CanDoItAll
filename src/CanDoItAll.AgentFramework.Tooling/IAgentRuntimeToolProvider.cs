using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Tooling;

public interface IAgentRuntimeToolProvider
{
    int Order { get; }

    ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
        AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken);
}
