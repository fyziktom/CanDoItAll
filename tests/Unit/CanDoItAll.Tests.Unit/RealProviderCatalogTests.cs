using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class RealProviderCatalogTests {
    [Theory]
    [InlineData(ProviderKind.OpenAi)]
    [InlineData(ProviderKind.Ollama)]
    public void Empty_prices_remain_unknown_after_normalization_and_editor_save(ProviderKind kind) {
        Assert.Empty(ProviderPricingDefaults.NormalizeModelPrices(kind, "unpriced-model", []));

        var service = new ProviderProfileService();
        var saved = service.CreateProfile(new ProviderProfileEditorModel {
            Name = "Unpriced provider",
            Kind = kind,
            Transport = ProviderTransportKind.ChatCompletions,
            BaseUrl = "http://localhost:11434",
            DefaultModel = "unpriced-model",
            SuggestedModels = ["unpriced-model"],
            ModelPrices = []
        });

        Assert.Empty(saved.ModelPrices);
        Assert.Empty(ProviderPricingMetadata.Read(saved.ConfigurationJson).ModelPrices);
        Assert.Empty(service.CreateEditor(saved).ModelPrices);
    }

    [Fact]
    public void Ollama_discovery_removes_foreign_prices_without_inventing_rates() {
        var result = ProviderPricingDefaults.MergeDiscoveredModelPrices(
            ProviderKind.Ollama, "gpt-5.4-mini",
            [new("gpt-5.4-mini", 0.75m, 0.075m, 4.5m)],
            [new("gpt-oss:20b", null, null, null), new("gemma3:4b", null, null, null)]);

        Assert.Equal(2, result.DiscoveredModelCount);
        Assert.Empty(result.ModelPrices);
    }

    [Fact]
    public void OpenAi_discovery_removes_nonmember_prices_and_keeps_unknown_rates_absent() {
        var result = ProviderPricingDefaults.MergeDiscoveredModelPrices(
            ProviderKind.OpenAi, "stale-default",
            [new("e2e-secondary-model", 1m, 0m, 2m)],
            [new("gpt-5.4-mini", null, null, null), new("new-upstream-model", null, null, null)]);

        var price = Assert.Single(result.ModelPrices);
        Assert.Equal("gpt-5.4-mini", price.Model);
        Assert.Equal(0.75m, price.InputPerMillionTokensUsd);
        Assert.Equal(2, result.DiscoveredModelCount);
    }
}
