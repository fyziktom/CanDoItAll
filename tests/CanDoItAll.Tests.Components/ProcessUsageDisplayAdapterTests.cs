using System.Globalization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessUsageDisplayAdapterTests
{
    [Fact]
    public void BuildCostDisplay_hides_precise_actual_cost_when_usage_is_incomplete()
    {
        var stats = CreateStats(
            actualCost: 12.34m,
            providerUsage: new ProviderUsageSummary(
                ObservationCount: 2,
                KnownObservationCount: 1,
                UnknownObservationCount: 1,
                InputTokens: 100,
                CachedInputTokens: 0,
                OutputTokens: 50,
                ReasoningTokens: 0,
                TotalTokens: 150,
                KnownCostUsd: 0.10m));

        var display = ProcessUsageDisplayAdapter.BuildCostDisplay(stats, CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("Incomplete", display.Value);
        Assert.Equal("warning", display.Tone);
        Assert.False(display.ShowsPreciseActualCost);
        Assert.Contains("incomplete provider usage", display.TooltipText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("12.34", display.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCostDisplay_shows_precise_actual_cost_when_usage_is_complete()
    {
        var stats = CreateStats(
            actualCost: 12.34m,
            providerUsage: new ProviderUsageSummary(
                ObservationCount: 1,
                KnownObservationCount: 1,
                UnknownObservationCount: 0,
                InputTokens: 100,
                CachedInputTokens: 0,
                OutputTokens: 50,
                ReasoningTokens: 0,
                TotalTokens: 150,
                KnownCostUsd: 12.34m));

        var display = ProcessUsageDisplayAdapter.BuildCostDisplay(stats, CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("$12.34", display.Value);
        Assert.Equal("danger", display.Tone);
        Assert.True(display.ShowsPreciseActualCost);
    }

    [Fact]
    public void BuildRunCostText_hides_run_actual_cost_when_scope_usage_is_incomplete()
    {
        var stats = CreateStats(
            actualCost: 12.34m,
            providerUsage: new ProviderUsageSummary(
                ObservationCount: 1,
                KnownObservationCount: 0,
                UnknownObservationCount: 1,
                InputTokens: 0,
                CachedInputTokens: 0,
                OutputTokens: 0,
                ReasoningTokens: 0,
                TotalTokens: 0,
                KnownCostUsd: 0m));
        var run = CreateRun(actualCost: 9.99m);

        var text = ProcessUsageDisplayAdapter.BuildRunCostText(stats, run, CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("Usage incomplete", text);
    }

    private static ProcessLiveStats CreateStats(
        decimal actualCost,
        ProviderUsageSummary providerUsage)
    {
        return new ProcessLiveStats(
            ObservedRunCount: 1,
            RunningRunCount: 1,
            BlockedRunCount: 0,
            FailedRunCount: 0,
            ActiveAgentCount: 1,
            PendingApprovalCount: 0,
            PendingOutboxCount: 0,
            DeadLetteredOutboxCount: 0,
            DurationMs: 1000,
            InputTokens: providerUsage.InputTokens,
            CachedInputTokens: providerUsage.CachedInputTokens,
            OutputTokens: providerUsage.OutputTokens,
            ToolCalls: 1,
            EstimatedCost: 20m,
            ActualCost: actualCost,
            ProviderUsage: providerUsage);
    }

    private static ProcessLiveRunCard CreateRun(decimal actualCost)
    {
        return new ProcessLiveRunCard(
            RunId: Guid.NewGuid(),
            DefinitionId: Guid.NewGuid(),
            DefinitionName: "Definition",
            RunName: "Run",
            Status: ProcessRunStatus.Active,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            CompletedStepCount: 1,
            TotalStepCount: 2,
            BlockedStepCount: 0,
            CapabilityGapCount: 0,
            EstimatedCost: 10m,
            ActualCost: actualCost,
            ActiveExecutionCount: 1,
            PendingApprovalCount: 0,
            PendingOutboxCount: 0,
            DeadLetteredOutboxCount: 0,
            BlockedOrFailedStepCount: 0,
            ManagerAgentId: null,
            ManagerAgentName: string.Empty,
            HealthSummary: string.Empty);
    }
}
