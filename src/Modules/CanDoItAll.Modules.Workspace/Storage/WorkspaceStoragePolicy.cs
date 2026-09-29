using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Workspace;

internal static class WorkspaceStoragePolicy {
    internal static StorageCatalogEditorModel CreateDraft(StorageProviderKind providerKind) {
        return new StorageCatalogEditorModel {
            ProviderKind = providerKind,
            ConnectionMode = providerKind == StorageProviderKind.FileSystem ? StorageConnectionMode.Local : StorageConnectionMode.Remote,
            IsEnabled = true,
            UseSsl = providerKind == StorageProviderKind.Ftp,
            UsePassiveMode = true,
            PinOnUpload = providerKind == StorageProviderKind.Ipfs,
            CapabilityMask = ResolveCapabilityMask(providerKind, isReadOnly: false)
        };
    }

    internal static StorageConnectionMode ResolveConnectionMode(StorageProviderKind providerKind, StorageConnectionMode requestedMode) {
        return providerKind == StorageProviderKind.FileSystem
            ? StorageConnectionMode.Local
            : requestedMode == StorageConnectionMode.Local
                ? StorageConnectionMode.Remote
                : requestedMode;
    }

    internal static StorageCapability ResolveCapabilityMask(StorageProviderKind providerKind, bool isReadOnly) {
        var capabilityMask = providerKind switch {
            StorageProviderKind.FileSystem => StorageCapability.Read |
                StorageCapability.Write |
                StorageCapability.Delete |
                StorageCapability.Download |
                StorageCapability.InlinePreview |
                StorageCapability.OpenLocally |
                StorageCapability.MutableUpdate |
                StorageCapability.BatchFolderUpload |
                StorageCapability.BatchTransfer |
                StorageCapability.ConnectionTest,
            StorageProviderKind.Ipfs => StorageCapability.Read |
                StorageCapability.Write |
                StorageCapability.Download |
                StorageCapability.InlinePreview |
                StorageCapability.DirectUrl |
                StorageCapability.BatchFolderUpload |
                StorageCapability.BatchTransfer |
                StorageCapability.ConnectionTest,
            StorageProviderKind.Ftp => StorageCapability.Read |
                StorageCapability.Write |
                StorageCapability.Delete |
                StorageCapability.Download |
                StorageCapability.BatchFolderUpload |
                StorageCapability.BatchTransfer |
                StorageCapability.ConnectionTest,
            _ => StorageCapability.None
        };

        if (!isReadOnly) {
            return capabilityMask;
        }

        return capabilityMask &
            ~StorageCapability.Write &
            ~StorageCapability.Delete &
            ~StorageCapability.MutableUpdate &
            ~StorageCapability.BatchFolderUpload;
    }

}
