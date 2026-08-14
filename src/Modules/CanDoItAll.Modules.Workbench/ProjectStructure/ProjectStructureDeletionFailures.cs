namespace CanDoItAll.Modules.Workbench;

using System.Text.Json.Serialization;
using CanDoItAll.Modules.Projects;

[JsonConverter(typeof(JsonStringEnumConverter<ProjectStructureManagedStorageDisposition>))]
public enum ProjectStructureManagedStorageDisposition
{
    Unspecified = 0,
    RetainManagedFiles = 1,
    DeleteOwnedManagedFiles = 2
}

internal static class ProjectStructureManagedStorageDispositionPolicy
{
    public static void EnsureSpecified(ProjectStructureManagedStorageDisposition disposition)
    {
        if (disposition is ProjectStructureManagedStorageDisposition.RetainManagedFiles or
            ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(
            nameof(disposition),
            disposition,
            "A managed-storage disposition is required for project-structure deletion.");
    }

    public static ProjectStructureManagedStorageDisposition ResolvePersisted(
        ProjectStructureManagedStorageDisposition disposition)
    {
        var resolved = disposition == ProjectStructureManagedStorageDisposition.Unspecified
            ? ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles
            : disposition;
        EnsureSpecified(resolved);
        return resolved;
    }
}

public enum ProjectStructureDeletionCommitState
{
    WorkbenchCommitted = 1
}

public enum ProjectStructureDeletionReconciliationStatus
{
    Pending = 0,
    WorkbenchCommitted = 1,
    Completed = 2,
    Processing = 3,
    Failed = 4
}

public sealed record ProjectStructureDeletionRecovery(
    Guid ProjectId,
    string RootNodeId,
    Guid DurableMutationId,
    ProjectStructureDeletionReconciliationStatus DurableMutationStatus,
    ProjectStructureDeletionCommitState CommitState,
    bool CanRetryNow,
    DateTimeOffset? RetryAvailableAtUtc,
    string RetryGuidance,
    ProjectStructureManagedStorageDisposition ManagedStorageDisposition);

public enum ProjectStructureDeletionWarningKind
{
    ManagedStorageRetainedByProvider = 1,
    ManagedStorageRetainedWithoutOwnershipProof = 2
}

public sealed record ProjectStructureDeletionWarning(
    ProjectStructureDeletionWarningKind Kind,
    ProjectDeletionRetainedObjectDescriptor RetainedObject,
    string Message,
    string Remediation);

public sealed record ProjectStructureDeletionResult(
    int DeletedNodeCount,
    IReadOnlyList<ProjectStructureDeletionWarning> DeletionWarnings)
{
    public IReadOnlyList<string> Warnings
        => DeletionWarnings
            .Select(warning => $"{warning.Message} {warning.Remediation}")
            .ToArray();
}

public sealed record ProjectStructureDeletionCompletionNotice(
    Guid ProjectId,
    string RootNodeId,
    Guid DurableMutationId,
    IReadOnlyList<ProjectStructureDeletionWarning> Warnings);

public sealed class ProjectStructureDeletionPartialCommitException : Exception
{
    public ProjectStructureDeletionPartialCommitException(
        ProjectStructureDeletionRecovery recovery,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Recovery = recovery;
    }

    public ProjectStructureDeletionRecovery Recovery { get; }
}

public enum ProjectStructureDeletionBatchRejectionReason
{
    SelectedNodesRequired = 1,
    SelectedNodesNotFound = 2
}

public sealed class ProjectStructureDeletionBatchRejectedException : Exception
{
    public ProjectStructureDeletionBatchRejectedException(
        ProjectStructureDeletionBatchRejectionReason reason,
        string message,
        IReadOnlyList<string> requestedNodeIds)
        : base(message)
    {
        Reason = reason;
        RequestedNodeIds = requestedNodeIds;
    }

    public ProjectStructureDeletionBatchRejectionReason Reason { get; }

    public IReadOnlyList<string> RequestedNodeIds { get; }
}

public sealed record ProjectStructureDeletionBatchRecovery(
    Guid ProjectId,
    IReadOnlyList<ProjectStructureDeletionRecovery> Recoveries,
    int CompletedNodeCount,
    IReadOnlyList<ProjectStructureDeletionWarning> Warnings)
{
    public IReadOnlyList<ProjectStructureDeletionBranchFailure> BranchFailures { get; init; } = [];
}

public enum ProjectStructureDeletionBranchFailureKind
{
    ManagedStorageValidation = 1,
    DispositionMismatch = 2,
    OperationFailed = 3
}

public sealed record ProjectStructureDeletionBranchFailure(
    string RootNodeId,
    ProjectStructureDeletionBranchFailureKind Kind,
    ProjectStructureManagedStorageDisposition RequestedDisposition,
    Guid? BindingId,
    string Message,
    string Remediation)
{
    public ProjectStructureManagedStorageDisposition? SuggestedRetryDisposition { get; init; }

    public int CompletedNodeCount { get; init; }
}

public sealed class ProjectStructureDeletionBatchPartialCommitException : Exception
{
    public ProjectStructureDeletionBatchPartialCommitException(
        ProjectStructureDeletionBatchRecovery recovery,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Recovery = recovery;
    }

    public ProjectStructureDeletionBatchRecovery Recovery { get; }
}

public sealed class ProjectStructureDeletionRecoveryNotFoundException : Exception
{
    public ProjectStructureDeletionRecoveryNotFoundException(
        Guid projectId,
        string rootNodeId,
        Guid durableMutationId)
        : base(
            $"Durable subtree-deletion cleanup '{durableMutationId:D}' was not found for project '{projectId:D}' and root '{rootNodeId}'.")
    {
        ProjectId = projectId;
        RootNodeId = rootNodeId;
        DurableMutationId = durableMutationId;
    }

    public Guid ProjectId { get; }

    public string RootNodeId { get; }

    public Guid DurableMutationId { get; }
}

public sealed class ProjectStructureDeletionDispositionMismatchException : Exception
{
    public ProjectStructureDeletionDispositionMismatchException(
        Guid projectId,
        string rootNodeId,
        Guid durableMutationId,
        ProjectStructureManagedStorageDisposition requestedDisposition,
        ProjectStructureManagedStorageDisposition persistedDisposition,
        int completedNodeCount = 0)
        : base(
            $"Durable subtree-deletion cleanup '{durableMutationId:D}' uses managed-storage disposition '{persistedDisposition}', not '{requestedDisposition}'.")
    {
        ProjectId = projectId;
        RootNodeId = rootNodeId;
        DurableMutationId = durableMutationId;
        RequestedDisposition = requestedDisposition;
        PersistedDisposition = persistedDisposition;
        CompletedNodeCount = completedNodeCount;
    }

    public Guid ProjectId { get; }

    public string RootNodeId { get; }

    public Guid DurableMutationId { get; }

    public ProjectStructureManagedStorageDisposition RequestedDisposition { get; }

    public ProjectStructureManagedStorageDisposition PersistedDisposition { get; }

    public int CompletedNodeCount { get; }
}
