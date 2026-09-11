namespace CanDoItAll.Infrastructure.Storage;

public sealed partial class StorageCatalogService {
    public async Task ApplyDefaultPurposesAsync(
        Guid storageId,
        IReadOnlyCollection<StorageUsagePurpose> defaultPurposes,
        CancellationToken cancellationToken = default) {
        var trackedPurposes = StorageWorkspaceDefaults.TrackedPurposes;
        var rules = await ListRoutingRuleRecordsAsync(cancellationToken);

        foreach (var purpose in trackedPurposes) {
            var existing = rules.FirstOrDefault(rule =>
                rule.ScopeKind == StorageRoutingScopeKind.Workspace &&
                rule.UsagePurpose == purpose);
            var selected = defaultPurposes.Contains(purpose);

            if (!selected && existing?.PreferredStorageId != storageId) {
                continue;
            }

            if (!selected) {
                if (existing is null) {
                    continue;
                }

                existing.IsEnabled = false;
                await SaveRuleCoreAsync(existing, cancellationToken);
                continue;
            }

            var previewRequired = RequiresPreviewCapability(purpose);
            var rule = existing ?? new StorageRoutingRule();
            rule.Name = $"{StoragePresentation.DescribeUsagePurpose(purpose)} default";
            rule.IsEnabled = true;
            rule.Priority = ResolvePriority(purpose);
            rule.ScopeKind = StorageRoutingScopeKind.Workspace;
            rule.ProjectId = null;
            rule.NodeKey = string.Empty;
            rule.UsagePurpose = purpose;
            rule.ContentKind = StorageContentKind.Unknown;
            rule.MimePattern = string.Empty;
            rule.EditIntent = purpose is StorageUsagePurpose.ProjectAsset or StorageUsagePurpose.PromptExport;
            rule.PreviewRequired = previewRequired;
            rule.PublishIntent = purpose is StorageUsagePurpose.ReleasePackage or StorageUsagePurpose.DeploymentMirror;
            rule.RequiredCapabilities = StorageCapability.Write |
                (previewRequired ? StorageCapability.InlinePreview : StorageCapability.None);
            rule.PreferredStorageId = storageId;
            rule.AlternativeStorageIdsJson = "[]";
            rule.Reason = BuildRoutingReason(purpose);
            await SaveRuleCoreAsync(rule, cancellationToken);
        }
    }

    private static bool RequiresPreviewCapability(StorageUsagePurpose purpose) {
        return purpose is StorageUsagePurpose.ProjectAsset or
            StorageUsagePurpose.PromptAttachment or
            StorageUsagePurpose.Evidence or
            StorageUsagePurpose.RecordingMedia;
    }

    private static int ResolvePriority(StorageUsagePurpose purpose) {
        return purpose switch {
            StorageUsagePurpose.ProjectAsset => 100,
            StorageUsagePurpose.PromptAttachment => 110,
            StorageUsagePurpose.PromptExport => 120,
            StorageUsagePurpose.Evidence => 130,
            StorageUsagePurpose.RecordingMedia => 140,
            StorageUsagePurpose.SnapshotPackage => 150,
            StorageUsagePurpose.ReleasePackage => 160,
            StorageUsagePurpose.DeploymentMirror => 170,
            _ => 500
        };
    }

    private static string BuildRoutingReason(StorageUsagePurpose purpose) {
        return purpose switch {
            StorageUsagePurpose.ProjectAsset => "Workspace default for editable project assets.",
            StorageUsagePurpose.PromptAttachment => "Workspace default for prompt attachments.",
            StorageUsagePurpose.PromptExport => "Workspace default for generated prompt exports.",
            StorageUsagePurpose.Evidence => "Workspace default for shareable evidence artifacts.",
            StorageUsagePurpose.RecordingMedia => "Workspace default for recordings and captured media.",
            StorageUsagePurpose.SnapshotPackage => "Workspace default for snapshot packages.",
            StorageUsagePurpose.ReleasePackage => "Workspace default for publish-ready release packages.",
            StorageUsagePurpose.DeploymentMirror => "Workspace default for deployment mirror targets.",
            _ => "Workspace default storage route."
        };
    }
}
