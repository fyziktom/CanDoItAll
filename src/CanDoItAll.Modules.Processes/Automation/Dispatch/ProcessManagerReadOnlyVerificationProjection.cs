using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.ObservationAggregation;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessManagerReadOnlyVerificationProjectionMapper
{
    public static ProcessManagerReadOnlyVerificationProjection Project(
        ProcessManagerReadOnlyVerificationProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Observation);

        if (!Enum.IsDefined(request.Mode))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Mode, "Unsupported manager verification projection mode.");
        }

        if (request.Mode != ProcessManagerReadOnlyVerificationProjectionMode.None &&
            string.IsNullOrWhiteSpace(request.RequestedBy))
        {
            throw new ArgumentException("Attached manager verification projections require a requesting manager identity.", nameof(request));
        }

        var attached = request.Mode != ProcessManagerReadOnlyVerificationProjectionMode.None;
        return new ProcessManagerReadOnlyVerificationProjection(
            request.Observation.ProcessRunId,
            request.Observation.StepRunId,
            request.Observation.CallerContext,
            request.Mode,
            ProcessManagerReadOnlyVerificationProjectionSource.SuppliedEvidenceOnly,
            attached,
            request.Mode == ProcessManagerReadOnlyVerificationProjectionMode.Diagnostics
                ? CreateDiagnostics(request.Observation)
                : [],
            request.Mode == ProcessManagerReadOnlyVerificationProjectionMode.EvidenceEnvelope
                ? CreateEvidenceEnvelope(request.Observation)
                : null,
            NoMutationPerformed: true,
            AllowsProcessMutation: false,
            AllowsTransitionMutation: false,
            AllowsFinalizerMutation: false,
            request.RequestedBy.Trim(),
            request.RequestedAt,
            ProcessReadOnlyObservationClock.ObservedAt(request.RequestedAt));
    }

    private static IReadOnlyList<ProcessManagerReadOnlyVerificationDiagnostic> CreateDiagnostics(
        ProcessReadOnlyVerificationBatchObservation observation)
    {
        var diagnostics = new List<ProcessManagerReadOnlyVerificationDiagnostic>();
        foreach (var response in observation.Responses)
        {
            var lane = ResolveLane(response);
            foreach (var diagnostic in response.Diagnostics)
            {
                diagnostics.Add(new ProcessManagerReadOnlyVerificationDiagnostic(
                    lane,
                    diagnostic.Severity,
                    diagnostic.Category,
                    diagnostic.Message,
                    [diagnostic.EvidenceReference],
                    response.ContractVersion));
            }
        }

        return Array.AsReadOnly(diagnostics.ToArray());
    }

    private static ProcessManagerReadOnlyVerificationEvidenceEnvelope CreateEvidenceEnvelope(
        ProcessReadOnlyVerificationBatchObservation observation)
    {
        var aggregate = observation.AggregateObservation
            ?? throw new InvalidOperationException("A manager evidence envelope requires an aggregate read-only verification observation.");

        return new ProcessManagerReadOnlyVerificationEvidenceEnvelope(
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
            aggregate.RequestedAt,
            aggregate.ObservedAt);
    }

    private static ProcessDriverCapabilityScopeKind? ResolveLane(ProcessDriverVerificationResponse response)
    {
        var lanes = response.AuditFacts
            .Select(fact => fact.Lane)
            .Distinct()
            .ToArray();

        return lanes.Length == 1
            ? lanes[0]
            : null;
    }
}

internal sealed record ProcessManagerReadOnlyVerificationProjectionRequest(
    ProcessReadOnlyVerificationBatchObservation Observation,
    ProcessManagerReadOnlyVerificationProjectionMode Mode,
    string RequestedBy,
    DateTimeOffset RequestedAt);

internal enum ProcessManagerReadOnlyVerificationProjectionMode
{
    None = 0,
    Diagnostics = 1,
    EvidenceEnvelope = 2
}

internal enum ProcessManagerReadOnlyVerificationProjectionSource
{
    SuppliedEvidenceOnly = 1
}

internal sealed record ProcessManagerReadOnlyVerificationProjection(
    Guid ProcessRunId,
    Guid StepRunId,
    string CallerContext,
    ProcessManagerReadOnlyVerificationProjectionMode Mode,
    ProcessManagerReadOnlyVerificationProjectionSource Source,
    bool IsAttached,
    IReadOnlyList<ProcessManagerReadOnlyVerificationDiagnostic> Diagnostics,
    ProcessManagerReadOnlyVerificationEvidenceEnvelope? EvidenceEnvelope,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    DateTimeOffset ObservedAt);

internal sealed record ProcessManagerReadOnlyVerificationDiagnostic(
    ProcessDriverCapabilityScopeKind? Lane,
    ProcessDriverDiagnosticSeverity Severity,
    ProcessDriverDiagnosticCategory Category,
    string Message,
    IReadOnlyList<ProcessDriverEvidenceReference> EvidenceReferences,
    ProcessDriverContractVersion ContractVersion);

internal sealed record ProcessManagerReadOnlyVerificationEvidenceEnvelope(
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
