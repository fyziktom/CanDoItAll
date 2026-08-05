using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectStructureTransferRejectionReason
{
    SourceProjectRequired,
    TargetProjectRequired,
    TargetProjectMustDiffer,
    SelectedNodesRequired,
    DescendantsUnavailable,
    SelectedNodesUnavailable,
    TargetProjectMismatch
}

public sealed class ProjectStructureTransferRejectedException : Exception
{
    internal ProjectStructureTransferRejectedException(
        ProjectStructureTransferRejectionReason reason,
        string message,
        Guid sourceProjectId,
        Guid targetProjectId,
        Guid? actualTargetProjectId = null)
        : base(message)
    {
        Reason = reason;
        SourceProjectId = sourceProjectId;
        TargetProjectId = targetProjectId;
        ActualTargetProjectId = actualTargetProjectId;
    }

    public ProjectStructureTransferRejectionReason Reason { get; }

    public Guid SourceProjectId { get; }

    public Guid TargetProjectId { get; }

    public Guid? ActualTargetProjectId { get; }
}

public sealed class ProjectStructureProjectCreationRejectedException : Exception
{
    public ProjectStructureProjectCreationRejectedException(
        string message,
        IReadOnlyList<Error>? errors = null)
        : base(message)
    {
        Errors = errors ?? [];
    }

    public IReadOnlyList<Error> Errors { get; }
}

public sealed class ProjectStructureCompensatedSubprojectTransferException : Exception
{
    public ProjectStructureCompensatedSubprojectTransferException(
        Guid removedProjectId,
        Exception transferFailure)
        : base(
            "The subproject transfer failed after child creation; the empty child was removed.",
            transferFailure)
    {
        RemovedProjectId = removedProjectId;
        TransferFailure = transferFailure;
    }

    public Guid RemovedProjectId { get; }

    public Exception TransferFailure { get; }
}

public sealed class ProjectStructureTransferPartialCommitException : Exception
{
    public ProjectStructureTransferPartialCommitException(
        ProjectStructureTransferRecovery recovery,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Recovery = recovery;
    }

    public ProjectStructureTransferRecovery Recovery { get; }
}

internal static class ProjectStructureProjectCreationResult
{
    public static void ThrowIfRejected(Result result, string fallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccess)
        {
            return;
        }

        var message = string.Join(" ", result.Errors.Select(error => error.Message));
        throw new ProjectStructureProjectCreationRejectedException(
            string.IsNullOrWhiteSpace(message) ? fallbackMessage : message,
            result.Errors);
    }
}

internal static class ProjectStructureExceptionGraph
{
    private const int MaxExceptionsToInspect = 32;

    public static bool TryFind<TException>(
        Exception exception,
        Predicate<TException> predicate,
        out TException matchedException)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(predicate);

        var pending = new Queue<Exception>();
        var inspected = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        pending.Enqueue(exception);

        while (pending.Count > 0 && inspected.Count < MaxExceptionsToInspect)
        {
            var current = pending.Dequeue();
            if (!inspected.Add(current))
            {
                continue;
            }

            if (current is TException candidate && predicate(candidate))
            {
                matchedException = candidate;
                return true;
            }

            if (current is AggregateException aggregateException)
            {
                foreach (var innerException in aggregateException.InnerExceptions)
                {
                    pending.Enqueue(innerException);
                }
            }
            else if (current.InnerException is not null)
            {
                pending.Enqueue(current.InnerException);
            }
        }

        matchedException = null!;
        return false;
    }
}
