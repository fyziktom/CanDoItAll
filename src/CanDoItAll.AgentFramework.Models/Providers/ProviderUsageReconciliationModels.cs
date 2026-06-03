namespace CanDoItAll.AgentFramework.Models;

public enum ProviderUsageReconciliationStatus
{
    Matched,
    TokenMismatch,
    InternalOnly,
    ExternalOnly,
    UnknownInternalUsage
}

public sealed record ProviderUsageExternalRecord(
    string ProviderResponseId,
    string ProviderRequestId,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    decimal? CostUsd);

public sealed record ProviderUsageReconciliationEntry(
    string ProviderResponseId,
    string ProviderRequestId,
    string SourcePhases,
    int InternalTotalTokens,
    int ExternalTotalTokens,
    int TokenDelta,
    decimal? InternalCostUsd,
    decimal? ExternalCostUsd,
    decimal? CostDeltaUsd,
    ProviderUsageReconciliationStatus Status);

public sealed record ProviderUsageReconciliationReport(
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ProviderUsageReconciliationEntry> Entries)
{
    public int MatchedCount => Entries.Count(entry => entry.Status == ProviderUsageReconciliationStatus.Matched);

    public int UnresolvedCount => Entries.Count(entry => entry.Status != ProviderUsageReconciliationStatus.Matched);
}

public static class ProviderUsageReconciliationReporter
{
    public static ProviderUsageReconciliationReport Create(
        IReadOnlyList<ProviderUsageObservation> internalObservations,
        IReadOnlyList<ProviderUsageExternalRecord> externalRecords)
    {
        ArgumentNullException.ThrowIfNull(internalObservations);
        ArgumentNullException.ThrowIfNull(externalRecords);

        var internalByResponseId = internalObservations
            .Where(observation => !string.IsNullOrWhiteSpace(observation.ProviderResponseId))
            .GroupBy(observation => observation.ProviderResponseId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var externalByResponseId = externalRecords
            .Where(record => !string.IsNullOrWhiteSpace(record.ProviderResponseId))
            .GroupBy(record => record.ProviderResponseId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var responseIds = internalByResponseId.Keys
            .Concat(externalByResponseId.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(responseId => responseId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var entries = responseIds
            .Select(responseId => CreateEntry(
                responseId,
                internalByResponseId.TryGetValue(responseId, out var observations) ? observations : [],
                externalByResponseId.TryGetValue(responseId, out var records) ? records : []))
            .ToArray();

        return new ProviderUsageReconciliationReport(DateTimeOffset.UtcNow, entries);
    }

    private static ProviderUsageReconciliationEntry CreateEntry(
        string responseId,
        IReadOnlyList<ProviderUsageObservation> internalObservations,
        IReadOnlyList<ProviderUsageExternalRecord> externalRecords)
    {
        var internalTotalTokens = internalObservations
            .Where(observation => ProviderPricingCalculator.IsKnownUsageStatus(observation.UsageStatus))
            .Sum(observation => observation.TotalTokens);
        var externalTotalTokens = externalRecords.Sum(record => Math.Max(0, record.TotalTokens));
        var internalCost = SumNullable(internalObservations.Select(observation => observation.ProviderCostUsd ?? observation.CalculatedCostUsd));
        var externalCost = SumNullable(externalRecords.Select(record => record.CostUsd));
        var tokenDelta = externalTotalTokens - internalTotalTokens;
        var status = ResolveStatus(internalObservations, externalRecords, tokenDelta);

        return new ProviderUsageReconciliationEntry(
            responseId,
            CoalesceText(
                internalObservations.Select(observation => observation.ProviderRequestId)
                    .Concat(externalRecords.Select(record => record.ProviderRequestId))),
            string.Join(
                ",",
                internalObservations
                    .Select(observation => observation.SourcePhase)
                    .Where(phase => !string.IsNullOrWhiteSpace(phase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(phase => phase, StringComparer.OrdinalIgnoreCase)),
            internalTotalTokens,
            externalTotalTokens,
            tokenDelta,
            internalCost,
            externalCost,
            internalCost.HasValue && externalCost.HasValue
                ? externalCost.Value - internalCost.Value
                : null,
            status);
    }

    private static ProviderUsageReconciliationStatus ResolveStatus(
        IReadOnlyList<ProviderUsageObservation> internalObservations,
        IReadOnlyList<ProviderUsageExternalRecord> externalRecords,
        int tokenDelta)
    {
        if (internalObservations.Count == 0)
        {
            return ProviderUsageReconciliationStatus.ExternalOnly;
        }

        if (internalObservations.Any(observation => !ProviderPricingCalculator.IsKnownUsageStatus(observation.UsageStatus)))
        {
            return ProviderUsageReconciliationStatus.UnknownInternalUsage;
        }

        if (externalRecords.Count == 0)
        {
            return ProviderUsageReconciliationStatus.InternalOnly;
        }

        return tokenDelta == 0
            ? ProviderUsageReconciliationStatus.Matched
            : ProviderUsageReconciliationStatus.TokenMismatch;
    }

    private static decimal? SumNullable(IEnumerable<decimal?> values)
    {
        var known = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return known.Length == 0
            ? null
            : known.Sum();
    }

    private static string CoalesceText(IEnumerable<string> values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
