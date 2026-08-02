using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Tests.Unit;

public sealed class OpenAiRequestCompatibilityPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(AgentReasoningEffortLevel.Medium)]
    [InlineData(AgentReasoningEffortLevel.Max)]
    public void Terra_chat_completions_function_tools_require_explicit_none(
        AgentReasoningEffortLevel? requestedEffort)
    {
        var resolution = OpenAiRequestCompatibilityPolicy.ResolveReasoningEffort(
            ProviderKind.OpenAi,
            ProviderTransportKind.ChatCompletions,
            OpenAiModelIds.Gpt56Terra,
            ProviderInvocationFeatures.FunctionTools,
            requestedEffort);

        Assert.Equal(requestedEffort, resolution.RequestedEffort);
        Assert.Equal(AgentReasoningEffortLevel.None, resolution.EffectiveEffort);
        Assert.Equal(
            ProviderModelParameterAdjustment.ReasoningDisabledForFunctionTools,
            resolution.Adjustment);
    }

    [Fact]
    public void Already_disabled_reasoning_needs_no_adjustment()
    {
        var resolution = OpenAiRequestCompatibilityPolicy.ResolveReasoningEffort(
            ProviderKind.OpenAi,
            ProviderTransportKind.ChatCompletions,
            OpenAiModelIds.Gpt56Terra,
            ProviderInvocationFeatures.FunctionTools,
            AgentReasoningEffortLevel.None);

        Assert.Equal(AgentReasoningEffortLevel.None, resolution.EffectiveEffort);
        Assert.Equal(ProviderModelParameterAdjustment.None, resolution.Adjustment);
    }

    [Theory]
    [InlineData(ProviderKind.OpenAi, ProviderTransportKind.Responses, OpenAiModelIds.Gpt56Terra, ProviderInvocationFeatures.FunctionTools)]
    [InlineData(ProviderKind.OpenAi, ProviderTransportKind.ChatCompletions, OpenAiModelIds.Gpt56Terra, ProviderInvocationFeatures.None)]
    [InlineData(ProviderKind.OpenAi, ProviderTransportKind.ChatCompletions, OpenAiModelIds.Gpt56Sol, ProviderInvocationFeatures.FunctionTools)]
    [InlineData(ProviderKind.AzureOpenAi, ProviderTransportKind.ChatCompletions, OpenAiModelIds.Gpt56Terra, ProviderInvocationFeatures.FunctionTools)]
    public void Unproven_request_shapes_preserve_requested_effort(
        ProviderKind providerKind,
        ProviderTransportKind transport,
        string model,
        ProviderInvocationFeatures features)
    {
        var resolution = OpenAiRequestCompatibilityPolicy.ResolveReasoningEffort(
            providerKind,
            transport,
            model,
            features,
            AgentReasoningEffortLevel.High);

        Assert.Equal(AgentReasoningEffortLevel.High, resolution.EffectiveEffort);
        Assert.Equal(ProviderModelParameterAdjustment.None, resolution.Adjustment);
    }
}
