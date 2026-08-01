namespace CanDoItAll.Modules.Workbench;

public enum ProjectStructureTaskEstimateRefreshStatus
{
    Preserved,
    Refreshed,
    Cleared
}

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
