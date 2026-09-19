using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Projects;

public static class ProjectMutationScopeKeys
{
    public const string Hierarchy = "projects:hierarchy";

    public static string ForProject(Guid projectId)
        => $"project:{projectId:D}";
}

public sealed record ProjectDeletionParticipantPreparation(
    Guid ProjectId,
    Guid RecoveryId);

public readonly record struct ProjectDeletionPreparationScopeKey
{
    public ProjectDeletionPreparationScopeKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString()
        => Value;
}

/// <summary>
/// Identifier of a project-deletion participant, the module part that cleans up its own data after a project is
/// deleted. It is serialized as an object with one member, for example <c>{ "value": "workbench" }</c>; send the
/// <c>value</c> text as <c>participantId</c> in the cleanup retry route.
/// </summary>
public readonly record struct ProjectDeletionParticipantId
{
    public ProjectDeletionParticipantId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    /// <summary>
    /// Text identifier of the participant, currently <c>workbench</c> (Project Structure nodes and managed media) or
    /// <c>agent-project-structure-access</c> (agents' access to the project). Never null in responses.
    /// </summary>
    public string Value { get; }

    public override string ToString()
        => Value;
}

public interface IProjectDeletionParticipant
{
    ProjectDeletionParticipantId Id { get; }

    IReadOnlyCollection<ProjectDeletionPreparationScopeKey> PreparationScopeKeys { get; }

