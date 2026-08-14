using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafFinalizerToolFactory
{
    public static FinalizerCapture? CreateCapture(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode)
    {
        if (finalizerMode == AgentFinalizerMode.Disabled ||
            !AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var policy))
        {
            return null;
        }

        var capture = new FinalizerCapture(policy);
        var tool = CreateTool(capture, policy);
        if (tool is null)
        {
            return null;
        }

        capture.Tools.Add(tool);
        return capture;
    }

    /// <summary>
    /// Single generic path for every finalizer contract: the submit delegate and JSON-schema shape are entirely
    /// driven by <see cref="AgentFinalizerPolicy"/> metadata (<see cref="AgentFinalizerPolicy.ToolDescription"/>,
    /// <see cref="AgentFinalizerPolicy.ResultParameterDescription"/>), never by a per-type switch. This keeps the
    /// generic finalizer mechanism free of any specific structured-output contract's identity.
    /// </summary>
    private static AIFunction CreateTool(FinalizerCapture capture, AgentFinalizerPolicy policy)
    {
        var options = new AIFunctionFactoryOptions
        {
            Name = policy.ToolName,
            Description = policy.ToolDescription,
            SerializerOptions = AgentOutputJson.SerializerOptions
        };
        var resultSchema = CreateTolerantResultSchema(policy);
        var resultParameterDescription = policy.ResultParameterDescription;
        if (resultParameterDescription is not null || resultSchema is not null)
        {
            options.JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions
            {
                ParameterDescriptionProvider = parameter =>
                    string.Equals(parameter.Name, "result", StringComparison.Ordinal)
                        ? resultParameterDescription
                        : null,
                TransformSchemaNode = resultSchema is null
                    ? null
                    : (context, schema) => context.TypeInfo.Type == typeof(JsonElement)
                        ? resultSchema.DeepClone()
                        : schema
            };
        }

        var function = AIFunctionFactory.Create(capture.CreateSubmitDelegate(), options);
        return new ExactFinalizerArgumentsAIFunction(function, policy);
    }

    private static JsonNode? CreateTolerantResultSchema(AgentFinalizerPolicy policy)
    {
        if (policy.KnownOutputNormalizer is null)
        {
            return null;
        }

        var schema = AIJsonUtilities.CreateJsonSchema(
            policy.OutputType,
            policy.ResultParameterDescription,
            hasDefaultValue: false,
            defaultValue: null,
            AgentOutputJson.SerializerOptions,
            new AIJsonSchemaCreateOptions());
        return JsonNode.Parse(schema.GetRawText())
            ?? throw new InvalidOperationException(
                $"Finalizer result schema for '{policy.ToolName}' could not be materialized.");
    }
}
