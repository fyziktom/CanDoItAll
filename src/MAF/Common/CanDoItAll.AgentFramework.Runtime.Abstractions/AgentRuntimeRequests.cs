using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Runtime.Abstractions;

/// <summary>
/// Explicit per-run approval handling policy. This record — not a bare bool —
/// is the contract-level statement of how tool approvals behave for one run.
/// The positional <c>SuppressApprovalRequirements</c> parameters on the
/// request records remain as bounded construction compatibility and always
/// project into this policy.
/// </summary>
public sealed record AgentRuntimeApprovalHandlingPolicy(bool AutoApproveConfiguredApprovals)
{
    /// <summary>Every configured approval gate stays active (fail-closed default).</summary>
    public static AgentRuntimeApprovalHandlingPolicy RequireConfiguredApprovals { get; } = new(false);

    /// <summary>
    /// The operator explicitly enabled auto-approval for this run; configured
    /// approval gates are satisfied without a user decision.
    /// </summary>
    public static AgentRuntimeApprovalHandlingPolicy AutoApprove { get; } = new(true);

    public static AgentRuntimeApprovalHandlingPolicy FromLegacyFlag(bool suppressApprovalRequirements)
        => suppressApprovalRequirements ? AutoApprove : RequireConfiguredApprovals;
}

public sealed record AgentRuntimeExecutionRequest(
    AgentDefinition Agent,
    ProviderProfile Provider,
    ChatSessionRecord Session,
    IReadOnlyList<CapabilityCatalogItem> Capabilities,
    IReadOnlyList<AgentMemoryRecord> Memory,
    string Prompt,
    string? RuntimeSessionKey,
    Func<ExecutionState, string, string, Task> ProgressCallback,
    bool SuppressApprovalRequirements = false,
    AgentStructuredOutputContract? StructuredOutput = null,
    AgentRuntimeExecutionOptions? ExecutionOptions = null)
{
    /// <summary>The explicit approval policy for this run.</summary>
    public AgentRuntimeApprovalHandlingPolicy ApprovalPolicy =>
        AgentRuntimeApprovalHandlingPolicy.FromLegacyFlag(SuppressApprovalRequirements);
}

public sealed record AgentRuntimeApprovalDecision(string ProposalId, bool Approved)
{
    private readonly string proposalId = ValidateProposalId(ProposalId);

    public string ProposalId
    {
        get => proposalId;
        init => proposalId = ValidateProposalId(value);
    }

    private static string ValidateProposalId(string proposalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proposalId);
        return proposalId;
    }
}

public sealed record AgentRuntimeContinuationRequest(
    AgentDefinition Agent,
    ProviderProfile Provider,
    ChatSessionRecord Session,
    IReadOnlyList<CapabilityCatalogItem> Capabilities,
    IReadOnlyList<AgentMemoryRecord> Memory,
    IReadOnlyList<AgentRuntimeApprovalDecision> Decisions,
    string? RuntimeSessionKey,
    Func<ExecutionState, string, string, Task> ProgressCallback,
    bool SuppressApprovalRequirements = false,
    AgentStructuredOutputContract? StructuredOutput = null,
    AgentRuntimeExecutionOptions? ExecutionOptions = null)
{
    private readonly IReadOnlyList<AgentRuntimeApprovalDecision> decisions =
        ValidateDecisions(Decisions);

    public IReadOnlyList<AgentRuntimeApprovalDecision> Decisions
    {
        get => decisions;
        init => decisions = ValidateDecisions(value);
    }

    /// <summary>The explicit approval policy for this continuation.</summary>
    public AgentRuntimeApprovalHandlingPolicy ApprovalPolicy =>
        AgentRuntimeApprovalHandlingPolicy.FromLegacyFlag(SuppressApprovalRequirements);

    public bool AllDecisionsApproved => Decisions.Count > 0 && Decisions.All(d => d.Approved);

    public IReadOnlySet<string> ResolvedApprovalRequestIds { get; init; } =
        new HashSet<string>(StringComparer.Ordinal);

    private static IReadOnlyList<AgentRuntimeApprovalDecision> ValidateDecisions(
        IReadOnlyList<AgentRuntimeApprovalDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        if (decisions.Count == 0)
        {
            throw new ArgumentException(
                "A continuation request requires at least one approval decision.",
                nameof(decisions));
        }

        return decisions;
    }
}
