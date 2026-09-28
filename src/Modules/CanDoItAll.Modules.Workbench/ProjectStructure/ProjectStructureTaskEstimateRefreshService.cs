namespace CanDoItAll.Modules.Workbench;

/// <summary>
/// What pricing did to a task's estimate, as a JSON integer: 0 Preserved (the estimate was kept), 1 Refreshed (the cost
/// amount and currency were set from the resource's price), 2 Cleared (the cost amount and currency were removed).
/// </summary>
public enum ProjectStructureTaskEstimateRefreshStatus
{
    Preserved,
    Refreshed,
    Cleared
}

/// <summary>
/// Why pricing treated a task's estimate as it did, as a JSON integer: 0 ExecutionStateDoesNotAllowRefresh (the task
/// has started or finished, so its cost is kept), 1 NoResourceSelected, 2 AuthoritativeResourceRemoved (the priced
/// resource was removed, so the cost was cleared), 3 AuthoritativeQuoteApplied, 4 AuthoritativeQuoteUnavailable (no
/// price was available for the resource, so the cost was cleared).
/// </summary>
public enum ProjectStructureTaskEstimateRefreshReason
{
    ExecutionStateDoesNotAllowRefresh,
    NoResourceSelected,
    AuthoritativeResourceRemoved,
    AuthoritativeQuoteApplied,
    AuthoritativeQuoteUnavailable
}

public enum ProjectStructureTaskMissingResourcePricingPolicy
{
    PreserveManualEstimate,
    ClearAuthoritativeSnapshot
}

/// <summary>
/// Outcome of pricing a task's estimate from its resource: the resulting estimate, what happened and why, and the price
/// and cost basis used.
/// </summary>
/// <param name="Estimate">The task's estimate after pricing.</param>
/// <param name="Status">What pricing did, as a JSON integer: 0 Preserved, 1 Refreshed, 2 Cleared.</param>
/// <param name="Reason">
/// Why, as a JSON integer: 0 ExecutionStateDoesNotAllowRefresh, 1 NoResourceSelected, 2 AuthoritativeResourceRemoved,
/// 3 AuthoritativeQuoteApplied, 4 AuthoritativeQuoteUnavailable.
/// </param>
/// <param name="Resource">The resource the task was priced for; null when it has none.</param>
/// <param name="Quote">The resource's price that was requested; null when no price was requested.</param>
/// <param name="CalculatedCostBasis">
/// Cost basis recorded with the estimate for this price; null when none was calculated.
/// </param>
/// <param name="ReplacesCostBasis">True when this pricing replaces or clears the task's stored cost basis.</param>
public sealed record ProjectStructureTaskEstimateRefreshResult(
    ProjectTaskEstimate Estimate,
    ProjectStructureTaskEstimateRefreshStatus Status,
    ProjectStructureTaskEstimateRefreshReason Reason,
    ProjectStructureTaskResourceSelection? Resource,
    ProjectStructureTaskResourceCostQuote? Quote,
    ProjectTaskExpectedCostBasis? CalculatedCostBasis,
    bool ReplacesCostBasis);

public sealed class ProjectStructureTaskEstimateRefreshService(
    ProjectStructureTaskResourceCostService resourceCostService)
{
    public async Task<ProjectStructureTaskEstimateRefreshResult> RefreshAsync(
        Guid projectId,
        ProjectTaskExecutionState executionState,
        ProjectStructureTaskResourceSelection? resource,
        ProjectTaskEstimate estimate,
        ProjectStructureTaskMissingResourcePricingPolicy missingResourcePolicy =
            ProjectStructureTaskMissingResourcePricingPolicy.PreserveManualEstimate,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project is required to refresh task cost.", nameof(projectId));
        }

        if (!Enum.IsDefined(executionState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(executionState),
                executionState,
                "Task execution state is not defined.");
        }

        var normalizedEstimate = ProjectTaskEstimatePolicy.ValidateAndNormalize(estimate);
        if (!ProjectTaskExecutionStatePolicy.AllowsAuthoritativeRepricing(executionState))
        {
            return new ProjectStructureTaskEstimateRefreshResult(
                normalizedEstimate,
                ProjectStructureTaskEstimateRefreshStatus.Preserved,
                ProjectStructureTaskEstimateRefreshReason.ExecutionStateDoesNotAllowRefresh,
                resource,
                null,
                null,
                false);
        }

        if (resource is null)
        {
            if (missingResourcePolicy ==
                ProjectStructureTaskMissingResourcePricingPolicy.ClearAuthoritativeSnapshot)
            {
                return new ProjectStructureTaskEstimateRefreshResult(
                    ProjectTaskEstimatePolicy.ValidateAndNormalize(normalizedEstimate with
                    {
                        ExpectedCostAmount = null,
                        ExpectedCostCurrencyCode = string.Empty
                    }),
                    ProjectStructureTaskEstimateRefreshStatus.Cleared,
                    ProjectStructureTaskEstimateRefreshReason.AuthoritativeResourceRemoved,
                    null,
                    null,
                    null,
                    true);
            }

            return new ProjectStructureTaskEstimateRefreshResult(
                normalizedEstimate,
                ProjectStructureTaskEstimateRefreshStatus.Preserved,
                ProjectStructureTaskEstimateRefreshReason.NoResourceSelected,
                null,
                null,
                null,
                false);
        }

        var quote = await resourceCostService.GetQuoteAsync(
            new ProjectStructureTaskResourceCostRequest(
                projectId,
                resource,
                normalizedEstimate),
            cancellationToken);
        if (!quote.IsAvailable)
        {
            return new ProjectStructureTaskEstimateRefreshResult(
                ProjectTaskEstimatePolicy.ValidateAndNormalize(normalizedEstimate with
                {
                    ExpectedCostAmount = null,
                    ExpectedCostCurrencyCode = string.Empty
                }),
                ProjectStructureTaskEstimateRefreshStatus.Cleared,
                ProjectStructureTaskEstimateRefreshReason.AuthoritativeQuoteUnavailable,
                resource,
                quote,
                ProjectTaskExpectedCostBasisPolicy.Create(resource, quote),
                true);
        }

        var refreshedEstimate = ProjectTaskEstimatePolicy.ValidateAndNormalize(normalizedEstimate with
        {
            ExpectedCostAmount = quote.Amount,
            ExpectedCostCurrencyCode = quote.CurrencyCode
        });
        return new ProjectStructureTaskEstimateRefreshResult(
            refreshedEstimate,
            ProjectStructureTaskEstimateRefreshStatus.Refreshed,
            ProjectStructureTaskEstimateRefreshReason.AuthoritativeQuoteApplied,
            resource,
            quote,
            ProjectTaskExpectedCostBasisPolicy.Create(resource, quote),
            true);
    }
}