    Task<ProjectDeletionParticipantPreparation?> PrepareAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ProjectDeletionParticipantCompletion> CompleteAsync(
        ProjectDeletionParticipantPreparation preparation,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectDeletionParticipantRecovery>> ListPendingRecoveriesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectDeletionParticipantCompletionNotice>> ListCompletionNoticesAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Kind of object that a project deletion kept, as a JSON integer: 1 ManagedStorageRetainedByProvider (managed media
/// kept by an immutable storage provider such as IPFS; no cleanup retry is needed), 2
/// ManagedStorageRetainedWithoutOwnershipProof (legacy managed media kept because the project's ownership of it could
/// not be proven; migrate or remove it manually after checking ownership).
/// </summary>
public enum ProjectDeletionWarningKind
{
    ManagedStorageRetainedByProvider = 1,
    ManagedStorageRetainedWithoutOwnershipProof = 2
}

public sealed record ProjectDeletionParticipantWarning(
    ProjectDeletionWarningKind Kind,
    ProjectDeletionRetainedObjectDescriptor RetainedObject,
    string Message,
    string Remediation);

/// <summary>
/// A stored object that a project deletion kept, with the reason.
/// </summary>
/// <param name="Provider">
/// Kind of storage provider that holds the object, as a JSON integer: 0 FileSystem, 1 Ipfs, 2 Ftp.
/// </param>
/// <param name="StorageId">Identifier of the configured storage that holds the object; null when not recorded.</param>
/// <param name="LocatorKind">
/// How <c>locator</c> is to be read, as a JSON integer: 0 RelativePath, 1 ContentAddress, 2 RemotePath, 3 AbsoluteUrl.
/// </param>
/// <param name="Locator">
/// Provider-specific reference to the object, for example a path relative to the storage root or an IPFS content
/// address.
/// </param>
/// <param name="Reason">Explanation of why the object was kept, for display.</param>
public sealed record ProjectDeletionRetainedObjectDescriptor(
    StorageProviderKind Provider,
    Guid? StorageId,
    StorageLocatorKind LocatorKind,
    string Locator,
    string Reason);

public sealed record ProjectDeletionParticipantCompletion(
    Guid RecoveryId,
    IReadOnlyList<ProjectDeletionParticipantWarning> Warnings)
{
    public static ProjectDeletionParticipantCompletion Empty(Guid recoveryId)
        => new(recoveryId, []);
}

public sealed class ProjectDeletionParticipantCleanupException : Exception
{
    public ProjectDeletionParticipantCleanupException(
        Guid recoveryId,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        RecoveryId = recoveryId;
    }

    public Guid RecoveryId { get; }
}

/// <summary>
/// State of an unfinished participant cleanup of a deleted project, as a JSON integer: 1 Pending (recorded, not
/// processed yet), 2 Processing (an attempt is running or still holds the cleanup), 3 Failed (the last attempt failed),
/// 4 Finalizing (the participant reports its work as done).
/// </summary>
public enum ProjectDeletionRecoveryStatus
{
    Pending = 1,
    Processing = 2,
    Failed = 3,
    Finalizing = 4
}

public sealed record ProjectDeletionParticipantRecovery(
    Guid ProjectId,
    Guid RecoveryId,
    ProjectDeletionRecoveryStatus Status,
    bool CanRetryNow,
    DateTimeOffset? RetryAvailableAtUtc,
    string RetryGuidance);

/// <summary>
/// Kind of finished cleanup, as a JSON integer: 1 ProjectDeletion (cleanup after a project was deleted),
/// 2 ProjectNodeCleanup (cleanup after nodes of a project were deleted in Project Structure).
/// </summary>
public enum ProjectDeletionCompletionOperation
{
    ProjectDeletion = 1,
    ProjectNodeCleanup = 2
}

public sealed record ProjectDeletionParticipantCompletionNotice(
    Guid ProjectId,
    Guid RecoveryId,
    ProjectDeletionCompletionOperation Operation,
    IReadOnlyList<ProjectDeletionParticipantWarning> Warnings);

/// <summary>
/// An object that a deletion participant kept while cleaning up after a project deletion, with guidance for operators.
/// </summary>
/// <param name="Kind">
/// Kind of kept object, as a JSON integer: 1 ManagedStorageRetainedByProvider, 2
/// ManagedStorageRetainedWithoutOwnershipProof.
/// </param>
/// <param name="ParticipantId">Participant that kept the object.</param>
/// <param name="RecoveryId">Recovery identifier of the participant cleanup that reported the object.</param>
/// <param name="RetainedObject">The kept storage object.</param>
/// <param name="Message">English explanation, for display; its wording can change.</param>
/// <param name="Remediation">What an operator can do about the object, in English, for display.</param>
public sealed record ProjectDeletionWarning(
    ProjectDeletionWarningKind Kind,
    ProjectDeletionParticipantId ParticipantId,
    Guid RecoveryId,
    ProjectDeletionRetainedObjectDescriptor RetainedObject,
    string Message,
    string Remediation);

/// <summary>
/// Result of a project deletion, or of a cleanup retry, in which every participant cleanup finished.
/// </summary>
/// <param name="ProjectId">Identifier of the deleted project.</param>
/// <param name="Warnings">Objects that were kept, with remediation; an empty array when nothing was kept.</param>
public sealed record ProjectDeletionResult(
    Guid ProjectId,
    IReadOnlyList<ProjectDeletionWarning> Warnings);

/// <summary>
/// A participant cleanup of a deleted project that has not finished yet.
/// </summary>
/// <param name="ProjectId">Identifier of the deleted project; use it in the retry route.</param>
/// <param name="ParticipantId">Participant that owns the cleanup; send its <c>value</c> in the retry route.</param>
/// <param name="RecoveryId">Recovery identifier of the cleanup; use it in the retry route.</param>
/// <param name="Status">
/// State of the cleanup, as a JSON integer: 1 Pending, 2 Processing, 3 Failed, 4 Finalizing.
/// </param>
/// <param name="CanRetryNow">False while another attempt is running and holds the cleanup; true otherwise.</param>
/// <param name="RetryAvailableAtUtc">
/// When the hold of a processing attempt on the cleanup ends, in UTC; it can lie in the past. Null when no attempt
/// holds the cleanup.
/// </param>
/// <param name="RetryGuidance">English guidance of the participant, for display.</param>
public sealed record ProjectDeletionPendingCleanup(
    Guid ProjectId,
    ProjectDeletionParticipantId ParticipantId,
    Guid RecoveryId,
    ProjectDeletionRecoveryStatus Status,
    bool CanRetryNow,
    DateTimeOffset? RetryAvailableAtUtc,
    string RetryGuidance);

/// <summary>
/// A finished participant cleanup of a project and the objects it kept.
/// </summary>
/// <param name="ProjectId">Identifier of the project the cleanup was for.</param>
/// <param name="ParticipantId">Participant that performed the cleanup.</param>
/// <param name="RecoveryId">Recovery identifier of the cleanup.</param>
/// <param name="Operation">
/// Kind of cleanup, as a JSON integer: 1 ProjectDeletion, 2 ProjectNodeCleanup.
/// </param>
/// <param name="Warnings">
/// Objects that the cleanup kept, with remediation; an empty array when nothing was kept.
/// </param>
public sealed record ProjectDeletionCompletionNotice(
    Guid ProjectId,
    ProjectDeletionParticipantId ParticipantId,
    Guid RecoveryId,
    ProjectDeletionCompletionOperation Operation,
    IReadOnlyList<ProjectDeletionWarning> Warnings);

/// <summary>
/// Kind of project-deletion step that did not finish, as a JSON integer: 1 ParticipantCleanup (a deletion participant
/// did not finish its cleanup).
/// </summary>
public enum ProjectDeletionRecoveryOperation
{
    ParticipantCleanup = 1
}

/// <summary>
/// One participant cleanup of a deleted project that did not finish.
/// </summary>
/// <param name="Operation">Kind of unfinished step, as a JSON integer: 1 ParticipantCleanup.</param>
/// <param name="ParticipantId">
/// Participant whose cleanup did not finish; send its <c>value</c> as <c>participantId</c> in the retry route.
/// </param>
/// <param name="RecoveryId">Recovery identifier to send in the retry route.</param>
public sealed record ProjectDeletionRecoveryFailure(
    ProjectDeletionRecoveryOperation Operation,
    ProjectDeletionParticipantId ParticipantId,
    Guid? RecoveryId);

/// <summary>
/// What remains to be done after a project deletion whose participant cleanups did not all finish. The project itself
/// is deleted.
/// </summary>
/// <param name="ProjectId">Identifier of the deleted project.</param>
/// <param name="Failures">The participant cleanups that did not finish; retry each of them.</param>
/// <param name="RetryGuidance">
/// English guidance: retry each exact participant and recovery identifier and do not start a new project deletion.
/// </param>
public sealed record ProjectDeletionRecovery(
    Guid ProjectId,
    IReadOnlyList<ProjectDeletionRecoveryFailure> Failures,
    string RetryGuidance);

public sealed class ProjectDeletionPartialCommitException : Exception
{
    public ProjectDeletionPartialCommitException(
        ProjectDeletionRecovery recovery,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Recovery = recovery;
    }

    public ProjectDeletionRecovery Recovery { get; }
}

public sealed class ProjectDeletionRecoveryNotFoundException : Exception
{
    public ProjectDeletionRecoveryNotFoundException(
        Guid projectId,
        ProjectDeletionParticipantId participantId,
        Guid recoveryId)
        : base(
            $"Pending project cleanup '{recoveryId:D}' for participant '{participantId}' and project '{projectId:D}' was not found.")
    {
        ProjectId = projectId;
        ParticipantId = participantId;
        RecoveryId = recoveryId;
    }

    public Guid ProjectId { get; }

    public ProjectDeletionParticipantId ParticipantId { get; }

    public Guid RecoveryId { get; }
}
