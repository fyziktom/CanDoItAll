using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessDryRunExecutionHost
{
    Task<ProcessDryRunExecutionHostResult> EvaluateAsync(
        ProcessDryRunExecutionRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class ProcessDryRunExecutionHost : IProcessDryRunExecutionHost
{
    [ActivatorUtilitiesConstructor]
    public ProcessDryRunExecutionHost(ProcessDryRunExecutionPipeline pipeline)
    {
        this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }

    public ProcessDryRunExecutionHost(ProcessExecutionCapableDriverFutureGate futureGate)
        : this(ProcessDryRunExecutionPipeline.CreateDefault(futureGate))
    {
    }

    private readonly ProcessDryRunExecutionPipeline pipeline;

    public Task<ProcessDryRunExecutionHostResult> EvaluateAsync(
        ProcessDryRunExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(pipeline.Evaluate(request, cancellationToken));
    }
}

internal sealed record ProcessDryRunExecutionRequest
{
    public ProcessDryRunExecutionRequest(
        Guid requestId,
        Guid processRunId,
        Guid stepRunId,
        string requestedBy,
        string purpose,
        IReadOnlyList<ProcessExecutionCapableDriverSurface> requestedSurfaces,
        IReadOnlyList<ProcessDriverOperation> requestedOperations,
        ProcessExecutionCapableDriverSandboxPolicy requestedPolicy,
        ProcessExecutionCapableDriverApprovalEvidence approvalEvidence,
        DateTimeOffset requestedAt)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Dry-run execution request id is required.", nameof(requestId));
        }

        if (processRunId == Guid.Empty)
        {
            throw new ArgumentException("Dry-run execution process run id is required.", nameof(processRunId));
        }

        if (stepRunId == Guid.Empty)
        {
            throw new ArgumentException("Dry-run execution step run id is required.", nameof(stepRunId));
        }

        if (string.IsNullOrWhiteSpace(requestedBy))
        {
            throw new ArgumentException("Dry-run execution requester identity is required.", nameof(requestedBy));
        }

        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("Dry-run execution purpose is required.", nameof(purpose));
        }

        ArgumentNullException.ThrowIfNull(requestedSurfaces);
        ArgumentNullException.ThrowIfNull(requestedOperations);
        ArgumentNullException.ThrowIfNull(requestedPolicy);
        ArgumentNullException.ThrowIfNull(approvalEvidence);

        if (requestedSurfaces.Count == 0 && requestedOperations.Count == 0)
        {
            throw new ArgumentException("Dry-run execution requires at least one requested surface or operation.", nameof(requestedSurfaces));
        }

        if (requestedSurfaces.Any(surface => !Enum.IsDefined(surface)))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedSurfaces), "Dry-run execution request contains an unsupported surface.");
        }

        if (requestedOperations.Any(operation => !Enum.IsDefined(operation)))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedOperations), "Dry-run execution request contains an unsupported operation.");
        }

        RequestId = requestId;
        ProcessRunId = processRunId;
        StepRunId = stepRunId;
        RequestedBy = requestedBy.Trim();
        Purpose = purpose.Trim();
        RequestedSurfaces = requestedSurfaces.Distinct().ToArray();
        RequestedOperations = requestedOperations.Distinct().ToArray();
        RequestedPolicy = requestedPolicy;
        ApprovalEvidence = approvalEvidence;
        RequestedAt = requestedAt;
    }

    public Guid RequestId { get; }

    public Guid ProcessRunId { get; }

    public Guid StepRunId { get; }

    public string RequestedBy { get; }

    public string Purpose { get; }

    public IReadOnlyList<ProcessExecutionCapableDriverSurface> RequestedSurfaces { get; }

    public IReadOnlyList<ProcessDriverOperation> RequestedOperations { get; }

    public ProcessExecutionCapableDriverSandboxPolicy RequestedPolicy { get; }

    public ProcessExecutionCapableDriverApprovalEvidence ApprovalEvidence { get; }

    public DateTimeOffset RequestedAt { get; }

    public IReadOnlyList<ProcessExecutionCapableDriverSurface> ResolveRequestedSurfaces()
    {
        return RequestedSurfaces
            .Concat(ProcessExecutionCapableDriverSurfaceMatrix.ResolveSurfacesForOperations(RequestedOperations))
            .Distinct()
            .ToArray();
    }
}

internal enum ProcessDryRunExecutionHostDecision
{
    Denied = 0,
    DryRunPlanCreated = 1
}

internal sealed record ProcessDryRunExecutionHostResult(
    string CapabilityKey,
    Guid RequestId,
    Guid ProcessRunId,
    Guid StepRunId,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    ProcessDryRunExecutionHostDecision Decision,
    ProcessExecutionCapableDriverGateResult GateResult,
    ProcessDryRunExecutionPlan Plan,
    IReadOnlyList<ProcessExecutionCapableDriverSurface> DeniedSurfaces,
    IReadOnlyList<ProcessDriverOperation> DeniedOperations,
    IReadOnlyList<ProcessExecutionCapableDriverAuthorizationGap> AuthorizationGaps,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation) {
    public ProcessRuntimeHostContractSnapshot Contract { get; init; } =
        ProcessRuntimeHostContractSnapshot.Create(ProcessRuntimeHostContractSurface.DryRunExecution);
}

internal sealed record ProcessDryRunExecutionPlan(
    string Summary,
    IReadOnlyList<ProcessDryRunExecutionPlanStep> Steps)
{
    public static ProcessDryRunExecutionPlan Create(
        ProcessDryRunExecutionRequest request,
        IReadOnlyList<ProcessExecutionCapableDriverSurface> requestedSurfaces,
        IReadOnlyList<ProcessExecutionCapableDriverSurface> deniedSurfaces,
        IReadOnlyList<ProcessDriverOperation> deniedOperations,
        ProcessExecutionCapableDriverGateResult gateResult)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(requestedSurfaces);
        ArgumentNullException.ThrowIfNull(deniedSurfaces);
        ArgumentNullException.ThrowIfNull(deniedOperations);
        ArgumentNullException.ThrowIfNull(gateResult);

        var summary = gateResult.Decision == ProcessExecutionCapableDriverGateDecision.ApprovedForFutureExecution &&
            deniedSurfaces.Count == 0
            ? "Dry-run plan created; no production effects were executed."
            : "Dry-run request denied; no production effects were executed.";
        return new ProcessDryRunExecutionPlan(
            summary,
            [
                new(
                    ProcessDryRunExecutionPlanStepKind.ValidateRequest,
                    $"Validated {requestedSurfaces.Count} requested surface(s) for {request.Purpose}."),
                new(
                    ProcessDryRunExecutionPlanStepKind.EvaluateFutureGate,
                    $"Future gate decision: {gateResult.Decision}."),
                new(
                    ProcessDryRunExecutionPlanStepKind.DenyProductionEffects,
                    $"Denied {deniedSurfaces.Count} surface(s) and {deniedOperations.Count} side-effect operation(s)."),
                new(
                    ProcessDryRunExecutionPlanStepKind.RecordNoMutation,
                    "Recorded dry-run-only result with no process, transition, finalizer, workspace, storage, command, network, Office, or CRM mutation.")
            ]);
    }
}

internal enum ProcessDryRunExecutionPlanStepKind
{
    ValidateRequest = 1,
    EvaluateFutureGate = 2,
    DenyProductionEffects = 3,
    RecordNoMutation = 4
}

internal sealed record ProcessDryRunExecutionPlanStep(
    ProcessDryRunExecutionPlanStepKind Kind,
    string Description);
