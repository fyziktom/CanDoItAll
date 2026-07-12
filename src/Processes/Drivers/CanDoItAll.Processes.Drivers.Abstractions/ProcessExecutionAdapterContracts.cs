using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Drivers.Abstractions;

public interface IProcessExecutionAdapter
{
    ProcessExecutionAdapterDescriptor Descriptor { get; }

    ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(
        ProcessExecutionAdapterRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessExecutionAdapterDescriptor(
    ProcessExecutionAdapterId AdapterId,
    ProcessExecutionAdapterKind Kind,
    string AdapterVersion,
    ProcessStrategyDescriptor Strategy,
    IReadOnlySet<CapabilityTag> CapabilityTags);

public sealed record ProcessExecutionAdapterRequest(
    ProcessRunId RunId,
    ProcessStepInstanceId? StepId,
    ProcessExecutionAdapterKind Kind,
    ProcessExecutionAdapterOperationKey OperationKey,
    ProcessStrategyBindingSnapshot Binding,
    IReadOnlyList<StrategyBindingInput> Inputs,
    IReadOnlyList<ProcessExecutionContextFacet> ContextFacets)
{
    public ProcessStepExecutionContract StepContract { get; init; } = ProcessStepExecutionContract.Empty;
}

public sealed record ProcessExecutionContextFacet(
    ProcessExecutionContextFacetKey Key,
    string ValueHash,
    StrategyDiagnosticSensitivity Sensitivity);

public sealed record ProcessExecutionAdapterResult(
    StrategyOutcome Outcome,
    IReadOnlyList<ProducedArtifactRef> ProducedArtifacts,
    IReadOnlyList<RequestedArtifactRef> RequestedArtifacts,
    IReadOnlyList<ProcessExecutionAdapterDiagnostic> Diagnostics,
    IReadOnlyList<ManagerSignal> ManagerSignals,
    string UserSafeSummary,
    string ResultHash)
{
    public static ProcessExecutionAdapterResult Succeeded(
        string userSafeSummary,
        string resultHash)
    {
        return new ProcessExecutionAdapterResult(
            StrategyOutcome.Succeeded,
            [],
            [],
            [],
            [],
            userSafeSummary,
            resultHash);
    }
}

public sealed record ProcessExecutionAdapterDiagnostic(
    StrategyDiagnosticCode Code,
    StrategyDiagnosticSensitivity Sensitivity,
    string EvidenceHash,
    string SafeSummary,
    string? RestrictedEvidenceReference,
    ProcessDiagnosticRetrySafety RetrySafety,
    ProcessDiagnosticIdempotencyClassification Idempotency);

public enum ProcessExecutionAdapterKind
{
    Workflow,
    SingleAgent,
    AgentGroup,
    Handoff,
    SchedulerTrigger,
    ScopedContext,
    Plugin
}
