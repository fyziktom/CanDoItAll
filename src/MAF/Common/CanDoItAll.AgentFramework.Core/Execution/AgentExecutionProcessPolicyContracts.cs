using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core.Execution;

/// <summary>
/// Typed admission facts generic execution offers to provider-selection policies. <see cref="SourceKind"/> is raw
/// context for policy diagnostics; <see cref="IsGovernedProcessStep"/> is the canonical typed governed signal (the
/// same signal <see cref="IAgentExecutionRunCriticalityPolicy"/> derives) so policies never re-derive it from a
/// source-kind string comparison of their own.
/// </summary>
public sealed record AgentExecutionProviderSelectionRequest(
    string SourceKind,
    bool IsGovernedProcessStep,
    Type? StructuredOutputType,
    string? StructuredOutputContractKey,
    ProviderProfile ConfiguredProvider);

/// <summary>
/// A pluggable, product-owned strategy that can override the configured provider for a governed execution request.
/// Generic Core invokes registered policies in DI registration order and applies the first one whose
/// <see cref="ShouldOverrideConfiguredProvider"/> returns true.
/// </summary>
public interface IAgentExecutionProviderSelectionPolicy
{
    bool ShouldOverrideConfiguredProvider(AgentExecutionProviderSelectionRequest request);

    IReadOnlyList<ProviderProfile> SelectOverrideCandidates(
        AgentExecutionProviderSelectionRequest request,
        IReadOnlyList<ProviderProfile> availableProviders);
}

/// <summary>
/// The minimal run identity facts needed to decide machine criticality (governed hardening such as suppressing
/// injected memory or requiring strict finalizer sequencing).
/// </summary>
public sealed record AgentExecutionRunCriticalitySnapshot(
    string SourceKind,
    string? ProcessRunId,
    string? ProcessStepId);

/// <summary>
/// A pluggable, product-owned strategy that decides whether a run is machine-critical (subject to governed
/// hardening). Generic Core treats the run as critical when any registered policy agrees; an empty policy list is
/// the fail-open default for hosts that never load a product module (governed hardening off).
/// </summary>
public interface IAgentExecutionRunCriticalityPolicy
{
    bool IsMachineCritical(AgentExecutionRunCriticalitySnapshot run);
}
