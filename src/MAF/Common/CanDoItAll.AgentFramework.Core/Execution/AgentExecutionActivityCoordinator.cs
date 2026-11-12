using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Streaming;

namespace CanDoItAll.AgentFramework.Core;

public enum AgentExecutionActivityAdmissionRejectionReason
{
    DuplicateOperation,
    PreviouslyEvicted,
    CapacityExhausted
}

public abstract record AgentExecutionActivityAdmission(
    AgentExecutionActivityStreamId StreamId);

public sealed record AgentExecutionActivityAdmitted(
    AgentExecutionActivityStreamId StreamId,
    IAgentExecutionActivityOperationLease Operation)
    : AgentExecutionActivityAdmission(StreamId);

public sealed record AgentExecutionActivityRejected(
    AgentExecutionActivityStreamId StreamId,
    AgentExecutionActivityAdmissionRejectionReason Reason)
    : AgentExecutionActivityAdmission(StreamId);

public sealed class AgentExecutionActivityAdmissionException(
    AgentExecutionActivityStreamId streamId,
    AgentExecutionActivityAdmissionRejectionReason reason)
    : InvalidOperationException(
        $"Agent execution activity admission was rejected with reason '{reason}'.")
{
    public AgentExecutionActivityStreamId StreamId { get; } = streamId;

    public AgentExecutionActivityAdmissionRejectionReason Reason { get; } = reason;
}

public sealed class AgentExecutionActivityPublicationException : InvalidOperationException {
    public AgentExecutionActivityPublicationException(AgentExecutionActivityStreamId streamId,
        AgentExecutionActivityPhase phase, Exception innerException)
        : base($"Publishing agent execution activity phase '{phase}' failed.", innerException) {
        StreamId = streamId;
        Phase = phase;
    }

    private AgentExecutionActivityPublicationException(AgentExecutionActivityStreamId streamId,
        AgentExecutionActivityPhase phase, Exception innerException, AgentEffectContinuation? continuation,
        AgentEffectReconciliationFailureKind? failureKind, bool leaseReleaseUnconfirmed, bool disposalFailed)
        : this(streamId, phase, innerException) {
        HasEffectOutcome = true;
        EffectContinuation = continuation;
        EffectFailureKind = failureKind;
        EffectLeaseReleaseUnconfirmed = leaseReleaseUnconfirmed;
        ActivityDisposalFailed = disposalFailed;
    }

    public AgentExecutionActivityStreamId StreamId { get; }
    public AgentExecutionActivityPhase Phase { get; }
    public bool HasEffectOutcome { get; }
    public AgentEffectContinuation? EffectContinuation { get; }
    public AgentEffectReconciliationFailureKind? EffectFailureKind { get; }
    public bool EffectLeaseReleaseUnconfirmed { get; }
    public bool ActivityDisposalFailed { get; }

    internal static AgentExecutionActivityPublicationException PreserveEffectOutcome(
        IAgentExecutionActivityOperationLease operation, Exception? primary, Exception publication,
        AgentEffectContinuation? continuation = null, bool disposalFailed = false) {
        var retained = primary as AgentExecutionActivityPublicationException;
        continuation = primary switch {
            AgentExecutionActivityPublicationException { HasEffectOutcome: true } prior => prior.EffectContinuation,
            AgentEffectReconciliationIncompleteException incomplete => incomplete.Continuation,
            AgentEffectReconciliationCleanupException cleanup => cleanup.Continuation,
            _ => continuation
        };
        var kind = primary switch {
            AgentExecutionActivityPublicationException { HasEffectOutcome: true } prior => prior.EffectFailureKind,
            AgentEffectReconciliationLeaseException lease => lease.FailureKind,
            AgentToolReceiptAccessDeniedException => AgentEffectReconciliationFailureKind.ReadDenied,
            AgentToolReceiptReadUnavailableException => AgentEffectReconciliationFailureKind.ReadUnavailable,
            OperationCanceledException => AgentEffectReconciliationFailureKind.Cancelled,
            AgentToolAdmissionException => AgentEffectReconciliationFailureKind.AdmissionRejected,
            null => (AgentEffectReconciliationFailureKind?)null,
            _ => AgentEffectReconciliationFailureKind.Failed
        };
        var leaseUnconfirmed = primary is AgentEffectReconciliationCleanupException or AgentEffectReconciliationLeaseException or
            AgentEffectReconciliationIncompleteException { LeaseReleaseUnconfirmed: true } || retained?.EffectLeaseReleaseUnconfirmed == true;
        var phase = kind == AgentEffectReconciliationFailureKind.Cancelled ? AgentExecutionActivityPhase.Cancelled :
            continuation?.Persistence == AgentEffectCheckpointPersistence.Confirmed ? AgentExecutionActivityPhase.AwaitingContinuation : AgentExecutionActivityPhase.Failed;
        return new(operation.StreamId, phase, primary is null ? publication : new AggregateException(primary, publication),
            continuation, kind, leaseUnconfirmed, disposalFailed || retained?.ActivityDisposalFailed == true);
    }
}

