using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureTaskEstimateRefreshServiceTests
{
    private static readonly Guid ProjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly ProjectStructureTaskResourceSelection Resource = new(
        ProjectStructureTaskResourceKind.Person,
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
    private static readonly ProjectTaskEstimate StaleEstimate = new(
        8m,
        ProjectWorkItemEffortUnit.Hours,
        500m,
        "EUR");

    [Fact]
    public async Task Not_started_task_applies_authoritative_quote()
    {
        var strategy = new RecordingStrategy(ProjectStructureTaskResourceCostQuoteStatus.Available);
        var service = CreateService(strategy);

        var result = await service.RefreshAsync(
            ProjectId,
            ProjectTaskExecutionState.NotStarted,
            Resource,
            StaleEstimate);

        Assert.Equal(ProjectStructureTaskEstimateRefreshStatus.Refreshed, result.Status);
        Assert.Equal(ProjectStructureTaskEstimateRefreshReason.AuthoritativeQuoteApplied, result.Reason);
        Assert.Equal(120m, result.Estimate.ExpectedCostAmount);
        Assert.Equal("USD", result.Estimate.ExpectedCostCurrencyCode);
        Assert.NotNull(result.CalculatedCostBasis);
        Assert.Equal(Resource.ResourceId, result.CalculatedCostBasis!.ResourceId);
        Assert.Equal(
            ProjectStructureTaskResourceCostSource.CrmWorkforceRate,
            result.CalculatedCostBasis.Source);
        Assert.True(result.ReplacesCostBasis);
        Assert.Equal(1, strategy.CallCount);
    }

    [Fact]
    public async Task Not_started_task_clears_stale_cost_when_authoritative_quote_is_unavailable()
    {
        var strategy = new RecordingStrategy(ProjectStructureTaskResourceCostQuoteStatus.Unavailable);
        var service = CreateService(strategy);

        var result = await service.RefreshAsync(
            ProjectId,
            ProjectTaskExecutionState.NotStarted,
            Resource,
            StaleEstimate);

        Assert.Equal(ProjectStructureTaskEstimateRefreshStatus.Cleared, result.Status);
        Assert.Equal(ProjectStructureTaskEstimateRefreshReason.AuthoritativeQuoteUnavailable, result.Reason);
        Assert.Null(result.Estimate.ExpectedCostAmount);
        Assert.Equal(string.Empty, result.Estimate.ExpectedCostCurrencyCode);
        Assert.NotNull(result.CalculatedCostBasis);
        Assert.True(result.ReplacesCostBasis);
        Assert.Equal(1, strategy.CallCount);
    }

    [Theory]
    [InlineData(ProjectTaskExecutionState.Unknown)]
    [InlineData(ProjectTaskExecutionState.Started)]
    [InlineData(ProjectTaskExecutionState.Completed)]
    [InlineData(ProjectTaskExecutionState.Cancelled)]
    public async Task Non_repriceable_state_preserves_snapshot_without_calling_strategy(
        ProjectTaskExecutionState state)
    {
        var strategy = new RecordingStrategy(ProjectStructureTaskResourceCostQuoteStatus.Available);
        var service = CreateService(strategy);

        var result = await service.RefreshAsync(
            ProjectId,
            state,
            Resource,
            StaleEstimate);

        Assert.Equal(ProjectStructureTaskEstimateRefreshStatus.Preserved, result.Status);
        Assert.Equal(
            ProjectStructureTaskEstimateRefreshReason.ExecutionStateDoesNotAllowRefresh,
            result.Reason);
        Assert.Equal(StaleEstimate, result.Estimate);
        Assert.False(result.ReplacesCostBasis);
        Assert.Equal(0, strategy.CallCount);
    }

    [Fact]
    public async Task Not_started_unassigned_task_preserves_manual_estimate_without_calling_strategy()
    {
        var strategy = new RecordingStrategy(ProjectStructureTaskResourceCostQuoteStatus.Available);
        var service = CreateService(strategy);

        var result = await service.RefreshAsync(
            ProjectId,
            ProjectTaskExecutionState.NotStarted,
            null,
            StaleEstimate);

        Assert.Equal(ProjectStructureTaskEstimateRefreshStatus.Preserved, result.Status);
        Assert.Equal(ProjectStructureTaskEstimateRefreshReason.NoResourceSelected, result.Reason);
        Assert.Equal(StaleEstimate, result.Estimate);
        Assert.False(result.ReplacesCostBasis);
        Assert.Equal(0, strategy.CallCount);
    }

    [Fact]
    public async Task Removing_authoritative_resource_clears_stale_cost_and_cost_basis()
    {
        var strategy = new RecordingStrategy(ProjectStructureTaskResourceCostQuoteStatus.Available);
        var service = CreateService(strategy);

        var result = await service.RefreshAsync(
            ProjectId,
            ProjectTaskExecutionState.NotStarted,
            null,
            StaleEstimate,
            ProjectStructureTaskMissingResourcePricingPolicy.ClearAuthoritativeSnapshot);

        Assert.Equal(ProjectStructureTaskEstimateRefreshStatus.Cleared, result.Status);
        Assert.Equal(
            ProjectStructureTaskEstimateRefreshReason.AuthoritativeResourceRemoved,
            result.Reason);
        Assert.Null(result.Estimate.ExpectedCostAmount);
        Assert.Equal(string.Empty, result.Estimate.ExpectedCostCurrencyCode);
        Assert.Null(result.CalculatedCostBasis);
        Assert.True(result.ReplacesCostBasis);
        Assert.Equal(0, strategy.CallCount);
    }

    private static ProjectStructureTaskEstimateRefreshService CreateService(
        IProjectStructureTaskResourceCostStrategy strategy)
    {
        var costService = new ProjectStructureTaskResourceCostService([strategy]);
        return new ProjectStructureTaskEstimateRefreshService(costService);
    }

    private sealed class RecordingStrategy(
        ProjectStructureTaskResourceCostQuoteStatus status) : IProjectStructureTaskResourceCostStrategy
    {
        public ProjectStructureTaskResourceKind Kind => ProjectStructureTaskResourceKind.Person;

        public int CallCount { get; private set; }

        public Task<ProjectStructureTaskResourceCostQuote> GetQuoteAsync(
            ProjectStructureTaskResourceCostRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(status == ProjectStructureTaskResourceCostQuoteStatus.Available
                ? new ProjectStructureTaskResourceCostQuote(
                    status,
                    120m,
                    "USD",
                    "test",
                    "Authoritative test quote.",
                    DateTimeOffset.UnixEpoch,
                    ProjectStructureTaskResourceCostSource.CrmWorkforceRate)
                : ProjectStructureTaskResourceCostQuote.Unavailable(
                    "test",
                    "No authoritative price.",
                    DateTimeOffset.UnixEpoch,
                    ProjectStructureTaskResourceCostSource.CrmWorkforceRate));
        }
    }
}
