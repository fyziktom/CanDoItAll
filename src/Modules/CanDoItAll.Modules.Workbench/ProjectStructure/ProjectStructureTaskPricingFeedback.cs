namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureTaskPricingFeedback
{
    private const int MaximumSummaryLength = 240;

    public static string BuildNotificationSuffix(
        ProjectStructureTaskEstimateRefreshResult pricing)
    {
        ArgumentNullException.ThrowIfNull(pricing);
        return pricing.Status switch
        {
            ProjectStructureTaskEstimateRefreshStatus.Refreshed =>
                BuildQuoteSuffix(
                    "The expected cost was refreshed from the authoritative resource source.",
                    pricing.Quote),
            ProjectStructureTaskEstimateRefreshStatus.Cleared
                when pricing.Reason == ProjectStructureTaskEstimateRefreshReason.AuthoritativeQuoteUnavailable =>
                BuildQuoteSuffix(
                    "The stale expected cost was removed because the authoritative source could not provide a price.",
                    pricing.Quote),
            ProjectStructureTaskEstimateRefreshStatus.Cleared =>
                " The authoritative resource was removed, so the stale expected cost was cleared.",
            _ => string.Empty
        };
    }

    private static string BuildQuoteSuffix(
        string outcome,
        ProjectStructureTaskResourceCostQuote? quote)
    {
        var summary = NormalizeSummary(quote?.Summary);
        return string.IsNullOrEmpty(summary)
            ? $" {outcome}"
            : $" {outcome} {summary}";
    }

    private static string NormalizeSummary(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return string.Empty;
        }

        var normalized = string.Join(
            ' ',
            summary.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= MaximumSummaryLength
            ? normalized
            : $"{normalized[..(MaximumSummaryLength - 3)]}...";
    }
}
