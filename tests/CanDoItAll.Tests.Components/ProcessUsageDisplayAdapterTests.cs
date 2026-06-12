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

        Assert.Equal(ProcessUsageCostDisplayKind.UnknownUsage, display.Kind);
        Assert.Equal("Usage unknown", display.Value);
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

        Assert.Equal(ProcessUsageCostDisplayKind.KnownActual, display.Kind);
        Assert.Equal("$12.34", display.Value);
        Assert.Equal("danger", display.Tone);
        Assert.True(display.ShowsPreciseActualCost);
    }

    [Fact]
    public void BuildCostDisplay_uses_usd_default_instead_of_current_culture_currency()
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

        var display = ProcessUsageDisplayAdapter.BuildCostDisplay(stats, CultureInfo.GetCultureInfo("cs-CZ"));

        Assert.Equal("$12.34", display.Value);
        Assert.DoesNotContain("Kč", display.Value, StringComparison.OrdinalIgnoreCase);
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

        Assert.Equal("Usage unknown", text);
    }

    [Fact]
    public void BuildRunCostText_uses_tree_actual_cost_when_run_has_descendants()
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
        var run = CreateRun(actualCost: 0.95m) with
        {
            TreeActualCost = 2.73m,
            DescendantRunCount = 3
        };

        var text = ProcessUsageDisplayAdapter.BuildRunCostText(stats, run, CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("Total $2.73", text);
    }

    [Fact]
    public void BuildRunCostText_uses_tree_estimated_cost_when_actual_usage_is_not_available()
    {
        var stats = CreateStats(
            actualCost: 0m,
            providerUsage: new ProviderUsageSummary(
                ObservationCount: 1,
                KnownObservationCount: 1,
                UnknownObservationCount: 0,
                InputTokens: 0,
                CachedInputTokens: 0,
                OutputTokens: 0,
                ReasoningTokens: 0,
                TotalTokens: 0,
                KnownCostUsd: 0m));
        var run = CreateRun(actualCost: 0m) with
        {
            TreeEstimatedCost = 5.54m,
            DescendantRunCount = 2
        };

        var text = ProcessUsageDisplayAdapter.BuildRunCostText(stats, run, CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("Est. $5.54", text);
    }

    [Fact]
    public void BuildCostDisplay_distinguishes_missing_usage_from_unknown_usage()
    {
        var stats = CreateStats(
            actualCost: 0m,
            providerUsage: new ProviderUsageSummary(
                ObservationCount: 0,
                KnownObservationCount: 0,
                UnknownObservationCount: 0,
                InputTokens: 100,
                CachedInputTokens: 0,
                OutputTokens: 50,
                ReasoningTokens: 0,
                TotalTokens: 150,
                KnownCostUsd: 0m));

        var display = ProcessUsageDisplayAdapter.BuildCostDisplay(stats, CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal(ProcessUsageCostDisplayKind.MissingUsage, display.Kind);
        Assert.Equal("Est. $20.00", display.Value);
        Assert.False(display.ShowsPreciseActualCost);
        Assert.Contains("missing", display.TooltipText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildCostDisplay_distinguishes_estimate_from_zero_cost()
    {
        var estimated = ProcessUsageDisplayAdapter.BuildCostDisplay(
            CreateStats(
                actualCost: 0m,
                providerUsage: new ProviderUsageSummary(
                    ObservationCount: 1,
                    KnownObservationCount: 1,
                    UnknownObservationCount: 0,
                    InputTokens: 0,
                    CachedInputTokens: 0,
                    OutputTokens: 0,
                    ReasoningTokens: 0,
                    TotalTokens: 0,
                    KnownCostUsd: 0m)),
            CultureInfo.GetCultureInfo("en-US"));
        var zero = ProcessUsageDisplayAdapter.BuildCostDisplay(
            new ProcessLiveStats(
                ObservedRunCount: 1,
                RunningRunCount: 0,
                BlockedRunCount: 0,
                FailedRunCount: 0,
                ActiveAgentCount: 0,
                PendingApprovalCount: 0,
                PendingOutboxCount: 0,
                DeadLetteredOutboxCount: 0,
                DurationMs: 0,
                InputTokens: 0,
                CachedInputTokens: 0,
                OutputTokens: 0,
                ToolCalls: 0,
                EstimatedCost: 0m,
                ActualCost: 0m,
                ProviderUsage: new ProviderUsageSummary(
                    ObservationCount: 0,
                    KnownObservationCount: 0,
                    UnknownObservationCount: 0,
                    InputTokens: 0,
                    CachedInputTokens: 0,
                    OutputTokens: 0,
                    ReasoningTokens: 0,
                    TotalTokens: 0,
                    KnownCostUsd: 0m)),
            CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal(ProcessUsageCostDisplayKind.Estimated, estimated.Kind);
        Assert.Equal("Est. $20.00", estimated.Value);
        Assert.False(estimated.ShowsPreciseActualCost);
        Assert.Equal(ProcessUsageCostDisplayKind.ZeroCost, zero.Kind);
        Assert.Equal("$0", zero.Value);
        Assert.True(zero.ShowsPreciseActualCost);
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
