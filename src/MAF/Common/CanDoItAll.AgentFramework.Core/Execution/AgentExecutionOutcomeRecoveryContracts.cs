using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core.Execution;

/// <summary>
/// The reason a generic execution/finalizer failure is being offered to recovery policies.
/// </summary>
public enum AgentExecutionOutcomeFailureCause
{
    ProviderStreamingTimeout,
    MissingRequiredFinalizer
}

/// <summary>
/// Bounded, provider-neutral read access to a single managed workspace text artifact. Implemented by the runtime
/// adapter (for example a workspace-scoped reader) and passed to policies so they never receive SDK objects.
/// </summary>
public interface IAgentExecutionRecoveryArtifactReader
{
    bool TryReadCompleteTextFile(string relativeManagedPath, out string content);
}

/// <summary>
/// Runtime-neutral evidence describing a completed or failed execution turn that a recovery policy may evaluate.
/// Contains bounded tool traces, output contract identity, and a scoped artifact reader; it never exposes MAF SDK
/// objects.
/// </summary>
public sealed record AgentExecutionOutcomeRecoveryEvidence(
    AgentRuntimeContextIntent ContextIntent,
    AgentExecutionOutcomeFailureCause Cause,
    string FinalizerToolName,
    string? OutputContractKey,
    Type? OutputType,
    IReadOnlyList<AgentToolInvocationTrace> CurrentExecutionToolTraces,
    IAgentExecutionRecoveryArtifactReader ArtifactReader);

public enum AgentExecutionOutcomeRecoveryStatus
{
    NotApplicable,
    Recovered,
    Rejected
}

/// <summary>
/// A recovery policy decision. Policies never mutate the run store directly; Core validates and persists the
/// recovered machine output through the same required-finalizer response assembly used for ordinary completion.
/// </summary>
public sealed record AgentExecutionOutcomeRecoveryDecision(
    AgentExecutionOutcomeRecoveryStatus Status,
    string MachineOutputJson,
    string RecoveryReason,
    string OutcomeStatusLabel,
    string EvidenceReference,
    string Diagnostics)
{
    public static AgentExecutionOutcomeRecoveryDecision NotApplicable(string diagnostics)
        => new(
            AgentExecutionOutcomeRecoveryStatus.NotApplicable,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            diagnostics);

    public static AgentExecutionOutcomeRecoveryDecision Rejected(string diagnostics)
        => new(
            AgentExecutionOutcomeRecoveryStatus.Rejected,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            diagnostics);

    public static AgentExecutionOutcomeRecoveryDecision Recovered(
        string machineOutputJson,
        string recoveryReason,
        string outcomeStatusLabel,
        string evidenceReference,
        string diagnostics)
        => new(
            AgentExecutionOutcomeRecoveryStatus.Recovered,
            machineOutputJson,
            recoveryReason,
            outcomeStatusLabel,
            evidenceReference,
            diagnostics);
}

/// <summary>
/// A pluggable, product-owned strategy that can synthesize a validated finalizer output from evidence gathered
/// after MAF-native bounded repair is exhausted. Generic execution iterates registered policies in DI registration
/// order and stops at the first <see cref="AgentExecutionOutcomeRecoveryStatus.Recovered"/> decision.
/// </summary>
public interface IAgentExecutionOutcomeRecoveryPolicy
{
    AgentExecutionOutcomeRecoveryDecision Evaluate(AgentExecutionOutcomeRecoveryEvidence evidence);
}
