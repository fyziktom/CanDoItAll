namespace CanDoItAll.Infrastructure.Storage;

public static class StoragePresentation
{
    private static readonly IReadOnlyList<StorageCapability> CapabilityDisplayOrder =
    [
        StorageCapability.Read,
        StorageCapability.Write,
        StorageCapability.Delete,
        StorageCapability.InlinePreview,
        StorageCapability.Download,
        StorageCapability.OpenLocally,
        StorageCapability.DirectUrl,
        StorageCapability.MutableUpdate,
        StorageCapability.BatchFolderUpload,
        StorageCapability.BatchTransfer,
        StorageCapability.ConnectionTest
    ];

    public static string DescribeProvider(StorageProviderKind providerKind)
    {
        return providerKind switch
        {
            StorageProviderKind.FileSystem => "File system",
            StorageProviderKind.Ipfs => "IPFS",
            StorageProviderKind.Ftp => "FTP",
            _ => providerKind.ToString()
        };
    }

    public static string DescribeConnectionMode(StorageConnectionMode connectionMode)
    {
        return connectionMode switch
        {
            StorageConnectionMode.Local => "Local",
            StorageConnectionMode.Remote => "Remote",
            _ => connectionMode.ToString()
        };
    }

    public static string DescribeHealth(StorageHealthStatus healthStatus)
    {
        return healthStatus switch
        {
            StorageHealthStatus.Healthy => "Healthy",
            StorageHealthStatus.Degraded => "Degraded",
            StorageHealthStatus.Unavailable => "Unavailable",
            _ => "Unknown"
        };
    }

    public static string DescribeCapability(StorageCapability capability)
    {
        return capability switch
        {
            StorageCapability.Read => "Read",
            StorageCapability.Write => "Write",
            StorageCapability.Delete => "Delete",
            StorageCapability.InlinePreview => "Inline preview",
            StorageCapability.Download => "Download",
            StorageCapability.OpenLocally => "Open locally",
            StorageCapability.DirectUrl => "Direct URL",
            StorageCapability.MutableUpdate => "Mutable update",
            StorageCapability.BatchFolderUpload => "Folder upload",
            StorageCapability.BatchTransfer => "Batch transfer",
            StorageCapability.ConnectionTest => "Connection test",
            _ => capability.ToString()
        };
    }

    public static IReadOnlyList<StorageCapability> ExpandCapabilities(StorageCapability capabilityMask)
    {
        return CapabilityDisplayOrder
            .Where(capability => capabilityMask.HasFlag(capability))
            .ToList();
    }

    public static string DescribeLocator(StorageLocatorKind locatorKind)
    {
        return locatorKind switch
        {
            StorageLocatorKind.RelativePath => "Relative path",
            StorageLocatorKind.ContentAddress => "Content address",
            StorageLocatorKind.RemotePath => "Remote path",
            StorageLocatorKind.AbsoluteUrl => "Absolute URL",
            _ => locatorKind.ToString()
        };
    }

    public static string DescribeUsagePurpose(StorageUsagePurpose usagePurpose)
    {
        return usagePurpose switch
        {
            StorageUsagePurpose.ProjectAsset => "Project assets",
            StorageUsagePurpose.PromptAttachment => "Prompt attachments",
            StorageUsagePurpose.PromptExport => "Prompt exports",
            StorageUsagePurpose.Evidence => "Evidence",
            StorageUsagePurpose.RecordingMedia => "Recording media",
            StorageUsagePurpose.DeploymentMirror => "Deployment mirrors",
            StorageUsagePurpose.SnapshotPackage => "Snapshot packages",
            StorageUsagePurpose.ReleasePackage => "Release packages",
            StorageUsagePurpose.WorkspaceExport => "Workspace exports",
            _ => "Unknown"
        };
    }
}
