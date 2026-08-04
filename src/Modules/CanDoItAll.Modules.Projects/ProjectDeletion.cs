using CanDoItAll.Infrastructure.Persistence;
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

public readonly record struct ProjectDeletionParticipantId
{
    public ProjectDeletionParticipantId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString()
        => Value;
}

public interface IProjectDeletionParticipant
{
    ProjectDeletionParticipantId Id { get; }

    IReadOnlyCollection<ProjectDeletionPreparationScopeKey> PreparationScopeKeys { get; }

    Task<ProjectDeletionParticipantPreparation?> PrepareAsync(
        AppDbContext dbContext,
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

public sealed record ProjectDeletionWarning(
    ProjectDeletionWarningKind Kind,
    ProjectDeletionParticipantId ParticipantId,
    Guid RecoveryId,
    ProjectDeletionRetainedObjectDescriptor RetainedObject,
    string Message,
    string Remediation);

public sealed record ProjectDeletionResult(
    Guid ProjectId,
    IReadOnlyList<ProjectDeletionWarning> Warnings);

public sealed record ProjectDeletionPendingCleanup(
    Guid ProjectId,
    ProjectDeletionParticipantId ParticipantId,
    Guid RecoveryId,
    ProjectDeletionRecoveryStatus Status,
    bool CanRetryNow,
    DateTimeOffset? RetryAvailableAtUtc,
    string RetryGuidance);

public sealed record ProjectDeletionCompletionNotice(
    Guid ProjectId,
    ProjectDeletionParticipantId ParticipantId,
    Guid RecoveryId,
    ProjectDeletionCompletionOperation Operation,
    IReadOnlyList<ProjectDeletionWarning> Warnings);

public enum ProjectDeletionRecoveryOperation
{
    ParticipantCleanup = 1
}

public sealed record ProjectDeletionRecoveryFailure(
    ProjectDeletionRecoveryOperation Operation,
    ProjectDeletionParticipantId ParticipantId,
    Guid? RecoveryId);

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
