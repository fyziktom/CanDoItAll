namespace CanDoItAll.Processes.Projections;

public readonly record struct LegacyProcessRunId
{
    public LegacyProcessRunId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Legacy process run id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString()
    {
        return Value;
    }
}

public enum ProcessRuntimeHistoryCompatibilityOption
{
    FullMigration,
    ArchiveExport,
    ReadOnlyLegacyProjectionPlusArchive,
    DropAfterExplicitApproval
}

public enum LegacyProcessRuntimeRecordKind
{
    Unknown,
    Run,
    StepRun,
    Assignment,
    Decision,
    Artifact,
    JournalEntry,
    WorkBrief,
    WorkflowLink,
    LaunchPlan,
    Observation,
    ImprovementCandidate
}

public sealed record LegacyProcessRuntimeHistoryRecord(
    LegacyProcessRunId RunId,
    LegacyProcessRuntimeRecordKind Kind,
    string SourceEntityName,
    DateTimeOffset? OccurredAtUtc,
    IReadOnlyList<string> UnmappedFieldNames,
    ProcessProjectedSensitivity Sensitivity);

public sealed record LegacyProcessHistoryEntityInventory(
    LegacyProcessRuntimeRecordKind Kind,
    string SourceEntityName,
    int Count);

public sealed record LegacyProcessHistoryInventoryReport(
    int TotalRecordCount,
    int LegacyRunCount,
    IReadOnlyList<LegacyProcessHistoryEntityInventory> EntityInventories,
    IReadOnlyList<string> UnmappedFieldNames,
    ProcessProjectedSensitivity MaxSensitivity,
    ProcessRuntimeHistoryCompatibilityOption RecommendedOption);

public sealed record LegacyProcessRunProjection(
    LegacyProcessRunId LegacyRunId,
    bool IsReadOnly,
    int RecordCount,
    DateTimeOffset? FirstObservedAtUtc,
    DateTimeOffset? LastObservedAtUtc,
    ProcessProjectedSensitivity Sensitivity,
    IReadOnlyList<string> SourceEntityNames,
    IReadOnlyList<string> UnmappedFieldNames);

public sealed record LegacyProcessHistoryActionDenial(
    LegacyProcessRunId LegacyRunId,
    string RequestedAction,
    LegacyProcessHistoryActionDenialReason Reason,
    string SafeSummary,
    string RestrictedEvidenceReference);

public enum LegacyProcessHistoryActionDenialReason
{
    ReadOnlyLegacyHistory
}

public sealed class LegacyProcessHistoryProjectionAdapter
{
    public LegacyProcessHistoryInventoryReport Inventory(IReadOnlyList<LegacyProcessRuntimeHistoryRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var entityCounts = new Dictionary<(LegacyProcessRuntimeRecordKind Kind, string SourceEntityName), int>();
        var runIds = new HashSet<LegacyProcessRunId>();
        var unmappedFields = new HashSet<string>(StringComparer.Ordinal);
        var maxSensitivity = ProcessProjectedSensitivity.Normal;

        foreach (var record in records)
        {
            runIds.Add(record.RunId);
            var key = (record.Kind, record.SourceEntityName);
            entityCounts[key] = entityCounts.GetValueOrDefault(key) + 1;

            foreach (var field in record.UnmappedFieldNames)
            {
                if (!string.IsNullOrWhiteSpace(field))
                {
                    unmappedFields.Add(field);
                }
            }

            if (record.Sensitivity == ProcessProjectedSensitivity.Restricted)
            {
                maxSensitivity = ProcessProjectedSensitivity.Restricted;
            }
        }

        var inventories = entityCounts
            .OrderBy(item => item.Key.Kind)
            .ThenBy(item => item.Key.SourceEntityName, StringComparer.Ordinal)
            .Select(item => new LegacyProcessHistoryEntityInventory(
                item.Key.Kind,
                item.Key.SourceEntityName,
                item.Value))
            .ToArray();
        var recommendedOption = records.Count == 0
            ? ProcessRuntimeHistoryCompatibilityOption.ArchiveExport
            : ProcessRuntimeHistoryCompatibilityOption.ReadOnlyLegacyProjectionPlusArchive;

        return new LegacyProcessHistoryInventoryReport(
            records.Count,
            runIds.Count,
            inventories,
            unmappedFields.Order(StringComparer.Ordinal).ToArray(),
            maxSensitivity,
            recommendedOption);
    }

    public IReadOnlyList<LegacyProcessRunProjection> ProjectReadOnlyRuns(IReadOnlyList<LegacyProcessRuntimeHistoryRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        return records
            .GroupBy(record => record.RunId)
            .OrderBy(group => group.Key.Value, StringComparer.Ordinal)
            .Select(CreateProjection)
            .ToArray();
    }

    public LegacyProcessHistoryActionDenial DenyRuntimeAction(
        LegacyProcessRunId legacyRunId,
        string requestedAction,
        string restrictedEvidenceReference)
    {
        if (string.IsNullOrWhiteSpace(requestedAction))
        {
            throw new ArgumentException("Requested action is required.", nameof(requestedAction));
        }

        if (string.IsNullOrWhiteSpace(restrictedEvidenceReference))
        {
            throw new ArgumentException("Restricted evidence reference is required.", nameof(restrictedEvidenceReference));
        }

        return new LegacyProcessHistoryActionDenial(
            legacyRunId,
            requestedAction,
            LegacyProcessHistoryActionDenialReason.ReadOnlyLegacyHistory,
            "Legacy process history is read-only; runtime actions require explicit full migration.",
            restrictedEvidenceReference);
    }

    private static LegacyProcessRunProjection CreateProjection(IGrouping<LegacyProcessRunId, LegacyProcessRuntimeHistoryRecord> group)
    {
        var records = group.ToArray();
        var sourceEntities = records
            .Select(record => record.SourceEntityName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var unmappedFields = records
            .SelectMany(record => record.UnmappedFieldNames)
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var timestamps = records
            .Select(record => record.OccurredAtUtc)
            .OfType<DateTimeOffset>()
            .Order()
            .ToArray();
        var sensitivity = records.Any(record => record.Sensitivity == ProcessProjectedSensitivity.Restricted)
            ? ProcessProjectedSensitivity.Restricted
            : ProcessProjectedSensitivity.Normal;

        return new LegacyProcessRunProjection(
            group.Key,
            IsReadOnly: true,
            records.Length,
            timestamps.Length == 0 ? null : timestamps[0],
            timestamps.Length == 0 ? null : timestamps[^1],
            sensitivity,
            sourceEntities,
            unmappedFields);
    }
}
