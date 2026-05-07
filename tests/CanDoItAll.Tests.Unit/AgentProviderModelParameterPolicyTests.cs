using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentProviderModelParameterPolicyTests
{
    [Theory]
    [InlineData("gpt-5")]
    [InlineData("gpt-5.4-mini")]
    [InlineData("gpt-5.2")]
    [InlineData("o1-mini")]
    [InlineData("o3")]
    [InlineData("o4-mini")]
    public void Openai_reasoning_models_omit_temperature(string model)
    {
        var shouldOmit = AgentProviderModelParameterPolicy.ShouldOmitTemperature(ProviderKind.OpenAi, model);

        Assert.True(shouldOmit);
    }

    [Theory]
    [InlineData("gpt-4o-mini")]
    [InlineData("gpt-4.1")]
    [InlineData("qwen3.5:9b")]
    public void Non_reasoning_models_keep_temperature_unless_forced(string model)
    {
        var shouldOmit = AgentProviderModelParameterPolicy.ShouldOmitTemperature(ProviderKind.OpenAi, model);

        Assert.False(shouldOmit);
    }

    [Fact]
    public void Forced_omission_applies_to_non_openai_providers()
    {
        var shouldOmit = AgentProviderModelParameterPolicy.ShouldOmitTemperature(
            ProviderKind.Ollama,
            "qwen3.5:9b",
            forceOmitTemperature: true);

        Assert.True(shouldOmit);
    }

    [Fact]
    public void Openai_reasoning_model_uses_agent_reasoning_effort_over_provider()
    {
        var effort = AgentProviderModelParameterPolicy.ResolveReasoningEffort(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            "gpt-5.4",
            "{\"reasoningEffort\":\"low\"}",
            "{\"reasoningEffort\":\"medium\"}");

        Assert.Equal(AgentReasoningEffortLevel.Medium, effort);
    }

    [Fact]
    public void Openai_reasoning_model_reads_nested_provider_reasoning_effort()
    {
        var effort = AgentProviderModelParameterPolicy.ResolveReasoningEffort(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            "gpt-5.4",
            "{\"modelParameters\":{\"reasoningEffort\":\"xhigh\"}}",
            string.Empty);

        Assert.Equal(AgentReasoningEffortLevel.ExtraHigh, effort);
    }

    [Fact]
    public void Non_reasoning_model_ignores_reasoning_effort()
    {
        var effort = AgentProviderModelParameterPolicy.ResolveReasoningEffort(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            "gpt-4.1",
            "{\"reasoningEffort\":\"medium\"}",
            "{\"reasoningEffort\":\"high\"}");

        Assert.Null(effort);
    }

    [Fact]
    public void Invalid_reasoning_effort_fails_explicitly()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentProviderModelParameterPolicy.ResolveReasoningEffort(
                ProviderKind.OpenAi,
                ProviderTransportKind.Responses,
                "gpt-5.4",
                "{\"reasoningEffort\":\"aggressive\"}",
                string.Empty));

        Assert.Contains("Unsupported reasoning effort", exception.Message);
    }

    [Fact]
    public void Openai_chat_completions_transport_does_not_apply_reasoning_effort()
    {
        var effort = AgentProviderModelParameterPolicy.ResolveReasoningEffort(
            ProviderKind.OpenAi,
            ProviderTransportKind.ChatCompletions,
            "gpt-5.4",
            "{\"reasoningEffort\":\"medium\"}",
            string.Empty);

        Assert.Null(effort);
    }
}
