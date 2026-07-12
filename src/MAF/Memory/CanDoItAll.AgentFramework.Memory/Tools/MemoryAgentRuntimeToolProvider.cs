using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Memory.Application;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Memory.Tools;

public sealed class MemoryAgentRuntimeToolProvider : IAgentRuntimeToolProvider
{
    public const string ProviderKey = "memory.runtime-tools";

    private const int ProviderOrder = 925;

    private readonly MemoryAgentQueryTools queryTools;
    private readonly MemoryAgentStatusTool statusTool;

    public MemoryAgentRuntimeToolProvider(
        IMemoryOperationHandler operationHandler,
        TimeProvider timeProvider)
    {
        queryTools = new MemoryAgentQueryTools(operationHandler, timeProvider);
        statusTool = new MemoryAgentStatusTool(operationHandler, timeProvider);
    }

    public int Order => ProviderOrder;

    public AgentRuntimeToolProviderDescriptor Descriptor { get; } = new(
        ProviderKey,
        "Memory runtime tools",
        "Provides generic Memory Protocol v1 tools backed by typed agent memory settings.",
        ["agent-framework", "memory"],
        [
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive
        ]);

    public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
        AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var access = AgentMemoryAccessMetadata.Read(context.Agent.ConfigurationJson);
        if (access.InvocationMode != AgentMemoryInvocationMode.Automatic ||
            !access.CanUseMemoryTools ||
            !context.Agent.Permissions.CanUseTools)
        {
            return ValueTask.FromResult<IReadOnlyList<AITool>>([]);
        }

        var providerAliases = string.Join(
            ", ",
            access.ProviderBindings.Select(binding => binding.Alias.Value));
        var providerHint = providerAliases.Length == 0
            ? "No provider aliases are configured."
            : $"Configured provider aliases: {providerAliases}.";
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                (MemoryContextQueryToolInput input, CancellationToken token = default) =>
                    queryTools.QueryAsync(context, access, input, token),
                MemoryAgentRuntimeToolNames.ContextQuery,
                "Queries a bound memory provider. ProviderInstanceId accepts an alias or exact bound id. " + providerHint),
            AIFunctionFactory.Create(
                (MemoryOperationStatusToolInput input, CancellationToken token = default) =>
                    statusTool.GetStatusAsync(context, access, input, token),
                MemoryAgentRuntimeToolNames.OperationStatus,
                "Reads the status and persisted final result of a memory query owned by this agent context.")
        };

        return ValueTask.FromResult<IReadOnlyList<AITool>>(tools);
    }

    public IReadOnlyList<AgentRuntimeToolMetadata> GetToolMetadata(
        AgentRuntimeToolProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var access = AgentMemoryAccessMetadata.Read(context.Agent.ConfigurationJson);
        if (access.InvocationMode != AgentMemoryInvocationMode.Automatic ||
            !access.CanUseMemoryTools ||
            !context.Agent.Permissions.CanUseTools)
        {
            return [];
        }

        return
        [
            CreateMetadata(MemoryAgentRuntimeToolNames.ContextQuery, AgentRuntimeToolOperationKind.Read),
            CreateMetadata(MemoryAgentRuntimeToolNames.OperationStatus, AgentRuntimeToolOperationKind.Read)
        ];
    }

    private static AgentRuntimeToolMetadata CreateMetadata(
        string toolName,
        AgentRuntimeToolOperationKind operationKind)
    {
        return new AgentRuntimeToolMetadata(
            ProviderKey,
            toolName,
            operationKind,
            requiresApprovalByDefault: false,
            ["memory", "generic-memory"]);
    }
}
