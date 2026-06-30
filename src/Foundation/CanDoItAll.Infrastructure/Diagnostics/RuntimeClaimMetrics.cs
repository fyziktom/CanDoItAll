using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CanDoItAll.Infrastructure.Diagnostics;

public static class RuntimeClaimMetrics {
    public const string MeterName = "CanDoItAll.Runtime.Claims";
    public const string ClaimedRecordsInstrumentName = "candoitall.runtime.claimed_records";
    public const string ProcessedRecordsInstrumentName = "candoitall.runtime.processed_records";
    public const string StaleFinalizationsInstrumentName = "candoitall.runtime.stale_finalizations";
    public const string DuplicateSuppressionsInstrumentName = "candoitall.runtime.duplicate_suppressions";
    public const string BatchDurationInstrumentName = "candoitall.runtime.batch_duration";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> ClaimedRecords = Meter.CreateCounter<long>(
        ClaimedRecordsInstrumentName,
        unit: "records");

    private static readonly Counter<long> ProcessedRecords = Meter.CreateCounter<long>(
        ProcessedRecordsInstrumentName,
        unit: "records");

    private static readonly Counter<long> StaleFinalizations = Meter.CreateCounter<long>(
        StaleFinalizationsInstrumentName,
        unit: "records");

    private static readonly Counter<long> DuplicateSuppressions = Meter.CreateCounter<long>(
        DuplicateSuppressionsInstrumentName,
        unit: "records");

    private static readonly Histogram<double> BatchDuration = Meter.CreateHistogram<double>(
        BatchDurationInstrumentName,
        unit: "ms");

    public static void RecordBatch(
        string workload,
        int claimedCount,
        int processedCount,
        int requestedBatchSize,
        int effectiveParallelism,
        TimeSpan duration) {
        var tags = CreateBatchTags(workload, requestedBatchSize, effectiveParallelism);
        ClaimedRecords.Add(claimedCount, tags);
        ProcessedRecords.Add(processedCount, tags);
        BatchDuration.Record(duration.TotalMilliseconds, tags);
    }

    public static void RecordStaleFinalization(string workload) {
        StaleFinalizations.Add(1, CreateWorkloadTags(workload));
    }

    public static void RecordDuplicateSuppression(string workload) {
        DuplicateSuppressions.Add(1, CreateWorkloadTags(workload));
    }

    private static TagList CreateBatchTags(string workload, int requestedBatchSize, int effectiveParallelism) {
        var tags = CreateWorkloadTags(workload);
        tags.Add("requested_batch_size", requestedBatchSize);
        tags.Add("effective_parallelism", effectiveParallelism);
        return tags;
    }

    private static TagList CreateWorkloadTags(string workload) {
        return new TagList { { "workload", workload } };
    }
}
