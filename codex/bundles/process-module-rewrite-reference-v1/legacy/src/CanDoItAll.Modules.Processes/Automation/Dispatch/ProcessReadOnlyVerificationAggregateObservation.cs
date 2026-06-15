using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.ObservationAggregation;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessReadOnlyVerificationAggregateObservationMapper
{
    public static ProcessReadOnlyVerificationAggregateObservation Create(
        ProcessReadOnlyVerificationBatchPayload payload,
        ProcessDriverObservationAggregationReadOnlyObservation aggregateObservation)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(aggregateObservation);

        return new ProcessReadOnlyVerificationAggregateObservation(
            payload.ProcessRunId,
            payload.StepRunId,
            payload.CallerContext,
            aggregateObservation.ResponseCount,
            aggregateObservation.AcceptedCount,
            aggregateObservation.DeniedCount,
            aggregateObservation.DiagnosticCount,
            aggregateObservation.ErrorCount,
            aggregateObservation.WarningCount,
            aggregateObservation.AggregationMutationFree,
            aggregateObservation.AllResponsesMutationFree,
            aggregateObservation.LaneSummaries,
            aggregateObservation.EvidenceReferences,
            aggregateObservation.Redaction,
            aggregateObservation.ContractVersion,
            payload.RequestedAt,
            ProcessReadOnlyObservationClock.ObservedAt(payload.RequestedAt));
    }
}

internal sealed record ProcessReadOnlyVerificationAggregateObservation
{
    public ProcessReadOnlyVerificationAggregateObservation(
        Guid processRunId,
        Guid stepRunId,
        string callerContext,
        int responseCount,
        int acceptedCount,
        int deniedCount,
        int diagnosticCount,
        int errorCount,
        int warningCount,
        bool aggregationMutationFree,
        bool allResponsesMutationFree,
        IReadOnlyList<ProcessDriverObservationLaneSummary> laneSummaries,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        ProcessDriverRedactionDescriptor redaction,
        ProcessDriverContractVersion contractVersion,
        DateTimeOffset requestedAt,
        DateTimeOffset observedAt)
    {
        ProcessRunId = processRunId;
        StepRunId = stepRunId;
        CallerContext = callerContext;
        ResponseCount = responseCount;
        AcceptedCount = acceptedCount;
        DeniedCount = deniedCount;
        DiagnosticCount = diagnosticCount;
        ErrorCount = errorCount;
        WarningCount = warningCount;
        AggregationMutationFree = aggregationMutationFree;
        AllResponsesMutationFree = allResponsesMutationFree;
        LaneSummaries = CreateReadonlyList(laneSummaries);
        EvidenceReferences = CreateReadonlyList(evidenceReferences);
        Redaction = redaction;
        ContractVersion = contractVersion;
        RequestedAt = requestedAt;
        ObservedAt = observedAt;
    }

    public Guid ProcessRunId { get; }

    public Guid StepRunId { get; }

    public string CallerContext { get; }

    public int ResponseCount { get; }

    public int AcceptedCount { get; }

    public int DeniedCount { get; }

    public int DiagnosticCount { get; }

    public int ErrorCount { get; }

    public int WarningCount { get; }

    public bool AggregationMutationFree { get; }

    public bool AllResponsesMutationFree { get; }

    public IReadOnlyList<ProcessDriverObservationLaneSummary> LaneSummaries { get; }

    public IReadOnlyList<ProcessDriverEvidenceReference> EvidenceReferences { get; }

    public ProcessDriverRedactionDescriptor Redaction { get; }

    public ProcessDriverContractVersion ContractVersion { get; }

    public DateTimeOffset RequestedAt { get; }

    public DateTimeOffset ObservedAt { get; }

    private static IReadOnlyList<T> CreateReadonlyList<T>(IEnumerable<T> values)
    {
        return Array.AsReadOnly(values.ToArray());
    }
}
