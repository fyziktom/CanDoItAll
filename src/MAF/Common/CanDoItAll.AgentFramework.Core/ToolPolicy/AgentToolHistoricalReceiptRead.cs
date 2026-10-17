using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentToolHistoricalReceiptReadAdmission {
    internal AgentToolHistoricalReceiptReadAdmission(AgentHistoricalReceiptReviewRequest request, AgentToolSessionAdmission originalSession,
        AgentToolApprovalBinding binding, AgentToolPreparedPayload payload, AgentHistoricalReceiptReadFence currentFence,
        long runRevision, long journalRevision) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(originalSession);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(currentFence);
        ArgumentOutOfRangeException.ThrowIfLessThan(runRevision, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(journalRevision, 1);
        if (originalSession.Reference is not { } originalReference || originalReference.ExecutionRunId != request.ExecutionRunId ||
                (originalReference.ChatSessionId == Guid.Empty ? (Guid?)null : originalReference.ChatSessionId) != request.ChatSessionId ||
                originalSession.AgentId != request.AgentId ||
                !Enum.IsDefined(originalSession.Purpose) || originalSession.Profile is not { } originalProfile ||
                originalProfile.ProfileId != currentFence.Profile.ProfileId ||
                !StringComparer.Ordinal.Equals(originalProfile.Fingerprint, currentFence.Profile.Fingerprint) ||
                binding.BatchId.Value == Guid.Empty || binding.IntentId.Value == Guid.Empty ||
                binding.SemanticVersion != payload.SemanticVersion || binding.Digest != payload.Digest ||
                payload.SemanticVersion < 2 || payload.SourcePreparation is null || payload.Recovery != AgentToolProposalRecovery.OwnerReceipt) {
            throw new ArgumentException("Historical receipt review requires the unchanged original admission and prepared owner identity.");
        }
        Request = request;
        OriginalSession = originalSession;
        Binding = binding;
        Payload = payload;
        CurrentFence = currentFence;
        RunRevision = runRevision;
        JournalRevision = journalRevision;
    }

    public AgentHistoricalReceiptReviewRequest Request { get; }
    public AgentToolSessionAdmission OriginalSession { get; }
    public AgentToolApprovalBinding Binding { get; }
    public AgentToolPreparedPayload Payload { get; }
    public AgentHistoricalReceiptReadFence CurrentFence { get; }
    public long RunRevision { get; }
    public long JournalRevision { get; }
}

public sealed record AgentToolHistoricalReceiptObservation {
    public AgentToolHistoricalReceiptObservation(AgentHistoricalReceiptKnowledge knowledge, AgentToolReceiptObservation? receipt = null) {
        if (!Enum.IsDefined(knowledge) || knowledge == AgentHistoricalReceiptKnowledge.Accepted && receipt?.CommittedEffect is null ||
                knowledge != AgentHistoricalReceiptKnowledge.Accepted && receipt is not null) {
            throw new ArgumentException("Historical receipt knowledge and its owner evidence do not agree.");
        }
        Knowledge = knowledge;
        Receipt = receipt;
    }

    public AgentHistoricalReceiptKnowledge Knowledge { get; }
    public AgentToolReceiptObservation? Receipt { get; }
}

public interface IAgentToolHistoricalReceiptReadProvider {
    bool Supports(string toolName);
    ValueTask<AgentToolHistoricalReceiptObservation> ReadAsync(AgentToolHistoricalReceiptReadClaim claim,
        CancellationToken cancellationToken = default);
}

public sealed class AgentToolHistoricalReceiptReadClaim {
    private enum ClaimState { Created, Active, Retired }
    private readonly AgentToolHistoricalReceiptReadAdmission admission;
    private readonly Func<AgentToolHistoricalReceiptReadAdmission, CancellationToken, ValueTask> validateCurrentRead;
    private CancellationToken readCancellation;
    private int state;

    internal AgentToolHistoricalReceiptReadClaim(AgentToolHistoricalReceiptReadAdmission admission,
        Func<AgentToolHistoricalReceiptReadAdmission, CancellationToken, ValueTask> validateCurrentRead) {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(validateCurrentRead);
        this.admission = admission;
        this.validateCurrentRead = validateCurrentRead;
    }

    public async ValueTask<AgentToolHistoricalReceiptReadAdmission> RequireAsync(CancellationToken cancellationToken = default) {
        RequireActive();
        using var linked = cancellationToken.CanBeCanceled && cancellationToken != readCancellation
            ? CancellationTokenSource.CreateLinkedTokenSource(readCancellation, cancellationToken) : null;
        var effective = linked?.Token ?? readCancellation;
        effective.ThrowIfCancellationRequested();
        await validateCurrentRead(admission, effective).ConfigureAwait(false);
        effective.ThrowIfCancellationRequested();
        RequireActive();
        return admission;
    }

    internal async ValueTask<AgentToolHistoricalReceiptObservation> ReadAsync(IAgentToolHistoricalReceiptReadProvider provider,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(provider);
        if (Interlocked.CompareExchange(ref state, (int)ClaimState.Active, (int)ClaimState.Created) != (int)ClaimState.Created) {
            throw new InvalidOperationException("A historical receipt claim can be used for only one bounded read.");
        }
        readCancellation = cancellationToken;
        try {
            await RequireAsync(cancellationToken).ConfigureAwait(false);
            if (!provider.Supports(admission.Payload.ToolName)) {
                return new(AgentHistoricalReceiptKnowledge.Unsupported);
            }
            var observation = await provider.ReadAsync(this, cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(observation);
            await RequireAsync(cancellationToken).ConfigureAwait(false);
            return observation;
        } finally {
            Retire();
        }
    }

    internal void Retire() => Interlocked.Exchange(ref state, (int)ClaimState.Retired);

    private void RequireActive() {
        if (Volatile.Read(ref state) != (int)ClaimState.Active) {
            throw new InvalidOperationException("A live original-bound historical receipt read is required.");
        }
    }
}
