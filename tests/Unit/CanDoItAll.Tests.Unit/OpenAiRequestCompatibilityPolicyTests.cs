using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Tests.Unit.AgentFramework;

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
            ProviderRequestCompatibilityDisposition.Adjusted,
            resolution.Disposition);
        Assert.Equal(
            ProviderModelParameterAdjustment.ReasoningDisabledForFunctionTools,
            resolution.Adjustment);
    }

    [Fact]
    public void Luna_chat_completions_function_tools_require_explicit_none()
    {
        var resolution = OpenAiRequestCompatibilityPolicy.ResolveReasoningEffort(
            ProviderKind.OpenAi,
            ProviderTransportKind.ChatCompletions,
            OpenAiModelIds.Gpt56Luna,
            ProviderInvocationFeatures.FunctionTools,
            AgentReasoningEffortLevel.Medium);

        Assert.Equal(AgentReasoningEffortLevel.Medium, resolution.RequestedEffort);
        Assert.Equal(AgentReasoningEffortLevel.None, resolution.EffectiveEffort);
        Assert.Equal(
            ProviderRequestCompatibilityDisposition.Adjusted,
            resolution.Disposition);
        Assert.Equal(
            ProviderModelParameterAdjustment.ReasoningDisabledForFunctionTools,
            resolution.Adjustment);
    }

    [Theory]
    [InlineData(OpenAiModelIds.Gpt54Mini)]
    [InlineData("gpt-5.4-mini-2026-08-01")]
    [InlineData("gpt-5.6-luna-2026-08-01")]
    [InlineData("gpt-5.6-terra-2026-08-01")]
    public void Additional_affected_models_require_explicit_none(string model)
    {
        var resolution = OpenAiRequestCompatibilityPolicy.ResolveReasoningEffort(
            ProviderKind.OpenAi,
            ProviderTransportKind.ChatCompletions,
            model,
            ProviderInvocationFeatures.FunctionTools,
            AgentReasoningEffortLevel.Medium);

        Assert.Equal(AgentReasoningEffortLevel.Medium, resolution.RequestedEffort);
        Assert.Equal(AgentReasoningEffortLevel.None, resolution.EffectiveEffort);
        Assert.Equal(
            ProviderRequestCompatibilityDisposition.Adjusted,
            resolution.Disposition);
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
        Assert.Equal(
            ProviderRequestCompatibilityDisposition.Preserved,
            resolution.Disposition);
        Assert.Equal(ProviderModelParameterAdjustment.None, resolution.Adjustment);
    }

    [Theory]
    [InlineData(ProviderKind.OpenAi, ProviderTransportKind.Responses, OpenAiModelIds.Gpt56Terra, ProviderInvocationFeatures.FunctionTools)]
    [InlineData(ProviderKind.OpenAi, ProviderTransportKind.ChatCompletions, OpenAiModelIds.Gpt56Terra, ProviderInvocationFeatures.None)]
    [InlineData(ProviderKind.OpenAi, ProviderTransportKind.ChatCompletions, OpenAiModelIds.Gpt56Sol, ProviderInvocationFeatures.FunctionTools)]
    [InlineData(ProviderKind.OpenAi, ProviderTransportKind.ChatCompletions, "gpt-5.6-sol-2026-08-01", ProviderInvocationFeatures.FunctionTools)]
    [InlineData(ProviderKind.OpenAi, ProviderTransportKind.ChatCompletions, OpenAiModelIds.Gpt56, ProviderInvocationFeatures.FunctionTools)]
    [InlineData(ProviderKind.OpenAi, ProviderTransportKind.ChatCompletions, "gpt-5.6-2026-08-01", ProviderInvocationFeatures.FunctionTools)]
    [InlineData(ProviderKind.OpenAi, ProviderTransportKind.ChatCompletions, "gpt-5.4-nano", ProviderInvocationFeatures.FunctionTools)]
    [InlineData(ProviderKind.OpenAi, ProviderTransportKind.ChatCompletions, "gpt-5.6-terra-latest", ProviderInvocationFeatures.FunctionTools)]
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
        Assert.Equal(
            ProviderRequestCompatibilityDisposition.Preserved,
            resolution.Disposition);
        Assert.Equal(ProviderModelParameterAdjustment.None, resolution.Adjustment);
    }

    [Fact]
    public void Evaluate_returns_versioned_effective_evidence_without_switching_transport()
    {
        var providerProfileId = Guid.NewGuid();

        var evidence = OpenAiRequestCompatibilityPolicy.Evaluate(
            ProviderKind.OpenAi,
            providerProfileId,
            ProviderTransportKind.ChatCompletions,
            " GPT-5.6-LUNA-2026-08-01 ",
            ProviderInvocationFeatures.FunctionTools,
            AgentReasoningEffortLevel.Medium);

        Assert.Equal(ProviderRequestCompatibilityEvidence.CurrentSchemaVersion, evidence.SchemaVersion);
        Assert.Equal(ProviderKind.OpenAi, evidence.ProviderKind);
        Assert.Equal(providerProfileId, evidence.ProviderProfileId);
        Assert.Equal(ProviderTransportKind.ChatCompletions, evidence.Transport);
        Assert.Equal("GPT-5.6-LUNA-2026-08-01", evidence.RequestedModel);
        Assert.Equal("GPT-5.6-LUNA-2026-08-01", evidence.EffectiveModel);
        Assert.Equal(ProviderInvocationFeatures.FunctionTools, evidence.InvocationFeatures);
        Assert.Equal(AgentReasoningEffortLevel.Medium, evidence.RequestedEffort);
        Assert.Equal(AgentReasoningEffortLevel.None, evidence.EffectiveEffort);
        Assert.Equal(ProviderRequestCompatibilityDisposition.Adjusted, evidence.Disposition);
        Assert.Equal(
            ProviderModelParameterAdjustment.ReasoningDisabledForFunctionTools,
            evidence.Adjustment);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_missing_model_fails_explicitly(string? model)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            OpenAiRequestCompatibilityPolicy.ResolveReasoningEffort(
                ProviderKind.OpenAi,
                ProviderTransportKind.ChatCompletions,
                model!,
                ProviderInvocationFeatures.FunctionTools,
                AgentReasoningEffortLevel.Medium));
    }
}
