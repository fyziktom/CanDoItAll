using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class ProviderUsageNormalizationTests
{
    [Fact]
    public void Normalize_reads_openai_responses_usage_details()
    {
        const string rawResponseJson = """
                                       {
                                         "id": "resp_123",
                                         "status": "completed",
                                         "usage": {
                                           "input_tokens": 75,
                                           "input_tokens_details": {
                                             "cached_tokens": 25,
                                             "cache_write_tokens": 10
                                           },
                                           "output_tokens": 1186,
                                           "output_tokens_details": {
                                             "reasoning_tokens": 1024
                                           },
                                           "total_tokens": 1261
                                         }
                                       }
                                       """;

        var observation = DefaultProviderUsageNormalizer.Instance.Normalize(new ProviderUsageNormalizationRequest(
            Provider: CreateOpenAiProvider(),
            Model: "gpt-5.4-mini",
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: ProviderUsageObservationStatus.Observed,
            InputTokens: 0,
            CachedInputTokens: 0,
            OutputTokens: 0,
            ReasoningTokens: 0,
            TotalTokens: 0,
            ToolCallCount: 1,
            ProviderResponseId: string.Empty,
            ProviderRequestId: "req_123",
            RuntimeSessionKey: "session-123",
            RawUsageJson: rawResponseJson,
            DiagnosticsJson: "{}"));

        Assert.Equal(ProviderUsageObservationStatus.Observed, observation.UsageStatus);
        Assert.Equal(75, observation.InputTokens);
        Assert.Equal(25, observation.CachedInputTokens);
        Assert.Equal(10, observation.CacheWriteTokens);
        Assert.Equal(1186, observation.OutputTokens);
        Assert.Equal(1024, observation.ReasoningTokens);
        Assert.Equal(1261, observation.TotalTokens);
        Assert.Equal("resp_123", observation.ProviderResponseId);
        Assert.Equal("req_123", observation.ProviderRequestId);
        Assert.Equal(rawResponseJson, observation.RawUsageJson);
    }

    [Fact]
    public void Normalize_marks_openai_usage_null_as_unavailable_without_cost_tokens()
    {
        const string rawResponseJson = """
                                       {
                                         "id": "resp_null",
                                         "status": "incomplete",
                                         "usage": null
                                       }
                                       """;

        var observation = DefaultProviderUsageNormalizer.Instance.Normalize(new ProviderUsageNormalizationRequest(
            Provider: CreateOpenAiProvider(),
            Model: "gpt-5.4-mini",
            SourcePhase: ProviderUsageSourcePhases.FinalizerShortCircuit,
            UsageStatus: ProviderUsageObservationStatus.Observed,
            InputTokens: 20,
            CachedInputTokens: 5,
            OutputTokens: 10,
            ReasoningTokens: 3,
            TotalTokens: 30,
            ToolCallCount: 0,
            ProviderResponseId: string.Empty,
            ProviderRequestId: string.Empty,
            RuntimeSessionKey: "session-null",
            RawUsageJson: rawResponseJson,
            DiagnosticsJson: "{}"));

        Assert.Equal(ProviderUsageObservationStatus.UsageUnavailable, observation.UsageStatus);
        Assert.Equal(0, observation.InputTokens);
        Assert.Equal(0, observation.CachedInputTokens);
        Assert.Equal(0, observation.CacheWriteTokens);
        Assert.Equal(0, observation.OutputTokens);
        Assert.Equal(0, observation.ReasoningTokens);
        Assert.Equal(0, observation.TotalTokens);
        Assert.Equal("resp_null", observation.ProviderResponseId);
    }

    [Fact]
    public void Normalize_reads_openai_cache_write_tokens_from_additional_counts()
    {
        const string rawUsageJson = """
                                    {
                                      "inputTokenCount": 100,
                                      "cachedInputTokenCount": 20,
                                      "outputTokenCount": 5,
                                      "additionalCounts": {
                                        "input_tokens_details.cache_write_tokens": 30
                                      }
                                    }
                                    """;

        var observation = DefaultProviderUsageNormalizer.Instance.Normalize(new ProviderUsageNormalizationRequest(
            Provider: CreateOpenAiProvider(),
            Model: OpenAiModelIds.Gpt56Terra,
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: ProviderUsageObservationStatus.Observed,
            InputTokens: 0,
            CachedInputTokens: 0,
            OutputTokens: 0,
            ReasoningTokens: 0,
            TotalTokens: 0,
            ToolCallCount: 0,
            ProviderResponseId: string.Empty,
            ProviderRequestId: string.Empty,
            RuntimeSessionKey: string.Empty,
            RawUsageJson: rawUsageJson,
            DiagnosticsJson: "{}"));

        Assert.Equal(100, observation.InputTokens);
        Assert.Equal(20, observation.CachedInputTokens);
        Assert.Equal(30, observation.CacheWriteTokens);
        Assert.Equal(5, observation.OutputTokens);
    }

    [Fact]
    public void Reconcile_reports_matched_mismatched_internal_only_and_external_only_rows()
    {
        var internalMatched = CreateObservation("resp_matched", totalTokens: 1261, costUsd: 0.006m);
        var internalMismatched = CreateObservation("resp_mismatch", totalTokens: 200, costUsd: 0.002m);
        var internalUnknown = CreateObservation(
            "resp_unknown",
            totalTokens: 0,
            costUsd: null,
            status: ProviderUsageObservationStatus.UsageUnavailable);

        var report = ProviderUsageReconciliationReporter.Create(
            [internalMatched, internalMismatched, internalUnknown],
            [
                new ProviderUsageExternalRecord("resp_matched", "req_1", 75, 25, 1186, 1024, 1261, 0.006m),
                new ProviderUsageExternalRecord("resp_mismatch", "req_2", 100, 0, 150, 0, 250, 0.003m),
                new ProviderUsageExternalRecord("resp_external_only", "req_3", 10, 0, 5, 0, 15, 0.001m)
            ]);

        Assert.Equal(4, report.Entries.Count);
        Assert.Equal(ProviderUsageReconciliationStatus.Matched, report.Entries.Single(item => item.ProviderResponseId == "resp_matched").Status);
        Assert.Equal(ProviderUsageReconciliationStatus.TokenMismatch, report.Entries.Single(item => item.ProviderResponseId == "resp_mismatch").Status);
        Assert.Equal(ProviderUsageReconciliationStatus.UnknownInternalUsage, report.Entries.Single(item => item.ProviderResponseId == "resp_unknown").Status);
        Assert.Equal(ProviderUsageReconciliationStatus.ExternalOnly, report.Entries.Single(item => item.ProviderResponseId == "resp_external_only").Status);
        Assert.Equal(50, report.Entries.Single(item => item.ProviderResponseId == "resp_mismatch").TokenDelta);
        Assert.Equal(1, report.MatchedCount);
        Assert.Equal(3, report.UnresolvedCount);
    }

    private static ProviderProfile CreateOpenAiProvider()
    {
        return new ProviderProfile(
            Id: Guid.Parse("4E43E8E7-8C38-4D0A-BCBB-8FE3B4CDE673"),
            Name: "OpenAI default",
            Kind: ProviderKind.OpenAi,
            BaseUrl: "https://api.openai.com/v1",
            ApiKeyEnvironmentVariable: "OPENAI_API_KEY",
            DefaultModel: "gpt-5.4-mini",
            Transport: ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "ok",
            LastCheckedAtUtc: null,
            SuggestedModels: [])
        {
            ModelPrices = [new ProviderModelTokenPrice("gpt-5.4-mini", 0.75m, 0.075m, 4.50m)]
        };
    }

    private static ProviderUsageObservation CreateObservation(
        string responseId,
        int totalTokens,
        decimal? costUsd,
        ProviderUsageObservationStatus status = ProviderUsageObservationStatus.Observed)
    {
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: "OpenAI default",
            ProviderKind: ProviderKind.OpenAi,
            Model: "gpt-5.4-mini",
            TransportKind: ProviderTransportKind.Responses,
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: status,
            InputTokens: Math.Max(0, totalTokens / 2),
            CachedInputTokens: 0,
            OutputTokens: Math.Max(0, totalTokens - totalTokens / 2),
            ReasoningTokens: 0,
            TotalTokens: totalTokens,
            ToolCallCount: 0)
        {
            ProviderResponseId = responseId,
            ProviderRequestId = responseId.Replace("resp_", "req_", StringComparison.Ordinal),
            CalculatedCostUsd = costUsd
        };
    }
}