public static class AgentEffectActivityFinalization {
    public static bool IsEffectFailure(Exception? exception) => exception is AgentEffectReconciliationIncompleteException or
        AgentEffectReconciliationCleanupException or AgentEffectReconciliationLeaseException or AgentEffectReconciliationCancellationException or
        AgentToolAdmissionException { IsEffectReconciliation: true } or
        AgentToolReceiptAccessDeniedException or AgentToolReceiptReadUnavailableException or AgentExecutionActivityPublicationException { HasEffectOutcome: true };

    public static AgentEffectContinuation? Continuation<TResult>(TResult result) => result switch {
        ExecutionRunResult run => run.EffectContinuation,
        AgentChatRunResult chat => chat.EffectContinuation,
        _ => null
    };

    public static Exception? Publish(IAgentExecutionActivityOperationLease operation, AgentEffectContinuation? continuation,
        Exception? primary) {
        RequireEffectOutcome(operation, continuation, primary);
        if (primary is AgentExecutionActivityPublicationException { HasEffectOutcome: true }) {
            return primary;
        }
        continuation ??= primary switch {
            AgentEffectReconciliationCleanupException cleanup => cleanup.Continuation,
            AgentEffectReconciliationIncompleteException incomplete => incomplete.Continuation,
            _ => null
        };
        try {
            if (!operation.IsTerminal) {
                if (continuation?.Persistence == AgentEffectCheckpointPersistence.Confirmed) {
                    operation.SuspendForContinuation(continuation, "The original execution checkpoint is confirmed and requires explicit continuation.");
                } else if (primary is OperationCanceledException or AgentEffectReconciliationLeaseException { FailureKind: AgentEffectReconciliationFailureKind.Cancelled }) {
                    operation.Cancel("Receipt reconciliation was cancelled.");
                } else {
                    operation.Fail("Receipt reconciliation needs attention.", AgentExecutionActivityFailureCodes.UnhandledExecutionFailure);
                }
            }
            return primary;
        } catch (Exception publication) {
            return AgentExecutionActivityPublicationException.PreserveEffectOutcome(operation, primary, publication, continuation);
        }
    }

    public static Exception? Dispose(IAgentExecutionActivityOperationLease operation, AgentEffectContinuation? continuation,
        Exception? primary) {
        RequireEffectOutcome(operation, continuation, primary);
        try {
            operation.Dispose();
            return primary;
        } catch (Exception disposal) {
            return AgentExecutionActivityPublicationException.PreserveEffectOutcome(operation, primary, disposal, continuation, disposalFailed: true);
        }
    }

    private static void RequireEffectOutcome(IAgentExecutionActivityOperationLease operation, AgentEffectContinuation? continuation,
        Exception? primary) {
        ArgumentNullException.ThrowIfNull(operation);
        if (continuation is null && !IsEffectFailure(primary)) {
            throw new ArgumentException("Effect finalization requires a typed effect result or failure.", nameof(primary));
        }
        continuation = primary switch {
            AgentExecutionActivityPublicationException { HasEffectOutcome: true } publication => publication.EffectContinuation,
            AgentEffectReconciliationIncompleteException incomplete => incomplete.Continuation,
            AgentEffectReconciliationCleanupException cleanup => cleanup.Continuation,
            _ => continuation
        };
        if (continuation?.Stop == AgentEffectReconciliationStop.SnapshotRead) {
            throw new ArgumentException("A read-only snapshot is not an execution checkpoint publication.", nameof(continuation));
        }
        if (primary is AgentExecutionActivityPublicationException { HasEffectOutcome: true } prior && prior.StreamId != operation.StreamId) {
            throw new ArgumentException("The effect failure must belong to the original activity stream.", nameof(primary));
        }
        if (continuation is not null && (continuation.ExecutionRunId != operation.ExecutionRunId || continuation.ChatSessionId != operation.ChatSessionId)) {
            throw new ArgumentException("The effect outcome must belong to the original bound activity.", nameof(continuation));
        }
    }
}

