namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentToolCommittedEffect(
    string SourceKind,
    string SourceId);

public sealed record AgentToolPreDispatchFailure(string FailureCode, string SafeMessage) : IAgentToolInvocationResultEvidence {
    public AgentToolInvocationOutcome Outcome => AgentToolInvocationOutcome.Failed;
    public AgentToolEffectState EffectState => AgentToolEffectState.NotCommitted;
    public bool CanRetryWithCorrectedInput { get; init; }
}

public sealed class AgentToolInvocationEffectScope : IDisposable {
    /// <summary>
    /// The failure code of a refusal recorded through <see cref="RecordProhibitedBeforeEffect"/>.
    /// </summary>
    public const string OperationProhibitedFailureCode = "OperationProhibited";

    private static readonly AsyncLocal<EffectCapture?> CurrentCapture = new();
    private readonly EffectCapture? previousCapture;
    private readonly EffectCapture capture = new();
    private bool disposed;

    private AgentToolInvocationEffectScope() {
        previousCapture = CurrentCapture.Value;
        CurrentCapture.Value = capture;
    }

    public AgentToolCommittedEffect? CommittedEffect => capture.CommittedEffect;
    public AgentToolProtocolEnvelope? DisclosureEvidence => Volatile.Read(ref capture.DisclosureEvidence);
    public AgentToolPreDispatchFailure? PreDispatchFailure => capture.PreDispatchFailure;

    /// <summary>
    /// The owner returned an unsuccessful result it decided before changing anything. A committed effect always wins.
    /// </summary>
    public bool RejectedBeforeEffect => capture.RejectedBeforeEffect && capture.CommittedEffect is null;

    /// <summary>
    /// The owner refused the requested operation itself, not its input, before any effect.
    /// </summary>
    public bool ProhibitedBeforeEffect => capture.Prohibited && RejectedBeforeEffect;

    /// <summary>
    /// Records that the owner rejected this invocation before any write, launch or other effect, so its returned
    /// failure is a proven no-effect rejection the model may correct. Without an active invocation scope nothing is
    /// recorded.
    /// </summary>
    public static void RecordRejectedBeforeEffect() {
        if (CurrentCapture.Value is { } current) {
            current.RejectedBeforeEffect = true;
        }
    }

    /// <summary>
    /// Records a rejection before any effect because the owner never permits the requested operation in this
    /// context, such as deleting a project file. No retry of that operation can succeed: the refusal directs the
    /// agent to another approach, so the attempt is not work the run still owes. Without an active invocation scope
    /// nothing is recorded.
    /// </summary>
    public static void RecordProhibitedBeforeEffect() {
        if (CurrentCapture.Value is { } current) {
            current.RejectedBeforeEffect = true;
            current.Prohibited = true;
        }
    }

    /// <summary>
    /// Whether the owner of the active invocation has recorded a rejection before any effect and nothing committed.
    /// </summary>
    public static bool IsCurrentRejectedBeforeEffect =>
        CurrentCapture.Value is { RejectedBeforeEffect: true, CommittedEffect: null };

    public static void RecordPreDispatchFailure(AgentToolPreDispatchFailure failure) {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentException.ThrowIfNullOrWhiteSpace(failure.FailureCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(failure.SafeMessage);
        if (CurrentCapture.Value is not { } current) {
            throw new InvalidOperationException("A pre-dispatch failure requires an active invocation scope.");
        }
        if (current.CommittedEffect is not null || current.PreDispatchFailure is { } previous && previous != failure) {
            throw new InvalidOperationException("The invocation already captured incompatible outcome evidence.");
        }
        current.PreDispatchFailure = failure;
    }

    public static AgentToolInvocationEffectScope Begin() {
        return new AgentToolInvocationEffectScope();
    }

    public static void RecordCommitted(string sourceKind, string sourceId) {
        if (CurrentCapture.Value is not { } current ||
            string.IsNullOrWhiteSpace(sourceKind) ||
            string.IsNullOrWhiteSpace(sourceId)) {
            return;
        }

        if (current.PreDispatchFailure is not null) {
            throw new InvalidOperationException("A denied invocation cannot record a committed effect.");
        }
        current.CommittedEffect = new AgentToolCommittedEffect(
            sourceKind.Trim(),
            sourceId.Trim());
    }

    public static void RecordDisclosureEvidence(AgentToolProtocolEnvelope evidence) {
        ArgumentNullException.ThrowIfNull(evidence);
        if (CurrentCapture.Value is not { } current) {
            return;
        }

        var previous = Interlocked.CompareExchange(ref current.DisclosureEvidence, evidence, null);
        if (previous is not null && previous != evidence) {
            throw new InvalidOperationException("One invocation cannot replace its original owner disclosure evidence.");
        }
    }

    public void Dispose() {
        if (disposed) {
            return;
        }

        disposed = true;
        if (ReferenceEquals(CurrentCapture.Value, capture)) {
            CurrentCapture.Value = previousCapture;
        }
    }

    private sealed class EffectCapture {
        public AgentToolCommittedEffect? CommittedEffect { get; set; }
        public AgentToolPreDispatchFailure? PreDispatchFailure { get; set; }
        public bool RejectedBeforeEffect { get; set; }
        public bool Prohibited { get; set; }
        public AgentToolProtocolEnvelope? DisclosureEvidence;
    }
}
