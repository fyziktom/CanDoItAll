using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureProcessStartEstimateCalculatorTests
{
    [Fact]
    public void Calculate_uses_provider_model_price_lists_for_preflight_cost()
    {
        var provider = CreateProvider(
            "Provider A",
            "model-a",
            [new ProviderModelTokenPrice("model-a", 1.00m, 0.10m, 4.00m)]);

        var estimate = ProjectStructureProcessStartEstimateCalculator.Calculate(
            "software-delivery",
            assignmentCount: 2,
            [
                new ProjectStructureProcessStartEstimateAssignment(Guid.NewGuid(), "model-a", provider),
                new ProjectStructureProcessStartEstimateAssignment(Guid.NewGuid(), "model-a", provider)
            ]);

        Assert.Equal(0.40m, estimate.EstimatedCostUsd);
        Assert.Equal("Priced", estimate.ConfidenceLabel);
        Assert.Equal("Provider price lists", estimate.SourceLabel);
        Assert.Contains("2 assignment(s) priced from provider model price lists.", estimate.Summary);
    }

    [Fact]
    public void Calculate_reports_partial_coverage_when_provider_model_price_is_missing()
    {
        var provider = CreateProvider(
            "Provider A",
            "model-a",
            [new ProviderModelTokenPrice("model-a", 1.00m, 0.10m, 4.00m)]);

        var estimate = ProjectStructureProcessStartEstimateCalculator.Calculate(
            "software-delivery",
            assignmentCount: 2,
            [
                new ProjectStructureProcessStartEstimateAssignment(Guid.NewGuid(), "model-a", provider),
                new ProjectStructureProcessStartEstimateAssignment(Guid.NewGuid(), "unpriced-model", provider)
            ]);

        Assert.Equal(0.20m, estimate.EstimatedCostUsd);
        Assert.Equal("Partial", estimate.ConfidenceLabel);
        Assert.Equal("Provider price lists", estimate.SourceLabel);
        Assert.Contains("1 assignment(s) priced; 1 assignment(s) missing provider model prices.", estimate.Summary);
    }

    [Fact]
    public void Calculate_prefers_historical_actual_cost_average_when_available()
    {
        var provider = CreateProvider(
            "Provider A",
            "model-a",
            [new ProviderModelTokenPrice("model-a", 1.00m, 0.10m, 4.00m)]);
        var definitionId = ProcessDefinitionId.New();
        var runOne = ProcessRunId.New();
        var runTwo = ProcessRunId.New();
        var historical = new ProcessHistoricalRunCostEstimate(
            definitionId,
            "software-delivery",
            CompletedRunCount: 2,
            PricedRunCount: 2,
            AverageActualCostUsd: 1.234m,
            [
                new ProcessHistoricalRunCostSample(runOne, DateTimeOffset.UtcNow.AddMinutes(-30), 3, 1.111m),
                new ProcessHistoricalRunCostSample(runTwo, DateTimeOffset.UtcNow.AddMinutes(-10), 4, 1.357m)
            ]);

        var estimate = ProjectStructureProcessStartEstimateCalculator.Calculate(
            "software-delivery",
            assignmentCount: 2,
            [
                new ProjectStructureProcessStartEstimateAssignment(Guid.NewGuid(), "model-a", provider),
                new ProjectStructureProcessStartEstimateAssignment(Guid.NewGuid(), "model-a", provider)
            ],
            historical);

        Assert.Equal(1.23m, estimate.EstimatedCostUsd);
        Assert.Equal("Historical", estimate.ConfidenceLabel);
        Assert.Equal("Historical run costs", estimate.SourceLabel);
        Assert.Contains("2 completed historical run(s) priced from actual usage", estimate.Summary);
    }

    [Fact]
    public void Calculate_keeps_provider_pricing_when_historical_runs_have_no_actual_cost()
    {
        var provider = CreateProvider(
            "Provider A",
            "model-a",
            [new ProviderModelTokenPrice("model-a", 1.00m, 0.10m, 4.00m)]);
        var historical = new ProcessHistoricalRunCostEstimate(
            ProcessDefinitionId.New(),
            "software-delivery",
            CompletedRunCount: 1,
            PricedRunCount: 0,
            AverageActualCostUsd: 0m,
            [new ProcessHistoricalRunCostSample(ProcessRunId.New(), DateTimeOffset.UtcNow.AddMinutes(-10), 2, 0m)]);

        var estimate = ProjectStructureProcessStartEstimateCalculator.Calculate(
            "software-delivery",
            assignmentCount: 1,
            [new ProjectStructureProcessStartEstimateAssignment(Guid.NewGuid(), "model-a", provider)],
            historical);

        Assert.Equal(0.20m, estimate.EstimatedCostUsd);
        Assert.Equal("Priced", estimate.ConfidenceLabel);
        Assert.Equal("Provider price lists", estimate.SourceLabel);
        Assert.Contains("historical completed run(s) were found, but none had resolvable actual usage cost", estimate.Summary);
    }

    private static ProviderProfile CreateProvider(
        string name,
        string defaultModel,
        IReadOnlyList<ProviderModelTokenPrice> prices)
    {
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: name,
            Kind: ProviderKind.OpenAi,
            BaseUrl: "https://api.example.test/v1",
            ApiKeyEnvironmentVariable: "TEST_API_KEY",
            DefaultModel: defaultModel,
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
}