public interface IAgentExecutionActivityCoordinator
{
    AgentExecutionActivityAdmission AdmitOperation(
        AgentExecutionActivityStreamId streamId,
        Guid? agentId,
        Guid? chatSessionId,
        string acceptedMessage);
}

public interface IAgentExecutionActivityReader
{
    ISequencedStreamReader<AgentExecutionActivity> OpenReader(
        AgentExecutionActivityStreamId streamId,
        StreamSequence fromInclusive);
}

public interface IAgentExecutionActivityReporter
{
    AgentExecutionActivityStreamId StreamId { get; }

    Guid? AgentId { get; }

    Guid? ChatSessionId { get; }

    Guid? ExecutionRunId { get; }

    bool IsTerminal { get; }

    void BindAgent(Guid agentId);

    void BindContext(AgentChatContextSource source, long version);

    void BindChatSession(Guid sessionId);

    void BindExecutionRun(Guid runId, Guid? sessionId = null);

    void Report(AgentExecutionActivityPhase phase, string message);
}

public interface IAgentExecutionActivityOperationLease :
    IAgentExecutionActivityReporter,
    IDisposable
{
    void Complete(string message);

    void Fail(string message, string? errorCode = null);

    void Cancel(string message);

    void Suspend(string message);

    void SuspendForContinuation(AgentEffectContinuation continuation, string message) {
        ArgumentNullException.ThrowIfNull(continuation);
        if (continuation.Persistence != AgentEffectCheckpointPersistence.Confirmed || continuation.Stop == AgentEffectReconciliationStop.SnapshotRead ||
            continuation.ExecutionRunId != ExecutionRunId || continuation.ChatSessionId != ChatSessionId) {
            throw new ArgumentException("Continuation activity requires the original confirmed waiting checkpoint.", nameof(continuation));
        }
        Report(AgentExecutionActivityPhase.AwaitingContinuation, message);
        Suspend(message);
    }
}

public sealed class AgentExecutionActivityCoordinator(
    PartitionedSequencedStream<AgentExecutionActivityStreamId, AgentExecutionActivity> stream,
    TimeProvider timeProvider) :
    IAgentExecutionActivityCoordinator,
    IAgentExecutionActivityReader
{
    public AgentExecutionActivityAdmission AdmitOperation(
        AgentExecutionActivityStreamId streamId,
        Guid? agentId,
        Guid? chatSessionId,
        string acceptedMessage)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ValidateOptionalId(agentId, nameof(agentId));
        ValidateOptionalId(chatSessionId, nameof(chatSessionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptedMessage);

        var acceptedActivity = new AgentExecutionActivity(
            AgentExecutionActivityPhase.Accepted,
            timeProvider.GetUtcNow(),
            agentId,
            acceptedMessage,
            chatSessionId);
        var outcome = stream.Admit(streamId);
        if (outcome != StreamPartitionAdmissionOutcome.Admitted)
        {
            return new AgentExecutionActivityRejected(
                streamId,
                MapRejection(outcome));
        }

        var operation = new AgentExecutionActivityOperation(
            stream,
            timeProvider,
            streamId,
            agentId,
            chatSessionId);
        operation.PublishAccepted(acceptedActivity);
        return new AgentExecutionActivityAdmitted(streamId, operation);
    }

    public ISequencedStreamReader<AgentExecutionActivity> OpenReader(
        AgentExecutionActivityStreamId streamId,
        StreamSequence fromInclusive)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        return stream.OpenReader(streamId, fromInclusive);
    }

    private static AgentExecutionActivityAdmissionRejectionReason MapRejection(
        StreamPartitionAdmissionOutcome outcome)
    {
        return outcome switch
        {
            StreamPartitionAdmissionOutcome.AlreadyActive or
                StreamPartitionAdmissionOutcome.AlreadyTerminal =>
                AgentExecutionActivityAdmissionRejectionReason.DuplicateOperation,
            StreamPartitionAdmissionOutcome.PreviouslyEvicted =>
                AgentExecutionActivityAdmissionRejectionReason.PreviouslyEvicted,
            StreamPartitionAdmissionOutcome.CapacityExhausted =>
                AgentExecutionActivityAdmissionRejectionReason.CapacityExhausted,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
        };
    }

    private static void ValidateOptionalId(Guid? value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Optional identifier cannot be empty.", parameterName);
        }
    }
}

