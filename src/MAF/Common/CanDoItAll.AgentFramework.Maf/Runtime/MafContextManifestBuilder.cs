using System.Reflection;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafContextManifestBuilder
{
    public static AgentRuntimeContextAssemblyManifest Create(
        AgentDefinition agent,
        ProviderProfile provider,
        string model,
        AgentRuntimeExecutionOptions runtimeOptions,
        IReadOnlyList<AITool> tools,
        IReadOnlyList<AgentRuntimeContextManifestSource> contextSources,
        IReadOnlyCollection<string> frameworkToolNames,
        int contextProviderCount,
        int runtimeToolProviderCount,
        IReadOnlyList<ChatMessage> inputMessages)
    {
        var toolSchemaChars = EstimateToolSchemaChars(tools);
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

        sources.AddRange(contextSources);
        foreach (var frameworkToolName in frameworkToolNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            sources.Add(AgentRuntimeContextManifestSource.Included(
                AgentRuntimeContextSourceCategories.FrameworkTool,
                frameworkToolName,
                "framework tool exposed by an attached context provider",
                1,
                frameworkToolName.Length));
        }

        var totals = new AgentRuntimeContextManifestTotals(
            InputMessageCount: inputMessages.Count,
            InputMessageChars: inputChars,
            InputMessageEstimatedTokens: inputTokens,
            ToolCount: tools.Count,
            ToolSchemaEstimatedChars: toolSchemaChars,
            ToolSchemaEstimatedTokens: toolSchemaTokens,
            ContextProviderCount: contextProviderCount,
            FrameworkToolCount: frameworkToolNames.Count,
            RuntimeToolProviderCount: runtimeToolProviderCount,
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

    public static int EstimateToolSchemaChars(IReadOnlyList<AITool> tools)
        => tools.Sum(EstimateToolSchemaChars);

    public static int EstimateToolSchemaChars(AITool tool)
    {
        var name = tool.Name ?? string.Empty;
        var description = ResolvePublicStringProperty(tool, "Description") ?? string.Empty;
        var parameterSchema = tool is AIFunction function
            ? function.JsonSchema.GetRawText()
            : string.Empty;
        return name.Length +
               description.Length +
               parameterSchema.Length +
               tool.GetType().Name.Length +
               128;
    }

    private static string? ResolvePublicStringProperty(object instance, string propertyName)
        => instance
            .GetType()
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(instance) as string;

    private static int EstimateTokens(int chars)
        => chars <= 0 ? 0 : Math.Max(1, chars / 4);
}
