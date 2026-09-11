using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration.Runtime;

internal sealed class RetainedRuntimeToolProvider(IAgentRuntimeToolProvider owner) : IAgentRuntimeToolProvider {
    private IReadOnlyList<AITool>? tools;
    private IReadOnlyList<AgentRuntimeToolMetadata>? metadata;

    public int Order => owner.Order;
    public AgentRuntimeToolProviderDescriptor? Descriptor => owner.Descriptor;
    internal int CompositionCount { get; private set; }
    internal AgentRuntimeToolProviderContext? OriginalContext { get; private set; }

    public AgentRuntimeConfiguredWorkspacePolicy GetConfiguredWorkspacePolicy(AgentWorkspaceToolAccessSettings access,
        AgentRuntimeContextIntent intent) => owner.GetConfiguredWorkspacePolicy(access, intent);

    public async ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken) {
        if (tools is null) {
            tools = await owner.CreateToolsAsync(context, cancellationToken);
            metadata = owner.GetToolMetadata(context);
            OriginalContext = context;
            CompositionCount++;
        }
        return tools;
    }

    public IReadOnlyList<AgentRuntimeToolMetadata> GetToolMetadata(AgentRuntimeToolProviderContext context)
        => metadata ?? throw new InvalidOperationException("The actual owner tools must be composed before reading their retained metadata.");
}
