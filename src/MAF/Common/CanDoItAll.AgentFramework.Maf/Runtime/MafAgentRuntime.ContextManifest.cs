using System.Reflection;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private static AgentRuntimeContextAssemblyManifest CreateContextAssemblyManifest(
        AgentDefinition agent,
        ProviderProfile provider,
        string model,
        AgentRuntimeExecutionOptions runtimeOptions,
        RuntimeBuildResult runtimeBuild,
        IReadOnlyList<ChatMessage> inputMessages)
    {
        var capabilityState = runtimeBuild.CapabilityState;
        var toolCount = capabilityState?.Tools.Count ?? 0;
        var toolSchemaChars = EstimateToolSchemaChars(capabilityState?.Tools ?? []);
        var inputChars = inputMessages.Sum(message => message.Text?.Length ?? 0);
        var instructionChars = agent.Instructions?.Length ?? 0;
        var inputTokens = EstimateTokens(inputChars);
        var toolSchemaTokens = EstimateTokens(toolSchemaChars);
        var sources = new List<AgentRuntimeContextManifestSource>();

        if (instructionChars > 0)
        {
            sources.Add(AgentRuntimeContextManifestSource.Included(
                AgentRuntimeContextSourceCategories.AgentInstructions,
                "agent-instructions",
                "agent instruction text attached to the provider call",
                1,
                instructionChars));
        }

        sources.Add(AgentRuntimeContextManifestSource.Included(
                AgentRuntimeContextSourceCategories.InputMessages,
                "runtime-input",
                "messages passed to Microsoft Agent Framework for this provider call",
                inputMessages.Count,
                inputChars));

        if (capabilityState is not null)
        {
            sources.AddRange(capabilityState.ContextSources);
            foreach (var frameworkToolName in capabilityState.FrameworkToolNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                sources.Add(AgentRuntimeContextManifestSource.Included(
                    AgentRuntimeContextSourceCategories.FrameworkTool,
                    frameworkToolName,
                    "framework tool exposed by an attached context provider",
                    1,
                    frameworkToolName.Length));
            }
        }

        var totals = new AgentRuntimeContextManifestTotals(
            InputMessageCount: inputMessages.Count,
            InputMessageChars: inputChars,
            InputMessageEstimatedTokens: inputTokens,
            ToolCount: toolCount,
            ToolSchemaEstimatedChars: toolSchemaChars,
            ToolSchemaEstimatedTokens: toolSchemaTokens,
            ContextProviderCount: capabilityState?.ContextProviders.Count ?? 0,
            FrameworkToolCount: capabilityState?.FrameworkToolNames.Count ?? 0,
            RuntimeToolProviderCount: capabilityState?.RuntimeToolProviderDescriptors.Count ?? 0,
            EstimatedInputTokens: sources.Sum(source => source.EstimatedTokens));

        return new AgentRuntimeContextAssemblyManifest(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            agent.Id,
            agent.Name,
            provider.Name,
            provider.Kind,
            model,
            provider.Transport,
            runtimeOptions.ContextIntent ?? AgentRuntimeContextIntent.Empty,
            totals,
            sources
                .OrderBy(source => source.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(source => source.SourceId, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private static int EstimateToolSchemaChars(IReadOnlyList<AITool> tools)
        => tools.Sum(EstimateToolSchemaChars);

    private static int EstimateToolSchemaChars(AITool tool)
    {
        var name = tool.Name ?? string.Empty;
        var description = ResolvePublicStringProperty(tool, "Description") ?? string.Empty;
        return name.Length + description.Length + tool.GetType().Name.Length + 128;
    }

    private static string? ResolvePublicStringProperty(object instance, string propertyName)
        => instance
            .GetType()
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(instance) as string;

    private static int EstimateTokens(int chars)
        => chars <= 0 ? 0 : Math.Max(1, chars / 4);
}
