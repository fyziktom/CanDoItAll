namespace CanDoItAll.AgentFramework.Maf;

internal readonly record struct MafWorkflowSessionId
{
    public MafWorkflowSessionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString()
        => Value;
}

internal readonly record struct MafNativeRequestId
{
    public MafNativeRequestId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString()
        => Value;
}

internal readonly record struct MafRequestPortId
{
    public MafRequestPortId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString()
        => Value;
}

internal readonly record struct MafCheckpointId
{
    public MafCheckpointId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString()
        => Value;
}

internal readonly record struct MafHumanInputRequestFact(
    MafWorkflowSessionId SessionId,
    MafNativeRequestId NativeRequestId,
    MafRequestPortId PortId);

internal readonly record struct MafWorkflowCheckpointFact(
    MafWorkflowSessionId SessionId,
    MafCheckpointId CheckpointId,
    bool HasPendingRequest);

internal readonly record struct MafHumanInputCheckpointBoundary(
    MafHumanInputRequestFact Request,
    MafWorkflowCheckpointFact Checkpoint);

internal enum MafWorkflowStreamCompletionKind
{
    Completed = 0,
    Faulted = 1
}

internal enum MafHumanInputCheckpointCorrelationStatus
{
    NoPendingBoundary = 0,
    Correlated = 1,
    Rejected = 2
}

internal enum MafHumanInputCheckpointCorrelationFailureKind
{
    MissingRequest = 0,
    MissingCheckpoint = 1,
    SessionMismatch = 2,
    DuplicateRequest = 3,
    ConflictingRequest = 4,
    MultiplePendingRequests = 5,
    DuplicateCheckpoint = 6,
    ConflictingCheckpoint = 7,
    CheckpointNotUsable = 8,
    StreamFaulted = 9,
    BoundaryAlreadyFinalized = 10,
    ResetWhileCorrelationPending = 11
}

internal sealed record MafHumanInputCheckpointCorrelationResult
{
    private MafHumanInputCheckpointCorrelationResult(
        MafHumanInputCheckpointCorrelationStatus status,
        MafWorkflowStreamCompletionKind completionKind,
        MafHumanInputCheckpointBoundary? boundary,
        MafHumanInputCheckpointCorrelationFailureKind? failureKind)
    {
        Status = status;
        CompletionKind = completionKind;
        Boundary = boundary;
        FailureKind = failureKind;
    }

    public MafHumanInputCheckpointCorrelationStatus Status { get; }

    public MafWorkflowStreamCompletionKind CompletionKind { get; }

    public MafHumanInputCheckpointBoundary? Boundary { get; }

    public MafHumanInputCheckpointCorrelationFailureKind? FailureKind { get; }

    public static MafHumanInputCheckpointCorrelationResult NoPendingBoundary(
        MafWorkflowStreamCompletionKind completionKind)
    {
        return new(
            MafHumanInputCheckpointCorrelationStatus.NoPendingBoundary,
            completionKind,
            boundary: null,
            failureKind: null);
    }

    public static MafHumanInputCheckpointCorrelationResult Correlated(
        MafWorkflowStreamCompletionKind completionKind,
        MafHumanInputCheckpointBoundary boundary)
    {
        return new(
            MafHumanInputCheckpointCorrelationStatus.Correlated,
            completionKind,
            boundary,
            failureKind: null);
    }

    public static MafHumanInputCheckpointCorrelationResult Rejected(
        MafWorkflowStreamCompletionKind completionKind,
        MafHumanInputCheckpointCorrelationFailureKind failureKind)
    {
        return new(
            MafHumanInputCheckpointCorrelationStatus.Rejected,
            completionKind,
            boundary: null,
            failureKind);
    }
}

internal sealed class MafHumanInputCheckpointCorrelationException(
    MafHumanInputCheckpointCorrelationFailureKind failureKind) : InvalidOperationException(
        $"MAF human-input checkpoint correlation failed with '{failureKind}'.")
{
    public MafHumanInputCheckpointCorrelationFailureKind FailureKind { get; } = failureKind;
}

internal sealed class MafHumanInputCheckpointCorrelator
{
    private readonly List<MafHumanInputRequestFact> requests = [];
    private readonly List<MafWorkflowCheckpointFact> checkpoints = [];
    private bool isFinalized;

    public void ObserveRequest(MafHumanInputRequestFact request)
    {
        EnsureObservationAllowed();
        requests.Add(request);
    }

