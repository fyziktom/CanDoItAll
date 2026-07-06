using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafRuntimeExecutionOptionsResolver
{
    private static readonly ProviderProfileService ProviderFeatureService = new();

    public static AgentRuntimeExecutionOptions Normalize(
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeExecutionOptions? executionOptions)
    {
        if (executionOptions is null)
        {
            return CreateDisabled(structuredOutput);
        }

        if (structuredOutput is not null &&
            executionOptions.StructuredOutput is not null &&
            !string.Equals(executionOptions.StructuredOutput.ContractKey, structuredOutput.ContractKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Runtime execution options contract '{executionOptions.StructuredOutput.ContractKey}' does not match the requested structured output contract '{structuredOutput.ContractKey}'.");
        }

        return executionOptions.StructuredOutput is null && structuredOutput is not null
            ? executionOptions with { StructuredOutput = structuredOutput }
            : executionOptions;
    }

    public static AgentRuntimeExecutionOptions CreateDisabled(
        AgentStructuredOutputContract? structuredOutput)
    {
        return new AgentRuntimeExecutionOptions(
            StructuredOutput: structuredOutput,
            FinalizerMode: AgentFinalizerMode.Disabled,
            RequireStructuredOutputValidation: true,
            MaxStructuredOutputRepairAttempts: 0);
    }

    public static void EnsureStructuredOutputCapability(
        ProviderProfile provider,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        if (runtimeOptions.StructuredOutput is not null)
        {
            if (runtimeOptions.FinalizerMode == AgentFinalizerMode.Required &&
                AgentFinalizerPolicies.TryResolveForStructuredOutput(runtimeOptions.StructuredOutput, out _))
            {
                return;
            }

            EnsureStructuredOutputCapability(provider, runtimeOptions.StructuredOutput);
            return;
        }

        if (!runtimeOptions.RequireJsonResponseFormat)
        {
            return;
        }

        var featureMatrix = ProviderFeatureService.ResolveFeatureMatrix(provider);
        if (featureMatrix.SupportsStructuredOutput)
        {
            return;
        }

        var schemaName = string.IsNullOrWhiteSpace(runtimeOptions.ResponseFormatSchemaName)
            ? "JSON"
            : runtimeOptions.ResponseFormatSchemaName;
        throw new InvalidOperationException(
            $"Provider '{provider.Name}' using transport '{provider.Transport}' cannot enforce workflow JSON response format '{schemaName}'. Choose a structured-output capable OpenAI/Azure OpenAI provider or use a non-JSON workflow component.");
    }

    private static void EnsureStructuredOutputCapability(
        ProviderProfile provider,
        AgentStructuredOutputContract? structuredOutput)
    {
        if (structuredOutput is null)
        {
            return;
        }

        var featureMatrix = ProviderFeatureService.ResolveFeatureMatrix(provider);
        if (featureMatrix.SupportsStructuredOutput)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Provider '{provider.Name}' using transport '{provider.Transport}' cannot enforce structured output contract '{structuredOutput.ContractKey}'. Choose a structured-output capable OpenAI/Azure OpenAI provider or disable the machine-critical structured-output request.");
    }
}
