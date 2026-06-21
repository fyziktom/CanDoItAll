using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessManagerRuntimeHostDryRunPlanReadbackDto(
    string CapabilityKey,
    Guid RequestId,
    Guid ProcessRunId,
    Guid StepRunId,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    ProcessDryRunExecutionHostDecision Decision,
    string PlanSummary,
    IReadOnlyList<ProcessManagerRuntimeHostDryRunPlanStepReadbackDto> PlanSteps,
    IReadOnlyList<ProcessExecutionCapableDriverSurface> DeniedSurfaces,
    IReadOnlyList<ProcessDriverOperation> DeniedOperations,
    IReadOnlyList<ProcessExecutionCapableDriverAuthorizationGap> AuthorizationGaps,
    IReadOnlyList<ProcessManagerRuntimeHostDenialReadbackDto> Denials,
    int DeniedSurfaceCount,
    int DeniedOperationCount,
    int AuthorizationGapCount,
    int DenialCount,
    string AuditReferenceId,
    string AuditReferenceContentHash,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation)
{
    public ProcessRuntimeHostContractSnapshot Contract { get; init; } =
        ProcessRuntimeHostContractSnapshot.Create(ProcessRuntimeHostContractSurface.ManagerReadback);
}

internal sealed record ProcessManagerRuntimeHostDryRunPlanStepReadbackDto(
    ProcessDryRunExecutionPlanStepKind Kind,
    string Description);

internal sealed record ProcessManagerRuntimeHostDenialReadbackDto(
    ProcessRuntimeHostDenialCategory Category,
    string Code,
    string Message,
    int SurfaceCount);

internal static class ProcessManagerRuntimeHostDryRunReadbackMapper
{
    public static ProcessManagerRuntimeHostDryRunPlanReadbackDto Project(ProcessDryRunExecutionHostResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var denials = result.Contract.SandboxDecision?.Denials
            .Select(denial => new ProcessManagerRuntimeHostDenialReadbackDto(
                denial.Category,
                denial.Code,
                denial.Message,
                denial.Surfaces.Count))
            .ToArray() ?? [];
        var auditReference = result.Contract.AuditReference;

        return new ProcessManagerRuntimeHostDryRunPlanReadbackDto(
            result.CapabilityKey,
            result.RequestId,
            result.ProcessRunId,
            result.StepRunId,
            result.RequestedBy,
            result.RequestedAt,
            result.Decision,
            result.Plan.Summary,
            result.Plan.Steps
                .Select(step => new ProcessManagerRuntimeHostDryRunPlanStepReadbackDto(step.Kind, step.Description))
                .ToArray(),
            result.DeniedSurfaces,
            result.DeniedOperations,
            result.AuthorizationGaps,
            denials,
            result.DeniedSurfaces.Count,
            result.DeniedOperations.Count,
            result.AuthorizationGaps.Count,
            denials.Length,
            auditReference?.AuditId ?? string.Empty,
            auditReference?.ContentHash ?? string.Empty,
            result.NoMutationPerformed,
            result.AllowsProcessMutation,
            result.AllowsTransitionMutation,
            result.AllowsFinalizerMutation)
        {
            Contract = result.Contract
        };
    }
}