internal sealed class AgentExecutionActivityOperation :
    IAgentExecutionActivityOperationLease
{
    private const string DisposedCancellationMessage = "The agent operation was cancelled before it reached a terminal outcome.";

    private readonly PartitionedSequencedStream<AgentExecutionActivityStreamId, AgentExecutionActivity> stream;
    private readonly TimeProvider timeProvider;
    private readonly Lock stateLock = new();
    private Guid? agentId;
    private Guid? chatSessionId;
    private Guid? executionRunId;
    private AgentExecutionActivityContextIdentity? context;
    private bool accepted;
    private bool terminal;
    private AgentExecutionActivityPhase? currentPhase;

    internal AgentExecutionActivityOperation(
        PartitionedSequencedStream<AgentExecutionActivityStreamId, AgentExecutionActivity> stream,
        TimeProvider timeProvider,
        AgentExecutionActivityStreamId streamId,
        Guid? agentId,
        Guid? chatSessionId)
    {
        this.stream = stream;
        this.timeProvider = timeProvider;
        StreamId = streamId;
        this.agentId = agentId;
        this.chatSessionId = chatSessionId;
    }

    public AgentExecutionActivityStreamId StreamId { get; }

    public Guid? AgentId
    {
        get
        {
            lock (stateLock)
            {
                return agentId;
            }
        }
    }

    public Guid? ChatSessionId
    {
        get
        {
            lock (stateLock)
            {
                return chatSessionId;
            }
        }
    }

    public Guid? ExecutionRunId
    {
        get
        {
            lock (stateLock)
            {
                return executionRunId;
            }
        }
    }

    public bool IsTerminal
    {
        get
        {
            lock (stateLock)
            {
                return terminal;
            }
        }
    }

    public void BindAgent(Guid agentId)
    {
        ValidateId(agentId, nameof(agentId));

        lock (stateLock)
        {
            EnsureActive();
            if (this.agentId.HasValue)
            {
                if (this.agentId.Value == agentId)
                {
                    return;
                }

                throw new InvalidOperationException(
                    "The agent operation is already bound to a different agent.");
            }

            this.agentId = agentId;
        }
    }

    public void BindContext(AgentChatContextSource source, long version)
    {
        var identity = new AgentExecutionActivityContextIdentity(source, version);

        lock (stateLock)
        {
            EnsureActive();
            if (context is not null)
            {
                if (context == identity)
                {
                    return;
                }

                throw new InvalidOperationException(
                    "The agent operation is already bound to a different context.");
            }

            context = identity;
        }
    }

    public void BindExecutionRun(Guid runId, Guid? sessionId = null)
    {
        ValidateId(runId, nameof(runId));
        ValidateOptionalId(sessionId, nameof(sessionId));

        lock (stateLock)
        {
            EnsureActive();
            if (executionRunId.HasValue)
            {
                throw new InvalidOperationException("The agent operation is already bound to an execution run.");
            }

            if (chatSessionId.HasValue &&
                sessionId.HasValue &&
                chatSessionId.Value != sessionId.Value)
            {
                throw new InvalidOperationException("The execution run belongs to a different chat session.");
            }

            executionRunId = runId;
            chatSessionId ??= sessionId;
        }
    }

    public void BindChatSession(Guid sessionId)
    {
        ValidateId(sessionId, nameof(sessionId));

        lock (stateLock)
        {
            EnsureActive();
            if (chatSessionId.HasValue)
            {
                throw new InvalidOperationException("The agent operation is already bound to a chat session.");
            }

            chatSessionId = sessionId;
        }
    }

    public void Report(
        AgentExecutionActivityPhase phase,
        string message)
    {
        if (phase is AgentExecutionActivityPhase.Accepted or
            AgentExecutionActivityPhase.Completed or
            AgentExecutionActivityPhase.Failed or
            AgentExecutionActivityPhase.Cancelled)
        {
            throw new ArgumentOutOfRangeException(
                nameof(phase),
                phase,
                "Acceptance and terminal phases are owned by the operation lifecycle.");
        }

        lock (stateLock)
        {
            EnsureActive();
            AgentExecutionActivityTransitionRules.EnsureProgressTransition(
                currentPhase!.Value,
                phase);
            stream.Append(
                StreamId,
                CreateActivity(phase, message));
            currentPhase = phase;
        }
    }

    public void Complete(string message)
    {
        Terminalize(
            AgentExecutionActivityTerminalOutcome.Succeeded,
            message);
    }

    public void Fail(string message, string? errorCode = null)
    {
        Terminalize(
            AgentExecutionActivityTerminalOutcome.Failed,
            message,
            errorCode);
    }

    public void Cancel(string message)
    {
        Terminalize(
            AgentExecutionActivityTerminalOutcome.Cancelled,
            message);
    }

    public void Suspend(string message)
    {
        Terminalize(
            AgentExecutionActivityTerminalOutcome.Suspended,
            message);
    }

    public void Dispose()
    {
        TryTerminalize(
            AgentExecutionActivityTerminalOutcome.Cancelled,
            DisposedCancellationMessage);
    }

    internal void PublishAccepted(AgentExecutionActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        if (activity.Phase != AgentExecutionActivityPhase.Accepted ||
            activity.IsTerminal ||
            activity.AgentId != agentId ||
            activity.ChatSessionId != chatSessionId ||
            activity.ExecutionRunId.HasValue ||
            activity.Context is not null)
        {
            throw new ArgumentException(
                "The activity is not a valid acceptance event for this operation.",
                nameof(activity));
        }

        lock (stateLock)
        {
            if (accepted)
            {
                throw new InvalidOperationException("The agent operation has already been accepted.");
            }

            stream.Append(
                StreamId,
                activity);
            accepted = true;
            currentPhase = AgentExecutionActivityPhase.Accepted;
        }
    }

    private void Terminalize(
        AgentExecutionActivityTerminalOutcome outcome,
        string message,
        string? errorCode = null)
    {
        if (!TryTerminalize(outcome, message, errorCode))
        {
            throw new InvalidOperationException(
                "The agent operation has already reached a terminal outcome.");
        }
    }

    private bool TryTerminalize(
        AgentExecutionActivityTerminalOutcome outcome,
        string message,
        string? errorCode = null)
    {
        lock (stateLock)
        {
            if (terminal)
            {
                return false;
            }

            EnsureAccepted();
            AgentExecutionActivityTransitionRules.EnsureTerminalTransition(
                currentPhase!.Value,
                outcome);
            var phase = outcome switch
            {
                AgentExecutionActivityTerminalOutcome.Succeeded =>
                    AgentExecutionActivityPhase.Completed,
                AgentExecutionActivityTerminalOutcome.Failed =>
                    AgentExecutionActivityPhase.Failed,
                AgentExecutionActivityTerminalOutcome.Cancelled =>
                    AgentExecutionActivityPhase.Cancelled,
                AgentExecutionActivityTerminalOutcome.Suspended => currentPhase == AgentExecutionActivityPhase.AwaitingContinuation
                    ? AgentExecutionActivityPhase.AwaitingContinuation : AgentExecutionActivityPhase.AwaitingApproval,
                _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
            };
            var activity = new AgentExecutionActivity(
                phase,
                timeProvider.GetUtcNow(),
                agentId,
                message,
                chatSessionId,
                executionRunId,
                outcome,
                errorCode,
                context);

            stream.Complete(StreamId, activity);
            currentPhase = phase;
            terminal = true;
            return true;
        }
    }

    private AgentExecutionActivity CreateActivity(
        AgentExecutionActivityPhase phase,
        string message)
    {
        return new AgentExecutionActivity(
            phase,
            timeProvider.GetUtcNow(),
            agentId,
            message,
            chatSessionId,
            executionRunId,
            context: context);
    }

    private void EnsureActive()
    {
        EnsureAccepted();
        if (terminal)
        {
            throw new InvalidOperationException("The agent operation has already reached a terminal outcome.");
        }
    }

    private void EnsureAccepted()
    {
        if (!accepted)
        {
            throw new InvalidOperationException("The agent operation has not published its acceptance activity.");
        }
    }

    private static void ValidateId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }

    private static void ValidateOptionalId(Guid? value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Optional identifier cannot be empty.", parameterName);
        }
    }
}
