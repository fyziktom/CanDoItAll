using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Tests.Unit;

public sealed class ProviderPricingTests
{
    [Fact]
    public void OpenAi_defaults_include_current_pricing_rows()
    {
        var prices = ProviderPricingDefaults.CreateDefaultPrices(ProviderKind.OpenAi, "gpt-5.4-mini");

        Assert.True(ProviderPricingDefaults.TryFindPrice(prices, "gpt-5.4-mini", out var miniPrice));
        Assert.Equal(0.75m, miniPrice.InputPerMillionTokensUsd);
        Assert.Equal(0.075m, miniPrice.CachedInputPerMillionTokensUsd);
        Assert.Equal(4.50m, miniPrice.OutputPerMillionTokensUsd);

        Assert.True(ProviderPricingDefaults.TryFindPrice(prices, "gpt-5.5", out var flagshipPrice));
        Assert.Equal(5.00m, flagshipPrice.InputPerMillionTokensUsd);
        Assert.Equal(0.50m, flagshipPrice.CachedInputPerMillionTokensUsd);
        Assert.Equal(30.00m, flagshipPrice.OutputPerMillionTokensUsd);
    }

    [Fact]
    public void Ollama_defaults_are_private_and_non_zero()
    {
        var prices = ProviderPricingDefaults.CreateDefaultPrices(ProviderKind.Ollama, "llama3.1");

        Assert.True(ProviderPricingDefaults.IsPrivateProvider(ProviderKind.Ollama));
        Assert.True(ProviderPricingDefaults.TryFindPrice(prices, "llama3.1", out var price));
        Assert.Equal(0.10m, price.InputPerMillionTokensUsd);
        Assert.Equal(0.02m, price.CachedInputPerMillionTokensUsd);
        Assert.Equal(0.20m, price.OutputPerMillionTokensUsd);
    }

    [Fact]
    public void Cost_calculator_prices_uncached_cached_and_output_tokens_separately()
    {
        var prices = new[]
        {
            new ProviderModelTokenPrice("model-a", 1.00m, 0.10m, 4.00m)
        };

        var calculated = ProviderPricingCalculator.TryCalculate(
            "Provider A",
            "model-a",
            inputTokens: 1_000_000,
            cachedInputTokens: 250_000,
            outputTokens: 500_000,
            prices,
            out var cost);

        Assert.True(calculated);
        Assert.Equal(0.75m, cost.InputCostUsd);
        Assert.Equal(0.025m, cost.CachedInputCostUsd);
        Assert.Equal(2.00m, cost.OutputCostUsd);
        Assert.Equal(2.775m, cost.TotalUsd);
    }

    [Fact]
    public void Pricing_metadata_round_trips_without_breaking_flat_configuration_state()
    {
        var json = ProviderPricingMetadata.Write(
            JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["baseUrl"] = "https://api.openai.com/v1/models",
                ["defaultModel"] = "gpt-5.4-mini"
            }),
            isPrivateProvider: true,
            [new ProviderModelTokenPrice("gpt-5.4-mini", 0.75m, 0.075m, 4.50m)]);

        var metadata = ProviderPricingMetadata.Read(json);
        var flatConfiguration = ConfigurationState.FromJson(json);

        Assert.True(metadata.IsPrivateProvider);
        Assert.Single(metadata.ModelPrices);
        Assert.Equal("https://api.openai.com/v1/models", flatConfiguration.GetText("baseUrl"));
        Assert.Equal("gpt-5.4-mini", flatConfiguration.GetText("defaultModel"));
    }
}
