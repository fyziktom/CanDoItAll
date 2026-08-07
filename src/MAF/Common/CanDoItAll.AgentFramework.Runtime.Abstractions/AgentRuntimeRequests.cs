using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Runtime.Abstractions;

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
    AgentRuntimeExecutionOptions? ExecutionOptions = null);

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

    // Bounded legacy semantics until per-proposal continuation lands (SB15):
    // every decision in one request must currently agree; the compatibility
    // facade validates this and replays the single boolean protocol.
    public bool AllDecisionsApproved => Decisions.Count > 0 && Decisions.All(d => d.Approved);

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
