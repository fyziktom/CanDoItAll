using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.ObservationAggregation;
using CanDoItAll.Processes.Drivers.VerificationGateway;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDriverObservationAggregationReadOnlyAdapter
{
    private readonly Func<ProcessDriverObservationAggregationRequest, ProcessDriverObservationAggregate> aggregateObservations;

    public ProcessDriverObservationAggregationReadOnlyAdapter()
        : this(ProcessDriverVerificationGateway.CreateDefault().AggregateObservations)
    {
    }

    internal ProcessDriverObservationAggregationReadOnlyAdapter(
        Func<ProcessDriverObservationAggregationRequest, ProcessDriverObservationAggregate> aggregateObservations)
    {
        this.aggregateObservations = aggregateObservations ?? throw new ArgumentNullException(nameof(aggregateObservations));
    }

    public ProcessDriverObservationAggregationReadOnlyObservation Aggregate(
        ProcessDriverObservationAggregationReadOnlyPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var request = new ProcessDriverObservationAggregationRequest(
            payload.Responses,
            payload.RequestedAt,
            payload.CallerContext.Trim());

        return ProcessDriverObservationAggregationMapper.Create(payload, aggregateObservations(request));
    }
}

internal static class ProcessDriverObservationAggregationMapper
{
    public static ProcessDriverObservationAggregationReadOnlyObservation Create(
        ProcessDriverObservationAggregationReadOnlyPayload payload,
        ProcessDriverObservationAggregate aggregate)
    {
        return new ProcessDriverObservationAggregationReadOnlyObservation(
            payload.ProcessRunId,
            payload.StepRunId,
            aggregate.ResponseCount,
            aggregate.AcceptedCount,
            aggregate.DeniedCount,
            aggregate.DiagnosticCount,
            aggregate.ErrorCount,
            aggregate.WarningCount,
            aggregate.AggregationMutationFree,
            aggregate.AllResponsesMutationFree,
            aggregate.LaneSummaries,
            aggregate.EvidenceReferences,
            aggregate.Redaction,
            aggregate.ContractVersion,
            payload.RequestedAt,
            ProcessReadOnlyObservationClock.ObservedAt(payload.RequestedAt));
    }
}

internal sealed record ProcessDriverObservationAggregationReadOnlyPayload(
    Guid ProcessRunId,
    Guid StepRunId,
    string CallerContext,
    IReadOnlyList<ProcessDriverVerificationResponse> Responses,
    DateTimeOffset RequestedAt);

internal sealed record ProcessDriverObservationAggregationReadOnlyObservation(
    Guid ProcessRunId,
    Guid StepRunId,
    int ResponseCount,
    int AcceptedCount,
    int DeniedCount,
    int DiagnosticCount,
    int ErrorCount,
    int WarningCount,
    bool AggregationMutationFree,
    bool AllResponsesMutationFree,
    IReadOnlyList<ProcessDriverObservationLaneSummary> LaneSummaries,
    IReadOnlyList<ProcessDriverEvidenceReference> EvidenceReferences,
    ProcessDriverRedactionDescriptor Redaction,
    ProcessDriverContractVersion ContractVersion,
    DateTimeOffset RequestedAt,
    DateTimeOffset ObservedAt);
