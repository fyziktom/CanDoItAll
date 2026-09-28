namespace CanDoItAll.Modules.Workbench;

using System.Text.Json.Serialization;
using CanDoItAll.Modules.Projects;

/// <summary>
/// What a node deletion does with the managed files that belong to the deleted nodes. Written as its name, for example
/// <c>"RetainManagedFiles"</c>; request bodies also accept the name in any casing or the integer. Unspecified (0) is
/// not accepted for a deletion; RetainManagedFiles (1) keeps the files in managed storage; DeleteOwnedManagedFiles (2)
/// deletes the managed files the deleted nodes own. Files that cannot be deleted are retained and reported as warnings.
/// </summary>
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

/// <summary>
/// How far a deletion that needs follow-up got, as a JSON integer. Its only value is 1 WorkbenchCommitted: the nodes
/// are already removed from the project structure and only the cleanup of related records and files is outstanding.
/// </summary>
public enum ProjectStructureDeletionCommitState
{
    WorkbenchCommitted = 1
}

/// <summary>
/// Status of the durable cleanup that follows a node deletion, as a JSON integer. 0 Pending (recorded, not started),
/// 1 WorkbenchCommitted (the structure change is committed and the cleanup is outstanding), 2 Completed, 3 Processing
/// (an attempt is running), 4 Failed (the last attempt failed; retry it).
/// </summary>
public enum ProjectStructureDeletionReconciliationStatus
{
    Pending = 0,
    WorkbenchCommitted = 1,
    Completed = 2,
    Processing = 3,
    Failed = 4
}

/// <summary>
/// A node deletion whose structure change is committed but whose cleanup of related records and managed files is not
/// complete. Listed by <c>GET /api/project-structure/projects/{projectId}/deletion-cleanups</c> and returned in the
/// <c>details</c> of HTTP 409 <c>ProjectStructureDeletionPartialCommit</c>. Finish it by sending
/// <c>POST /api/project-structure/projects/{projectId}/nodes/{rootNodeId}/delete</c> with this
/// <c>durableMutationId</c> and the same <c>managedStorageDisposition</c>; never start a new deletion of the same root
/// instead.
/// </summary>
/// <param name="ProjectId">Identifier of the project the deletion belongs to.</param>
/// <param name="RootNodeId">Identifier of the deleted subtree's root node; the node no longer appears in reads.</param>
/// <param name="DurableMutationId">
/// Identifier of the durable cleanup, a GUID assigned by the owner. Send it as <c>durableMutationId</c> to retry the
/// cleanup.
/// </param>
/// <param name="DurableMutationStatus">
/// Cleanup status, as a JSON integer: 0 Pending, 1 WorkbenchCommitted, 2 Completed, 3 Processing, 4 Failed.
/// </param>
/// <param name="CommitState">
/// Deletion progress, as a JSON integer; always 1 WorkbenchCommitted (the nodes are already removed).
/// </param>
/// <param name="CanRetryNow">
/// True when a retry can start now; false while another attempt is running and its processing lease has not expired.
/// </param>
/// <param name="RetryAvailableAtUtc">
/// Instant (UTC, with offset) from which a retry can start while an attempt is running; null when a retry can start at
/// any time.
/// </param>
/// <param name="RetryGuidance">Human-readable instruction for retrying this cleanup.</param>
/// <param name="ManagedStorageDisposition">
/// Managed-file choice recorded for this deletion, as its name: <c>RetainManagedFiles</c> or
/// <c>DeleteOwnedManagedFiles</c>. A retry must send the same value.
/// </param>
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

/// <summary>
/// Why a deletion kept a managed file, as a JSON integer. 1 ManagedStorageRetainedByProvider (the storage provider kept
/// the object), 2 ManagedStorageRetainedWithoutOwnershipProof (the file was kept because the project's ownership of it
/// could not be proven).
/// </summary>
public enum ProjectStructureDeletionWarningKind
{
    ManagedStorageRetainedByProvider = 1,
    ManagedStorageRetainedWithoutOwnershipProof = 2
}

/// <summary>
/// A managed file that a committed node deletion did not delete, with the reason and what to do about it.
/// </summary>
/// <param name="Kind">
/// Why the file was kept, as a JSON integer: 1 ManagedStorageRetainedByProvider,
/// 2 ManagedStorageRetainedWithoutOwnershipProof.
/// </param>
/// <param name="RetainedObject">Description of the stored object that was kept.</param>
/// <param name="Message">Human-readable description of what was kept and why.</param>
/// <param name="Remediation">Human-readable suggestion for dealing with the kept file.</param>
public sealed record ProjectStructureDeletionWarning(
    ProjectStructureDeletionWarningKind Kind,
    ProjectDeletionRetainedObjectDescriptor RetainedObject,
    string Message,
    string Remediation);

/// <summary>
/// Result of a committed node deletion: how many nodes were removed and which managed files were kept.
/// </summary>
/// <param name="DeletedNodeCount">Number of nodes removed, including the descendants of each deleted node.</param>
/// <param name="DeletionWarnings">Managed files that were kept, with reasons; empty when none.</param>
public sealed record ProjectStructureDeletionResult(
    int DeletedNodeCount,
    IReadOnlyList<ProjectStructureDeletionWarning> DeletionWarnings)
{
    /// <summary>
    /// The same warnings as plain text, each message followed by its remediation; empty when none.
    /// </summary>
    public IReadOnlyList<string> Warnings
        => DeletionWarnings
            .Select(warning => $"{warning.Message} {warning.Remediation}")
            .ToArray();
}

/// <summary>
/// A completed node deletion that kept managed files, listed by <c>GET
/// /api/project-structure/projects/{projectId}/deletion-completion-notices</c>. Notices stay listed; reading them does
/// not acknowledge or remove them.
/// </summary>
/// <param name="ProjectId">Identifier of the project the deletion belongs to.</param>
/// <param name="RootNodeId">Identifier of the deleted subtree's root node.</param>
/// <param name="DurableMutationId">Identifier of the deletion's durable cleanup, a GUID assigned by the owner.</param>
/// <param name="Warnings">Managed files the deletion kept, with reasons; never empty in a listed notice.</param>
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
