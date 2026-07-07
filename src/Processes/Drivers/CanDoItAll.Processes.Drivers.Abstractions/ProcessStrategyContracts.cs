using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Drivers.Abstractions;

public interface IProcessStrategyFactory
{
    ProcessStrategyDescriptor Descriptor { get; }

    ValueTask<IProcessStrategy> CreateAsync(
        ProcessStrategyBindingSnapshot binding,
        CancellationToken cancellationToken = default);
}

public interface IProcessStrategy
{
    ValueTask<StrategyResultEnvelope> ExecuteAsync(
        ProcessStrategyExecutionContext context,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessStrategyBindingSnapshot(
    DriverId DriverId,
    StrategyId StrategyId,
    string StrategyVersion,
    string FactoryVersion,
    string MinRuntimeSchema,
    string MaxRuntimeSchema,
    string BindingInputsHash,
    IReadOnlyList<StrategyBindingInput> Inputs);

public sealed record StrategyBindingInput(
    StrategyBindingInputKey Key,
    string ValueHash);

public sealed record ProcessStrategyExecutionContext
{
    public ProcessStrategyExecutionContext(
        ProcessRunId runId,
        ProcessStepInstanceId? stepId,
        ProcessStrategyBindingSnapshot binding,
        IReadOnlyList<StrategyBindingInput> inputs,
        ProcessStepExecutionContract? stepContract = null)
    {
        RunId = runId;
        StepId = stepId;
        Binding = binding;
        Inputs = inputs;
        StepContract = stepContract ?? ProcessStepExecutionContract.Empty;
    }

    public ProcessRunId RunId { get; init; }

    public ProcessStepInstanceId? StepId { get; init; }

    public ProcessStrategyBindingSnapshot Binding { get; init; }

    public IReadOnlyList<StrategyBindingInput> Inputs { get; init; }

    public ProcessStepExecutionContract StepContract { get; init; }
}

public sealed record ProcessStepExecutionContract(
    IReadOnlyList<RequiredArtifactInputRef> RequiredArtifacts,
    IReadOnlyList<ExpectedProducedArtifactRef> ExpectedProducedArtifacts,
    IReadOnlyList<string> RequiredRuntimeToolNames,
    string ContractHash)
{
    public static ProcessStepExecutionContract Empty { get; } = new([], [], [], "sha256:empty-step-contract");
}

public sealed record RequiredArtifactInputRef(
    ArtifactSlotId SlotId,
    ProcessArtifactInputAvailability Availability,
    ProcessStepInstanceId? ProducerStepId,
    ArtifactInstanceId? ArtifactId,
    string ContentHash,
    string ConnectionHash);

public sealed record ExpectedProducedArtifactRef(
    ArtifactSlotId SlotId);

public enum ProcessArtifactInputAvailability
{
    Expected,
    Available,
    Missing
}

public sealed record StrategyResultEnvelope(
    StrategyId StrategyId,
    string StrategyVersion,
    Guid IdempotencyKey,
    StrategyOutcome Outcome,
    IReadOnlyList<ProducedArtifactRef> ProducedArtifacts,
    IReadOnlyList<RequestedArtifactRef> RequestedArtifacts,
    IReadOnlyList<StrategyDiagnosticRef> Diagnostics,
    IReadOnlyList<ManagerSignal> ManagerSignals,
    string ResultHash);

public sealed record ProducedArtifactRef(
    ArtifactInstanceId ArtifactId,
    ArtifactSlotId SlotId,
    string ContentHash);

public sealed record RequestedArtifactRef(
    ArtifactSlotId SlotId,
    string RequestHash);

public sealed record StrategyDiagnosticRef(
    StrategyDiagnosticCode Code,
    StrategyDiagnosticSensitivity Sensitivity,
    string EvidenceHash,
    string SafeSummary,
    string? RestrictedEvidenceReference = null,
    ProcessDiagnosticRetrySafety RetrySafety = ProcessDiagnosticRetrySafety.Unknown,
    ProcessDiagnosticIdempotencyClassification Idempotency = ProcessDiagnosticIdempotencyClassification.Unknown);

public sealed record ManagerSignal(
    ManagerSignalCode Code,
    string SignalHash,
    string SafeSummary);

public enum StrategyOutcome
{
    Succeeded,
    Failed,
    Waiting,
    NeedsManager,
    Canceled
}

public enum StrategyDiagnosticSensitivity
{
    Normal,
    Restricted
}

public enum ProcessDiagnosticRetrySafety
{
    Unknown,
    SafeToRetry,
    UnsafeToRetry
}

public enum ProcessDiagnosticIdempotencyClassification
{
    Unknown,
    Idempotent,
    NonIdempotent
}
