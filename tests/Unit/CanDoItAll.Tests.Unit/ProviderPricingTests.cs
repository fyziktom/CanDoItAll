using System.Net;
using System.Net.Http;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.SharedKernel.Configuration;

using OllamaProviderAdministrationConnector = CanDoItAll.Modules.AgentFramework.ProviderManagement.OllamaProviderAdministrationConnector;
using OpenAiProviderAdministrationConnector = CanDoItAll.Modules.AgentFramework.ProviderManagement.OpenAiProviderAdministrationConnector;
using PersistedProviderProfile = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProviderPricingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Editor_private_flag_replaces_stale_configuration_and_survives_reload(bool initialValue) {
        var prices = new[] { new ProviderModelTokenPrice("model-a", 1.23m, 0m, 4.56m) };
        var current = CreateProvider("Source pricing", prices) with {
            IsPrivateProvider = initialValue,
            ConfigurationJson = ProviderPricingMetadata.Write("{}", initialValue, prices)
        };
        var service = new ProviderProfileService();
        var editor = service.CreateEditor(current);
        editor.IsPrivateProvider = !initialValue;

        var saved = service.CreateProfile(editor, current);

        Assert.Equal(!initialValue, saved.IsPrivateProvider);
        Assert.Equal(!initialValue, ProviderPricingMetadata.Read(saved.ConfigurationJson).IsPrivateProvider);
        Assert.Equal(!initialValue, service.CreateEditor(saved).IsPrivateProvider);
        Assert.Equal(prices[0], Assert.Single(saved.ModelPrices, price => price.Model == "model-a"));
    }

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
    public void OpenAi_defaults_include_exact_gpt_5_6_standard_and_long_context_prices()
    {
        var prices = ProviderPricingDefaults.CreateDefaultPrices(ProviderKind.OpenAi, OpenAiModelIds.Gpt56Terra);
        ProviderModelTokenPrice[] expectedPrices =
        [
            new(OpenAiModelIds.Gpt56Luna, 0.20m, 0.02m, 1.20m)
            {
                CacheWritePerMillionTokensUsd = 0.25m,
                LongContextThresholdTokens = OpenAiModelPricingPolicy.Gpt56LongContextThresholdTokens,
                LongContextInputPerMillionTokensUsd = 0.40m,
                LongContextCachedInputPerMillionTokensUsd = 0.04m,
                LongContextCacheWritePerMillionTokensUsd = 0.50m,
                LongContextOutputPerMillionTokensUsd = 1.80m
            },
            new(OpenAiModelIds.Gpt56Terra, 2.00m, 0.20m, 12.00m)
            {
                CacheWritePerMillionTokensUsd = 2.50m,
                LongContextThresholdTokens = OpenAiModelPricingPolicy.Gpt56LongContextThresholdTokens,
                LongContextInputPerMillionTokensUsd = 4.00m,
                LongContextCachedInputPerMillionTokensUsd = 0.40m,
                LongContextCacheWritePerMillionTokensUsd = 5.00m,
                LongContextOutputPerMillionTokensUsd = 18.00m
            },
            new(OpenAiModelIds.Gpt56Sol, 4.00m, 0.40m, 20.00m)
            {
                CacheWritePerMillionTokensUsd = 5.00m,
                LongContextThresholdTokens = OpenAiModelPricingPolicy.Gpt56LongContextThresholdTokens,
                LongContextInputPerMillionTokensUsd = 8.00m,
                LongContextCachedInputPerMillionTokensUsd = 0.80m,
                LongContextCacheWritePerMillionTokensUsd = 10.00m,
                LongContextOutputPerMillionTokensUsd = 30.00m
            }
        ];

        foreach (var expectedPrice in expectedPrices)
        {
            Assert.True(ProviderPricingDefaults.TryFindPrice(prices, expectedPrice.Model, out var actualPrice));
            Assert.Equal(expectedPrice, actualPrice);
        }
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
    public void Cost_calculator_uses_long_context_rates_above_the_gpt_5_6_threshold()
    {
        var prices = ProviderPricingDefaults.CreateDefaultPrices(ProviderKind.OpenAi, OpenAiModelIds.Gpt56Terra);

        var calculated = ProviderPricingCalculator.TryCalculate(
            "OpenAI",
            OpenAiModelIds.Gpt56Terra,
            inputTokens: 300_000,
            cachedInputTokens: 100_000,
            outputTokens: 20_000,
            prices,
            out var cost);

        Assert.True(calculated);
        Assert.Equal(0.80m, cost.InputCostUsd);
        Assert.Equal(0.04m, cost.CachedInputCostUsd);
        Assert.Equal(0.36m, cost.OutputCostUsd);
        Assert.Equal(1.20m, cost.TotalUsd);
    }

    [Fact]
    public void Cost_calculator_prices_standard_context_cache_writes_separately()
    {
        var prices = ProviderPricingDefaults.CreateDefaultPrices(ProviderKind.OpenAi, OpenAiModelIds.Gpt56Terra);

        var calculated = ProviderPricingCalculator.TryCalculate(
            "OpenAI",
            OpenAiModelIds.Gpt56Terra,
            inputTokens: 200_000,
            cachedInputTokens: 50_000,
            cacheWriteTokens: 20_000,
            outputTokens: 0,
            prices,
            out var cost);

        Assert.True(calculated);
        Assert.Equal(130_000, cost.InputTokens - cost.CachedInputTokens - cost.CacheWriteTokens);
        Assert.Equal(20_000, cost.CacheWriteTokens);
        Assert.Equal(0.26m, cost.InputCostUsd);
        Assert.Equal(0.01m, cost.CachedInputCostUsd);
        Assert.Equal(0.05m, cost.CacheWriteCostUsd);
        Assert.Equal(0.32m, cost.TotalUsd);
    }

    [Fact]
    public void Cost_calculator_prices_long_context_cache_writes_at_the_long_rate()
    {
        var prices = ProviderPricingDefaults.CreateDefaultPrices(ProviderKind.OpenAi, OpenAiModelIds.Gpt56Terra);

        var calculated = ProviderPricingCalculator.TryCalculate(
            "OpenAI",
            OpenAiModelIds.Gpt56Terra,
            inputTokens: 400_000,
            cachedInputTokens: 100_000,
            cacheWriteTokens: 50_000,
            outputTokens: 20_000,
            prices,
            out var cost);

        Assert.True(calculated);
        Assert.Equal(1.00m, cost.InputCostUsd);
        Assert.Equal(0.04m, cost.CachedInputCostUsd);
        Assert.Equal(0.25m, cost.CacheWriteCostUsd);
        Assert.Equal(0.36m, cost.OutputCostUsd);
        Assert.Equal(1.65m, cost.TotalUsd);
    }

    [Fact]
    public void Usage_summary_applies_long_context_pricing_per_observation_not_to_aggregate_tokens()
    {
        var provider = CreateProvider(
            "Provider A",
            ProviderPricingDefaults.CreateDefaultPrices(ProviderKind.OpenAi, OpenAiModelIds.Gpt56Terra));
        var firstRequest = CreateUsageObservation(
            ProviderUsageObservationStatus.Observed,
            inputTokens: 200_000,
            cachedInputTokens: 0,
            outputTokens: 0,
            model: OpenAiModelIds.Gpt56Terra);
        var secondRequest = CreateUsageObservation(
            ProviderUsageObservationStatus.Observed,
            inputTokens: 200_000,
            cachedInputTokens: 0,
            outputTokens: 0,
            model: OpenAiModelIds.Gpt56Terra);

        var summary = ProviderPricingCalculator.SummarizeUsage([firstRequest, secondRequest], [provider]);

        Assert.Equal(400_000, summary.InputTokens);
        Assert.Equal(0.80m, summary.KnownCostUsd);
    }

    [Fact]
    public void Observation_cost_uses_total_tokens_when_reasoning_is_not_in_output_tokens()
    {
        var provider = CreateProvider(
            "Provider A",
            [new ProviderModelTokenPrice("model-a", 1.00m, 0.10m, 4.00m)]);
        var observation = new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: "Provider A",
            ProviderKind: ProviderKind.OpenAi,
            Model: "model-a",
            TransportKind: ProviderTransportKind.Responses,
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: ProviderUsageObservationStatus.Observed,
            InputTokens: 1_000_000,
            CachedInputTokens: 250_000,
            OutputTokens: 500_000,
            ReasoningTokens: 250_000,
            TotalTokens: 1_750_000,
            ToolCallCount: 0);

        var resolved = ProviderPricingCalculator.TryResolveObservationCost(observation, [provider], out var costUsd);
        var summary = ProviderPricingCalculator.SummarizeUsage([observation], [provider]);

        Assert.True(resolved);
        Assert.Equal(3.775m, costUsd);
        Assert.Equal(1_750_000, summary.TotalTokens);
        Assert.Equal(3.775m, summary.KnownCostUsd);
    }

    [Fact]
    public void Metric_cost_resolution_prices_cached_input_tokens_when_cost_is_not_persisted()
    {
        var metric = new AgentRunMetric(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Outcome: RunOutcome.Succeeded,
            ProviderName: "Provider A",
            Model: "model-a",
            DurationMs: 100,
            InputTokens: 1_000_000,
            OutputTokens: 500_000,
            ToolCalls: 0)
        {
            CachedInputTokens = 250_000
        };
        var provider = new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: "Provider A",
            Kind: ProviderKind.OpenAi,
            BaseUrl: "https://api.example.test/v1",
            ApiKeyEnvironmentVariable: "TEST_API_KEY",
            DefaultModel: "model-a",
            Transport: ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "ok",
            LastCheckedAtUtc: null,
            SuggestedModels: [])
        {
            ModelPrices = [new ProviderModelTokenPrice("model-a", 1.00m, 0.10m, 4.00m)]
        };

        var resolved = ProviderPricingCalculator.TryResolveMetricCost(metric, [provider], out var costUsd);

        Assert.True(resolved);
        Assert.Equal(2.775m, costUsd);
    }

    [Fact]
    public void Usage_summary_counts_only_observed_usage_as_known_actual_cost()
    {
        var provider = CreateProvider(
            "Provider A",
            [new ProviderModelTokenPrice("model-a", 1.00m, 0.10m, 4.00m)]);
        var observed = CreateUsageObservation(
            ProviderUsageObservationStatus.Observed,
            inputTokens: 1_000_000,
            cachedInputTokens: 250_000,
            outputTokens: 500_000);
        var estimated = CreateUsageObservation(
            ProviderUsageObservationStatus.EstimatedFromMetric,
            inputTokens: 1_000_000,
            cachedInputTokens: 0,
            outputTokens: 500_000);
        var unknown = CreateUsageObservation(
            ProviderUsageObservationStatus.MissingAfterProviderActivity,
            inputTokens: 0,
            cachedInputTokens: 0,
            outputTokens: 0);

        var summary = ProviderPricingCalculator.SummarizeUsage([observed, estimated, unknown], [provider]);

        Assert.Equal(3, summary.ObservationCount);
        Assert.Equal(1, summary.KnownObservationCount);
        Assert.Equal(2, summary.UnknownObservationCount);
        Assert.Equal(1_000_000, summary.InputTokens);
        Assert.Equal(250_000, summary.CachedInputTokens);
        Assert.Equal(500_000, summary.OutputTokens);
        Assert.Equal(1_500_000, summary.TotalTokens);
        Assert.Equal(2.775m, summary.KnownCostUsd);
        Assert.False(ProviderPricingCalculator.TryResolveObservationCost(estimated, [provider], out _));
        Assert.False(ProviderPricingCalculator.TryResolveObservationCost(unknown, [provider], out _));
    }

    [Fact]
    public void Usage_summary_counts_observed_usage_across_runtime_repair_and_finalizer_phases()
    {
        var provider = CreateProvider(
            "Provider A",
            [new ProviderModelTokenPrice("model-a", 1.00m, 0.10m, 4.00m)]);
        var runtimeUsage = CreateUsageObservation(
            ProviderUsageObservationStatus.Observed,
            inputTokens: 1_000_000,
            cachedInputTokens: 250_000,
            outputTokens: 500_000,
            sourcePhase: ProviderUsageSourcePhases.AgentRuntime);
        var repairUsage = CreateUsageObservation(
            ProviderUsageObservationStatus.Observed,
            inputTokens: 10_000,
            cachedInputTokens: 1_000,
            outputTokens: 2_000,
            sourcePhase: ProviderUsageSourcePhases.StructuredOutputRepair);
        var finalizerUsage = CreateUsageObservation(
            ProviderUsageObservationStatus.ObservedFromMetric,
            inputTokens: 5_000,
            cachedInputTokens: 0,
            outputTokens: 1_000,
            sourcePhase: ProviderUsageSourcePhases.FinalizerRecovery);

        var summary = ProviderPricingCalculator.SummarizeUsage([runtimeUsage, repairUsage, finalizerUsage], [provider]);

        Assert.Equal(3, summary.ObservationCount);
        Assert.Equal(3, summary.KnownObservationCount);
        Assert.Equal(0, summary.UnknownObservationCount);
        Assert.Equal(1_015_000, summary.InputTokens);
        Assert.Equal(251_000, summary.CachedInputTokens);
        Assert.Equal(503_000, summary.OutputTokens);
        Assert.Equal(1_518_000, summary.TotalTokens);
        Assert.Equal(2.8011m, summary.KnownCostUsd);
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

    [Fact]
    public void Discovered_prices_override_same_model_and_remove_nonmembers_without_fabricating_unknown_prices()
    {
        var currentPrices = new[]
        {
            new ProviderModelTokenPrice("priced-model", 9.00m, 0.90m, 18.00m),
            new ProviderModelTokenPrice("manual-only", 2.00m, 0.20m, 6.00m)
        };
        var discoveredPrices = new[]
        {
            new ProviderDiscoveredModelPrice("priced-model", 1.25m, 0.125m, 5.00m),
            new ProviderDiscoveredModelPrice("name-only-model", null, null, null)
        };

        var merged = ProviderPricingDefaults.MergeDiscoveredModelPrices(
            ProviderKind.OpenAi,
            "priced-model",
            currentPrices,
            discoveredPrices);

        Assert.Equal(2, merged.DiscoveredModelCount);
        Assert.Equal(1, merged.ExplicitPriceCount);
        Assert.Equal(1, merged.ModelNameOnlyCount);
        Assert.True(ProviderPricingDefaults.TryFindPrice(merged.ModelPrices, "priced-model", out var pricedModel));
        Assert.Equal(1.25m, pricedModel.InputPerMillionTokensUsd);
        Assert.False(ProviderPricingDefaults.TryFindPrice(merged.ModelPrices, "manual-only", out _));
        Assert.DoesNotContain(
            merged.ModelPrices,
            price => string.Equals(price.Model, "name-only-model", StringComparison.OrdinalIgnoreCase));
        Assert.False(ProviderPricingDefaults.TryFindPrice(merged.ModelPrices, "name-only-model", out _));
    }

    [Fact]
    public void Known_defaults_enrich_existing_gpt_5_6_rows_without_overwriting_standard_prices()
    {
        var configuredPrices = new[]
        {
            new ProviderModelTokenPrice(OpenAiModelIds.Gpt56Terra, 9.00m, 0.90m, 18.00m)
        };

        var merged = ProviderPricingDefaults.MergeKnownDefaultPrices(
            ProviderKind.OpenAi,
            OpenAiModelIds.Gpt56Terra,
            configuredPrices);

        Assert.True(ProviderPricingDefaults.TryFindPrice(merged, OpenAiModelIds.Gpt56Terra, out var terra));
        Assert.Equal(9.00m, terra.InputPerMillionTokensUsd);
        Assert.Equal(0.90m, terra.CachedInputPerMillionTokensUsd);
        Assert.Equal(18.00m, terra.OutputPerMillionTokensUsd);
        Assert.Equal(2.50m, terra.CacheWritePerMillionTokensUsd);
        Assert.Equal(OpenAiModelPricingPolicy.Gpt56LongContextThresholdTokens, terra.LongContextThresholdTokens);
        Assert.Equal(4.00m, terra.LongContextInputPerMillionTokensUsd);
        Assert.Equal(0.40m, terra.LongContextCachedInputPerMillionTokensUsd);
        Assert.Equal(5.00m, terra.LongContextCacheWritePerMillionTokensUsd);
        Assert.Equal(18.00m, terra.LongContextOutputPerMillionTokensUsd);
    }

    [Fact]
    public void Authoritative_known_defaults_replace_stale_known_rows_and_preserve_custom_models()
    {
        var configuredPrices = new[]
        {
            new ProviderModelTokenPrice(OpenAiModelIds.Gpt56Luna, 9.00m, 0.90m, 18.00m)
            {
                CacheWritePerMillionTokensUsd = 4.00m,
                LongContextThresholdTokens = 300_000,
                LongContextInputPerMillionTokensUsd = 8.00m,
                LongContextCachedInputPerMillionTokensUsd = 0.80m,
                LongContextCacheWritePerMillionTokensUsd = 10.00m,
                LongContextOutputPerMillionTokensUsd = 24.00m
            },
            new ProviderModelTokenPrice("custom-model", 7.00m, 0.70m, 14.00m)
        };

        var merged = ProviderPricingDefaults.MergeAuthoritativeKnownDefaultPrices(
            ProviderKind.OpenAi,
            OpenAiModelIds.Gpt56Luna,
            configuredPrices);

        Assert.True(ProviderPricingDefaults.TryFindPrice(merged, OpenAiModelIds.Gpt56Luna, out var luna));
        Assert.Equal(0.20m, luna.InputPerMillionTokensUsd);
        Assert.Equal(0.02m, luna.CachedInputPerMillionTokensUsd);
        Assert.Equal(0.25m, luna.CacheWritePerMillionTokensUsd);
        Assert.Equal(1.20m, luna.OutputPerMillionTokensUsd);
        Assert.Equal(OpenAiModelPricingPolicy.Gpt56LongContextThresholdTokens, luna.LongContextThresholdTokens);
        Assert.Equal(0.40m, luna.LongContextInputPerMillionTokensUsd);
        Assert.Equal(0.04m, luna.LongContextCachedInputPerMillionTokensUsd);
        Assert.Equal(0.50m, luna.LongContextCacheWritePerMillionTokensUsd);
        Assert.Equal(1.80m, luna.LongContextOutputPerMillionTokensUsd);
        Assert.True(ProviderPricingDefaults.TryFindPrice(merged, "custom-model", out var custom));
        Assert.Equal(7.00m, custom.InputPerMillionTokensUsd);
        Assert.Equal(0.70m, custom.CachedInputPerMillionTokensUsd);
        Assert.Equal(14.00m, custom.OutputPerMillionTokensUsd);
    }

    [Fact]
    public void Explicit_discovery_preserves_existing_optional_gpt_5_6_pricing_metadata()
    {
        var configuredPrices = new[]
        {
            new ProviderModelTokenPrice(OpenAiModelIds.Gpt56Terra, 9.00m, 0.90m, 18.00m)
            {
                CacheWritePerMillionTokensUsd = 4.00m,
                LongContextThresholdTokens = 300_000,
                LongContextInputPerMillionTokensUsd = 8.00m,
                LongContextCachedInputPerMillionTokensUsd = 0.80m,
                LongContextCacheWritePerMillionTokensUsd = 10.00m,
                LongContextOutputPerMillionTokensUsd = 24.00m
            }
        };
        var discoveredPrices = new[]
        {
            new ProviderDiscoveredModelPrice(OpenAiModelIds.Gpt56Terra, 2.75m, 0.275m, 16.00m)
        };

        var merged = ProviderPricingDefaults.MergeDiscoveredModelPrices(
            ProviderKind.OpenAi,
            OpenAiModelIds.Gpt56Terra,
            configuredPrices,
            discoveredPrices);

        Assert.True(ProviderPricingDefaults.TryFindPrice(merged.ModelPrices, OpenAiModelIds.Gpt56Terra, out var terra));
        Assert.Equal(2.75m, terra.InputPerMillionTokensUsd);
        Assert.Equal(0.275m, terra.CachedInputPerMillionTokensUsd);
        Assert.Equal(16.00m, terra.OutputPerMillionTokensUsd);
        Assert.Equal(4.00m, terra.CacheWritePerMillionTokensUsd);
        Assert.Equal(300_000, terra.LongContextThresholdTokens);
        Assert.Equal(8.00m, terra.LongContextInputPerMillionTokensUsd);
        Assert.Equal(0.80m, terra.LongContextCachedInputPerMillionTokensUsd);
        Assert.Equal(10.00m, terra.LongContextCacheWritePerMillionTokensUsd);
        Assert.Equal(24.00m, terra.LongContextOutputPerMillionTokensUsd);
    }

    [Fact]
    public void Name_only_discovery_adds_known_gpt_5_6_default_but_does_not_price_unknown_models()
    {
        var configuredPrices = new[]
        {
            new ProviderModelTokenPrice("legacy-model", 1.00m, 0.10m, 4.00m)
        };
        var discoveredPrices = new[]
        {
            new ProviderDiscoveredModelPrice(OpenAiModelIds.Gpt56Terra, null, null, null),
            new ProviderDiscoveredModelPrice("unknown-model", null, null, null)
        };

        var merged = ProviderPricingDefaults.MergeDiscoveredModelPrices(
            ProviderKind.OpenAi,
            "legacy-model",
            configuredPrices,
            discoveredPrices);

        Assert.True(ProviderPricingDefaults.TryFindPrice(merged.ModelPrices, OpenAiModelIds.Gpt56Terra, out var terra));
        Assert.Equal(2.00m, terra.InputPerMillionTokensUsd);
        Assert.False(ProviderPricingDefaults.TryFindPrice(merged.ModelPrices, "legacy-model", out _));
        Assert.False(ProviderPricingDefaults.TryFindPrice(merged.ModelPrices, "unknown-model", out _));
        Assert.DoesNotContain(
            merged.ModelPrices,
            price => string.Equals(price.Model, "unknown-model", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OpenAi_pricing_discovery_reads_explicit_price_metadata()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "data": [
                    {
                      "id": "priced-model",
                      "pricing": {
                        "input_per_million_tokens_usd": 1.25,
                        "cached_input_per_million_tokens_usd": "0.125",
                        "output_per_million_tokens_usd": 5.00
                      }
                    }
                  ]
                }
                """)
        };
        var connector = new OpenAiProviderAdministrationConnector(new FakeHttpClientFactory(_ => response));
        var result = await connector.DiscoverModelPricingAsync(
            new PersistedProviderProfile
            {
                Name = "OpenAI test",
                BaseUrl = "https://api.example.test/v1/models",
                DefaultModel = "priced-model",
                TimeoutSeconds = 45
            },
            "sk-test");

        Assert.True(result.IsSuccess);
        var price = Assert.Single(result.Value!.Models);
        Assert.True(price.HasExplicitPrices);
        Assert.Equal("priced-model", price.Model);
        Assert.Equal(1.25m, price.InputPerMillionTokensUsd);
        Assert.Equal(0.125m, price.CachedInputPerMillionTokensUsd);
        Assert.Equal(5.00m, price.OutputPerMillionTokensUsd);
    }

    [Fact]
    public async Task OpenAi_pricing_discovery_requires_secret()
    {
        var connector = new OpenAiProviderAdministrationConnector(new FakeHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var result = await connector.DiscoverModelPricingAsync(
            new PersistedProviderProfile
            {
                Name = "OpenAI test",
                BaseUrl = "https://api.example.test/v1/models",
                DefaultModel = "priced-model",
                TimeoutSeconds = 45
            },
            null);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Message.Contains("API key secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ollama_pricing_discovery_returns_model_names_without_prices()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "models": [
                    { "name": "llama3.1" },
                    { "model": "mistral" }
                  ]
                }
                """)
        };
        var connector = new OllamaProviderAdministrationConnector(new FakeHttpClientFactory(_ => response));

        var result = await connector.DiscoverModelPricingAsync(
            new PersistedProviderProfile
            {
                Name = "Ollama test",
                BaseUrl = "http://127.0.0.1:11434",
                DefaultModel = "llama3.1",
                TimeoutSeconds = 45
            },
            null);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Models.Count);
        Assert.All(result.Value.Models, model => Assert.False(model.HasExplicitPrices));
        Assert.Contains(result.Value.Models, model => model.Model == "llama3.1");
        Assert.Contains(result.Value.Models, model => model.Model == "mistral");
    }

    private static ProviderProfile CreateProvider(
        string name,
        IReadOnlyList<ProviderModelTokenPrice> prices)
    {
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: name,
            Kind: ProviderKind.OpenAi,
            BaseUrl: "https://api.example.test/v1",
            ApiKeyEnvironmentVariable: "TEST_API_KEY",
            DefaultModel: "model-a",
            Transport: ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "ok",
            LastCheckedAtUtc: null,
            SuggestedModels: [])
        {
            ModelPrices = prices
        };
    }

    private static ProviderUsageObservation CreateUsageObservation(
        ProviderUsageObservationStatus status,
        int inputTokens,
        int cachedInputTokens,
        int outputTokens,
        string sourcePhase = ProviderUsageSourcePhases.AgentRuntime,
        string model = "model-a",
        int cacheWriteTokens = 0)
    {
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: "Provider A",
            ProviderKind: ProviderKind.OpenAi,
            Model: model,
            TransportKind: ProviderTransportKind.Responses,
            SourcePhase: sourcePhase,
            UsageStatus: status,
            InputTokens: inputTokens,
            CachedInputTokens: cachedInputTokens,
            OutputTokens: outputTokens,
            ReasoningTokens: 0,
            TotalTokens: inputTokens + outputTokens,
            ToolCallCount: 0)
        {
            CacheWriteTokens = cacheWriteTokens
        };
    }

    private sealed class FakeHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new FakeHttpMessageHandler(handler));
        }
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
