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

    [Theory]
    [InlineData(ProviderTransportKind.Responses)]
    [InlineData(ProviderTransportKind.ChatCompletions)]
    public void Openai_supported_transports_apply_reasoning_effort(ProviderTransportKind transport)
    {
        var effort = AgentProviderModelParameterPolicy.ResolveReasoningEffort(
            ProviderKind.OpenAi,
            transport,
            OpenAiModelIds.Gpt56Sol,
            "{\"reasoningEffort\":\"medium\"}",
            string.Empty);

        Assert.True(AgentProviderModelParameterPolicy.CanApplyReasoningEffort(
            ProviderKind.OpenAi,
            transport,
            OpenAiModelIds.Gpt56Sol));
        Assert.Equal(AgentReasoningEffortLevel.Medium, effort);
    }

    [Fact]
    public void Max_reasoning_effort_is_parsed_and_formatted_for_openai_models()
    {
        var effort = AgentProviderModelParameterPolicy.ResolveReasoningEffort(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            OpenAiModelIds.Gpt56Sol,
            "{\"reasoningEffort\":\"max\"}",
            string.Empty);
        var parsedEffort = Assert.IsType<AgentReasoningEffortLevel>(effort);

        Assert.Equal(AgentReasoningEffortLevel.Max, parsedEffort);
        Assert.Equal("max", AgentProviderModelParameterPolicy.FormatReasoningEffort(parsedEffort));
    }

    [Theory]
    [InlineData("gpt-5.4")]
    [InlineData("gpt-5.5")]
    [InlineData("o3")]
    public void Max_reasoning_effort_fails_before_calling_unsupported_openai_models(string model)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentProviderModelParameterPolicy.ResolveReasoningEffort(
                ProviderKind.OpenAi,
                ProviderTransportKind.Responses,
                model,
                "{\"reasoningEffort\":\"max\"}",
                string.Empty));

        Assert.Contains("only supported by GPT-5.6", exception.Message);
        Assert.Contains(model, exception.Message);
    }

    [Fact]
    public void Max_output_tokens_prefers_agent_configuration_over_provider_configuration()
    {
        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
            ProviderKind.OpenAi,
            OpenAiModelIds.Gpt56Luna,
            "{\"modelParameters\":{\"maxOutputTokens\":300}}",
            "{\"maxOutputTokens\":120}");

        Assert.Equal(120, maxOutputTokens);
    }

    [Fact]
    public void Gpt5_max_output_tokens_accepts_128k_and_rejects_larger_values()
    {
        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
            ProviderKind.OpenAi,
            OpenAiModelIds.Gpt56Luna,
            "{\"modelParameters\":{\"maxOutputTokens\":128000}}",
            string.Empty);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
                ProviderKind.OpenAi,
                OpenAiModelIds.Gpt56Luna,
                "{\"modelParameters\":{\"maxOutputTokens\":128001}}",
                string.Empty));

        Assert.Equal(128_000, maxOutputTokens);
        Assert.Contains("between 1 and 128000", exception.Message);
    }

    [Theory]
    [InlineData("gpt-4.1")]
    [InlineData("gpt-4o")]
    public void Older_openai_models_keep_the_conservative_output_token_limit(string model)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
                ProviderKind.OpenAi,
                model,
                "{\"modelParameters\":{\"maxOutputTokens\":8193}}",
                string.Empty));

        Assert.Contains("between 1 and 8192", exception.Message);
    }

    [Fact]
    public void Ollama_num_predict_is_treated_as_max_output_tokens()
    {
        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
            ProviderKind.Ollama,
            "{\"modelParameters\":{\"numPredict\":160}}",
            string.Empty);

        Assert.Equal(160, maxOutputTokens);
    }

    [Fact]
    public void Ollama_think_can_be_disabled_from_model_parameters()
    {
        var think = AgentProviderModelParameterPolicy.ResolveOllamaThink(
            "{\"modelParameters\":{\"think\":false}}",
            string.Empty);

        Assert.False(think);
    }

    [Fact]
    public void Ollama_defaults_bound_generation_and_disable_thinking()
    {
        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveOllamaMaxOutputTokensOrDefault(
            "{}",
            string.Empty);
        var think = AgentProviderModelParameterPolicy.ResolveOllamaThinkOrDefault(
            "{}",
            string.Empty);

        Assert.Equal(AgentProviderModelParameterPolicy.DefaultOllamaMaxOutputTokens, maxOutputTokens);
        Assert.Equal(AgentProviderModelParameterPolicy.DefaultOllamaThinkEnabled, think);
    }

    [Fact]
    public void Invalid_max_output_tokens_fails_explicitly()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
                ProviderKind.Ollama,
                "{\"modelParameters\":{\"num_predict\":0}}",
                string.Empty));

        Assert.Contains("must be between", exception.Message);
    }
}
