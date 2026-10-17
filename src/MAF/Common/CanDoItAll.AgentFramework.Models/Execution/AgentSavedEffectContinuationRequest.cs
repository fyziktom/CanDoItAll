using System.Collections.Immutable;
using System.Text;

namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentSavedEffectContinuationRequest {
    public AgentSavedEffectContinuationRequest(Guid executionRunId, Guid? chatSessionId, long expectedRunRevision,
        long expectedJournalRevision, AgentToolApprovalBinding binding, IReadOnlyList<PendingToolApprovalDecision> decisions) {
        AgentSavedEffectRequestValidation.RequireIdentity(executionRunId, chatSessionId, expectedRunRevision, expectedJournalRevision);
        AgentSavedEffectRequestValidation.RequireBinding(binding);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(decisions.Count, AgentToolJournalRecord.MaximumCallsPerBatch);
        var captured = ImmutableArray.CreateBuilder<PendingToolApprovalDecision>(decisions.Count);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var intents = new HashSet<AgentToolBusinessIntentId>();
        long bytes = 0;
        foreach (var decision in decisions) {
            ArgumentNullException.ThrowIfNull(decision);
            ArgumentException.ThrowIfNullOrWhiteSpace(decision.ApprovalId);
            AgentSavedEffectRequestValidation.RequireBinding(decision.ToolAdmission);
            bytes += Encoding.UTF8.GetByteCount(decision.ApprovalId);
            if (!identities.Add(decision.ApprovalId) || !intents.Add(decision.ToolAdmission!.IntentId) ||
                bytes > AgentToolJournalRecord.MaximumUtf8Bytes) {
                throw new ArgumentException("Saved-effect decisions require distinct bounded original approval and intent identities.", nameof(decisions));
            }
            captured.Add(decision with { });
        }
        ExecutionRunId = executionRunId;
        ChatSessionId = chatSessionId;
        ExpectedRunRevision = expectedRunRevision;
        ExpectedJournalRevision = expectedJournalRevision;
        Binding = binding;
        Decisions = captured.MoveToImmutable();
    }

    public Guid ExecutionRunId { get; }
    public Guid? ChatSessionId { get; }
    public long ExpectedRunRevision { get; }
    public long ExpectedJournalRevision { get; }
    public AgentToolApprovalBinding Binding { get; }
    public IReadOnlyList<PendingToolApprovalDecision> Decisions { get; }
}

public sealed record AgentSavedEffectCancellationRequest {
    public AgentSavedEffectCancellationRequest(Guid executionRunId, Guid? chatSessionId, long expectedRunRevision,
        long expectedJournalRevision) {
        AgentSavedEffectRequestValidation.RequireIdentity(executionRunId, chatSessionId, expectedRunRevision, expectedJournalRevision);
        ExecutionRunId = executionRunId;
        ChatSessionId = chatSessionId;
        ExpectedRunRevision = expectedRunRevision;
        ExpectedJournalRevision = expectedJournalRevision;
    }

    public Guid ExecutionRunId { get; }
    public Guid? ChatSessionId { get; }
    public long ExpectedRunRevision { get; }
    public long ExpectedJournalRevision { get; }
}

internal static class AgentSavedEffectRequestValidation {
    internal static void RequireIdentity(Guid runId, Guid? chatId, long runRevision, long journalRevision) {
        ArgumentOutOfRangeException.ThrowIfEqual(runId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThan(runRevision, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(journalRevision, 1);
        if (chatId == Guid.Empty) {
            throw new ArgumentException("An omitted chat identity is null, never an empty identifier.", nameof(chatId));
        }
    }

    internal static void RequireBinding(AgentToolApprovalBinding? binding) {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentOutOfRangeException.ThrowIfEqual(binding.BatchId.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(binding.IntentId.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThan(binding.SemanticVersion, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.Digest.Value);
    }
}
