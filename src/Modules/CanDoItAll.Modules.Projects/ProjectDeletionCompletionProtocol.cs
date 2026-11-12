namespace CanDoItAll.Modules.Projects;

internal static class ProjectDeletionCompletionProtocol {
    internal const int SchemaVersion = 1;
}

internal enum ProjectDeletionCompletionOrigin {
    OrdinaryDeletion = 1,
    CreationCompensation = 2,
    LegacyInstallation = 3,
    ReservedCancellation = 4
}

internal enum ProjectDeletionCompletionReleaseState {
    AwaitingOwnerRelease = 1,
    Released = 2
}
