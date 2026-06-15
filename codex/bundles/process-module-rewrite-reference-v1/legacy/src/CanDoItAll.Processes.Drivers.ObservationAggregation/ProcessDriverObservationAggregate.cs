using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.ObservationAggregation;

public sealed record ProcessDriverObservationLaneSummary(
    ProcessDriverCapabilityScopeKind Lane,
    int ResponseCount,
    int AcceptedCount,
    int DeniedCount,
    int DiagnosticCount,
    int ErrorCount,
    int WarningCount,
    int RedactedResponseCount,
    bool AllResponsesMutationFree,
    IReadOnlyList<ProcessDriverDiagnosticCategory> DiagnosticCategories);

public sealed record ProcessDriverObservationAggregate(
    DateTimeOffset RequestedAt,
    string CallerContext,
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
    ProcessDriverContractVersion ContractVersion);
