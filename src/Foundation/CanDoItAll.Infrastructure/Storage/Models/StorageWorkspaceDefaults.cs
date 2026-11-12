using System.Security.Cryptography;
using System.Text.Json;

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

    public static Guid Observe(IEnumerable<StorageRoutingRuleSnapshot> rules) {
        var rows = rules.Where(rule => rule.ScopeKind == StorageRoutingScopeKind.Workspace && TrackedPurposes.Contains(rule.UsagePurpose))
            .OrderBy(rule => rule.Id)
            .Select(rule => new { rule.Id, rule.UpdatedAtUtc, rule.UsagePurpose, rule.Priority, rule.IsEnabled, rule.PreferredStorageId })
            .ToArray();
        return rows.Length == 0 ? Guid.Empty : new Guid(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rows)).AsSpan(0, 16));
    }
}
