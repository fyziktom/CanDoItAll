namespace CanDoItAll.Modules.Projects.Pages.Components;

public sealed record ProjectPackageTargetOption(Guid Id, string DisplayName, string Descriptor);

public sealed record ProjectCleanupTarget(Guid ProjectId, ProjectDeletionParticipantId ParticipantId, Guid RecoveryId);

public sealed record ProjectCleanupStatus(
    ProjectCleanupTarget Target, string Status, bool CanRetryNow, DateTimeOffset? RetryAvailableAtUtc, string RetryGuidance);

public sealed record ProjectCleanupNotice(ProjectCleanupTarget Target, string Operation, IReadOnlyList<string> Warnings);

public enum ProjectCleanupView { Pending, RetainedMedia }

public sealed record ProjectCleanupReview(
    ProjectCleanupView View, int Page = 0, ProjectCleanupTarget? Target = null, int MediaPage = 0);
