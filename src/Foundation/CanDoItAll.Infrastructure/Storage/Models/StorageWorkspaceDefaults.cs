namespace CanDoItAll.Infrastructure.Storage;

public static class StorageWorkspaceDefaults {
    public static IReadOnlyList<StorageUsagePurpose> TrackedPurposes { get; } =
    [
        StorageUsagePurpose.ProjectAsset,
        StorageUsagePurpose.PromptAttachment,
        StorageUsagePurpose.PromptExport,
        StorageUsagePurpose.Evidence,
        StorageUsagePurpose.RecordingMedia,
        StorageUsagePurpose.SnapshotPackage,
        StorageUsagePurpose.ReleasePackage,
        StorageUsagePurpose.DeploymentMirror
    ];

}