    public void ObserveCheckpoint(MafWorkflowCheckpointFact checkpoint)
    {
        EnsureObservationAllowed();
        checkpoints.Add(checkpoint);
    }

    public MafHumanInputCheckpointCorrelationResult CompleteBoundary(
        MafWorkflowStreamCompletionKind completionKind)
    {
        if (isFinalized)
        {
            return MafHumanInputCheckpointCorrelationResult.Rejected(
                completionKind,
                MafHumanInputCheckpointCorrelationFailureKind.BoundaryAlreadyFinalized);
        }

        isFinalized = true;

        var requestMultiplicityFailure = ResolveRequestMultiplicityFailure();
        if (requestMultiplicityFailure is not null)
        {
            return Reject(completionKind, requestMultiplicityFailure.Value);
        }

        var checkpointMultiplicityFailure = ResolveCheckpointMultiplicityFailure();
        if (checkpointMultiplicityFailure is not null)
        {
            return Reject(completionKind, checkpointMultiplicityFailure.Value);
        }

        if (requests.Count == 0 && checkpoints.Count == 0)
        {
            return completionKind == MafWorkflowStreamCompletionKind.Faulted
                ? Reject(completionKind, MafHumanInputCheckpointCorrelationFailureKind.StreamFaulted)
                : MafHumanInputCheckpointCorrelationResult.NoPendingBoundary(completionKind);
        }

        if (requests.Count == 0)
        {
            return Reject(completionKind, MafHumanInputCheckpointCorrelationFailureKind.MissingRequest);
        }

        if (checkpoints.Count == 0)
        {
            return Reject(completionKind, MafHumanInputCheckpointCorrelationFailureKind.MissingCheckpoint);
        }

        var request = requests[0];
        var checkpoint = checkpoints[0];
        if (!checkpoint.HasPendingRequest)
        {
            return Reject(completionKind, MafHumanInputCheckpointCorrelationFailureKind.CheckpointNotUsable);
        }

        if (request.SessionId != checkpoint.SessionId)
        {
            return Reject(completionKind, MafHumanInputCheckpointCorrelationFailureKind.SessionMismatch);
        }

        if (completionKind == MafWorkflowStreamCompletionKind.Faulted)
        {
            return Reject(completionKind, MafHumanInputCheckpointCorrelationFailureKind.StreamFaulted);
        }

        return MafHumanInputCheckpointCorrelationResult.Correlated(
            completionKind,
            new MafHumanInputCheckpointBoundary(request, checkpoint));
    }

    public void Reset()
    {
        if (!isFinalized && (requests.Count > 0 || checkpoints.Count > 0))
        {
            throw new MafHumanInputCheckpointCorrelationException(
                MafHumanInputCheckpointCorrelationFailureKind.ResetWhileCorrelationPending);
        }

        requests.Clear();
        checkpoints.Clear();
        isFinalized = false;
    }

    private MafHumanInputCheckpointCorrelationFailureKind? ResolveRequestMultiplicityFailure()
    {
        if (requests.Count < 2)
        {
            return null;
        }

        if (requests.Select(request => request.NativeRequestId).Distinct().Skip(1).Any())
        {
            return MafHumanInputCheckpointCorrelationFailureKind.MultiplePendingRequests;
        }

        return requests.All(request => request == requests[0])
            ? MafHumanInputCheckpointCorrelationFailureKind.DuplicateRequest
            : MafHumanInputCheckpointCorrelationFailureKind.ConflictingRequest;
    }

    private MafHumanInputCheckpointCorrelationFailureKind? ResolveCheckpointMultiplicityFailure()
    {
        if (checkpoints.Count < 2)
        {
            return null;
        }

        return checkpoints.All(checkpoint => checkpoint == checkpoints[0])
            ? MafHumanInputCheckpointCorrelationFailureKind.DuplicateCheckpoint
            : MafHumanInputCheckpointCorrelationFailureKind.ConflictingCheckpoint;
    }

    private void EnsureObservationAllowed()
    {
        if (isFinalized)
        {
            throw new MafHumanInputCheckpointCorrelationException(
                MafHumanInputCheckpointCorrelationFailureKind.BoundaryAlreadyFinalized);
        }
    }

    private static MafHumanInputCheckpointCorrelationResult Reject(
        MafWorkflowStreamCompletionKind completionKind,
        MafHumanInputCheckpointCorrelationFailureKind failureKind)
    {
        return MafHumanInputCheckpointCorrelationResult.Rejected(completionKind, failureKind);
    }
}
