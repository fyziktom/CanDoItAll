using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentProviderModelParameterPolicyTests
{
    [Theory]
    [InlineData("gpt-5")]
    [InlineData("gpt-5-mini")]
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
}
