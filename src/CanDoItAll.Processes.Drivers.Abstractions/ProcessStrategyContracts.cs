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

public sealed record ProcessStrategyExecutionContext(
    ProcessRunId RunId,
    ProcessStepInstanceId? StepId,
    ProcessStrategyBindingSnapshot Binding,
    IReadOnlyList<StrategyBindingInput> Inputs);

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
    string SafeSummary);

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
