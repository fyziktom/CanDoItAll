using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessRuntimeReadQueryServiceTests
{
    [Fact]
    public void ResolveBestArtifactForExpectation_prefers_concrete_agent_artifact_over_browser_projection()
    {
        var expectationId = Guid.NewGuid();
        var expectation = new ProcessArtifactExpectation
        {
            Id = expectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Implementation summary",
            TrustRequirement = ProcessArtifactTrustRequirement.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal
        };
        var concreteSummary = new ProcessArtifactRecord
        {
            Id = Guid.NewGuid(),
            ArtifactExpectationId = expectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Implementation summary",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ManagedStoragePath = "external-target/C/programovani/demo/implementation-summary.md",
            ExternalReferenceKey = "agentframework-artifact:summary-001",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5)
        };
        var browserSnapshot = new ProcessArtifactRecord
        {
            Id = Guid.NewGuid(),
            ArtifactExpectationId = expectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "browser-snapshot.md",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ManagedStoragePath = "artifacts/scopes/organization/demo/process-runs/run/browser-snapshot.md",
            ExternalReferenceKey = "agentframework-browser-artifact:run:browser-snapshot",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var unrelatedAgentArtifact = new ProcessArtifactRecord
        {
            Id = Guid.NewGuid(),
            ArtifactExpectationId = expectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "favicon.svg",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ManagedStoragePath = "external-target/C/programovani/demo/favicon.svg",
            ExternalReferenceKey = "agentframework-artifact:favicon-001",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddSeconds(5)
        };

        var resolved = ProcessRuntimeReadQueryService.ResolveBestArtifactForExpectation(
            expectation,
            [browserSnapshot, unrelatedAgentArtifact, concreteSummary]);

        Assert.Same(concreteSummary, resolved);
    }

    [Fact]
    public void Process_run_tree_cost_rollups_include_nested_subprocesses()
    {
        var rootRunId = Guid.NewGuid();
        var implementationRunId = Guid.NewGuid();
        var setupRunId = Guid.NewGuid();
        var boundedChangeRunId = Guid.NewGuid();
        var screenshotRunId = Guid.NewGuid();

        var rollups = ProcessRunTreeCostCalculator.BuildRollups(
            [
                new ProcessRunTreeCostInput(rootRunId, ParentRunId: null, EstimatedCost: 2.440m, ActualCost: 0.954375m),
                new ProcessRunTreeCostInput(implementationRunId, rootRunId, EstimatedCost: 0.995m, ActualCost: 0.165037m),
                new ProcessRunTreeCostInput(setupRunId, implementationRunId, EstimatedCost: 0.690m, ActualCost: 0.375906m),
                new ProcessRunTreeCostInput(boundedChangeRunId, implementationRunId, EstimatedCost: 0.940m, ActualCost: 0.809067m),
                new ProcessRunTreeCostInput(screenshotRunId, rootRunId, EstimatedCost: 0.670m, ActualCost: 0.198379m)
            ]);

        Assert.Equal(2.502764m, rollups[rootRunId].ActualCost);
        Assert.Equal(1.350010m, rollups[implementationRunId].ActualCost);
        Assert.Equal(4, rollups[rootRunId].DescendantRunCount);
        Assert.Equal(2, rollups[implementationRunId].DescendantRunCount);
    }

    [Fact]
    public void Covered_process_run_cost_rollup_counts_overlapping_tree_selection_once()
    {
        var rootRunId = Guid.NewGuid();
        var childRunId = Guid.NewGuid();
        var grandchildRunId = Guid.NewGuid();

        var coveredRollup = ProcessRunTreeCostCalculator.BuildCoveredRollup(
            [rootRunId, childRunId],
            [
                new ProcessRunTreeCostInput(rootRunId, ParentRunId: null, EstimatedCost: 10m, ActualCost: 1m),
                new ProcessRunTreeCostInput(childRunId, rootRunId, EstimatedCost: 2m, ActualCost: 0.25m),
                new ProcessRunTreeCostInput(grandchildRunId, childRunId, EstimatedCost: 3m, ActualCost: 0.75m)
            ]);

        Assert.Equal(15m, coveredRollup.EstimatedCost);
        Assert.Equal(2m, coveredRollup.ActualCost);
    }

    [Fact]
    public void Provider_usage_summary_prices_legacy_metrics_when_provider_prices_are_available()
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
            ToolCalls: 2)
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

        var summary = ProcessProviderUsageSummaryBuilder.Build(
            usageObservations: [],
            legacyMetricsWithoutUsageObservations: [metric],
            providers: [provider]);

        Assert.Equal(1, summary.ObservationCount);
        Assert.Equal(1, summary.KnownObservationCount);
        Assert.Equal(0, summary.UnknownObservationCount);
        Assert.Equal(1_000_000, summary.InputTokens);
        Assert.Equal(250_000, summary.CachedInputTokens);
        Assert.Equal(500_000, summary.OutputTokens);
        Assert.Equal(1_500_000, summary.TotalTokens);
        Assert.Equal(2.775m, summary.KnownCostUsd);
    }
}
