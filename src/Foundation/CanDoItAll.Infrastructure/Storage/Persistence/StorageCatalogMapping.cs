using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Infrastructure.Storage;

internal static class StorageCatalogMapping {
    internal static StorageCatalogSnapshot ToSnapshot(this StorageCatalogRecord source) => new() {
        Id = source.Id,
        Name = source.Name,
        ProviderKind = source.ProviderKind,
        IsEnabled = source.IsEnabled,
        IsSystemDefault = source.IsSystemDefault,
        IsReadOnly = source.IsReadOnly,
        DisplayOrder = source.DisplayOrder,
        ConnectionMode = source.ConnectionMode,
        EndpointOrRoot = source.EndpointOrRoot,
        RootBindingFormatVersion = source.RootBindingFormatVersion,
        RootPlatformFamily = source.RootPlatformFamily,
        RootPathSyntax = source.RootPathSyntax,
        RootHostBindingId = source.RootHostBindingId,
        RootPathState = source.RootPathState,
        RootLastValidatedAtUtc = source.RootLastValidatedAtUtc,
        CapabilityMask = source.CapabilityMask,
        HealthStatus = source.HealthStatus,
        LastTestedAtUtc = source.LastTestedAtUtc,
        LastHealthMessage = source.LastHealthMessage,
        CredentialSecretId = source.CredentialSecretId,
        CreatedAtUtc = source.CreatedAtUtc,
        UpdatedAtUtc = source.UpdatedAtUtc,
        SourceFingerprint = SourceFingerprint(source)
    };

    internal static StorageRoutingRuleSnapshot ToSnapshot(this StorageRoutingRule source) => new() {
        Id = source.Id,
        Name = source.Name,
        IsEnabled = source.IsEnabled,
        Priority = source.Priority,
        ScopeKind = source.ScopeKind,
        ProjectId = source.ProjectId,
        NodeKey = source.NodeKey,
        UsagePurpose = source.UsagePurpose,
        ContentKind = source.ContentKind,
        MimePattern = source.MimePattern,
        MinimumContentLength = source.MinimumContentLength,
        MaximumContentLength = source.MaximumContentLength,
        EditIntent = source.EditIntent,
        PreviewRequired = source.PreviewRequired,
        PublishIntent = source.PublishIntent,
        RequiredCapabilities = source.RequiredCapabilities,
        PreferredStorageId = source.PreferredStorageId,
        Reason = source.Reason,
        CreatedAtUtc = source.CreatedAtUtc,
        UpdatedAtUtc = source.UpdatedAtUtc
    };

    internal static StorageCatalogRecord CreateDraft(StorageCatalogSaveRequest source) => new() {
        Id = source.Id,
        Name = source.Name,
        ProviderKind = source.ProviderKind,
        IsEnabled = source.IsEnabled,
        IsSystemDefault = source.IsSystemDefault,
        IsReadOnly = source.IsReadOnly,
        DisplayOrder = source.DisplayOrder,
        ConnectionMode = source.ConnectionMode,
        EndpointOrRoot = source.EndpointOrRoot,
        CapabilityMask = source.CapabilityMask,
        HealthStatus = source.HealthStatus,
        LastTestedAtUtc = source.LastTestedAtUtc,
        LastHealthMessage = source.LastHealthMessage,
        CredentialSecretId = source.CredentialSecretId,
        ConfigJson = source.Configuration is null ? "{}" : StorageJson.SerializeProviderConfiguration(source.Configuration)
    };

    internal static StorageRoutingRule CreateDraft(StorageRoutingRuleSaveRequest source) => new() {
        Id = source.Id,
        Name = source.Name,
        IsEnabled = source.IsEnabled,
        Priority = source.Priority,
        ScopeKind = source.ScopeKind,
        ProjectId = source.ProjectId,
        NodeKey = source.NodeKey,
        UsagePurpose = source.UsagePurpose,
        ContentKind = source.ContentKind,
        MimePattern = source.MimePattern,
        MinimumContentLength = source.MinimumContentLength,
        MaximumContentLength = source.MaximumContentLength,
        EditIntent = source.EditIntent,
        PreviewRequired = source.PreviewRequired,
        PublishIntent = source.PublishIntent,
        RequiredCapabilities = source.RequiredCapabilities,
        PreferredStorageId = source.PreferredStorageId,
        Reason = source.Reason,
        AlternativeStorageIdsJson = source.AlternativeStorageIds is null ? "[]" : StorageJson.SerializeGuidList(source.AlternativeStorageIds)
    };

    internal static StorageDriverInput ToDriverInput(this StorageCatalogRecord source) => new(source.ToSnapshot(), source.ConfigJson);

    internal static StorageCatalogRecord ToRecord(this StorageDriverInput source) => new() {
        Id = source.Id,
        Name = source.Name,
        ProviderKind = source.ProviderKind,
        IsEnabled = source.IsEnabled,
        IsSystemDefault = source.IsSystemDefault,
        IsReadOnly = source.IsReadOnly,
        DisplayOrder = source.DisplayOrder,
        ConnectionMode = source.ConnectionMode,
        EndpointOrRoot = source.EndpointOrRoot,
        RootBindingFormatVersion = source.RootBindingFormatVersion,
        RootPlatformFamily = source.RootPlatformFamily,
        RootPathSyntax = source.RootPathSyntax,
        RootHostBindingId = source.RootHostBindingId,
        RootPathState = source.RootPathState,
        RootLastValidatedAtUtc = source.RootLastValidatedAtUtc,
        CapabilityMask = source.CapabilityMask,
        HealthStatus = source.HealthStatus,
        LastTestedAtUtc = source.LastTestedAtUtc,
        LastHealthMessage = source.LastHealthMessage,
        CredentialSecretId = source.CredentialSecretId,
        CreatedAtUtc = source.CreatedAtUtc,
        UpdatedAtUtc = source.UpdatedAtUtc,
        ConfigJson = source.OriginalConfigurationJson
    };

    private static string SourceFingerprint(StorageCatalogRecord source) {
        string canonical = string.Join('|', source.Id.ToString("N"), source.ProviderKind, source.IsEnabled,
            source.IsReadOnly, (int)source.CapabilityMask, source.ConnectionMode, source.EndpointOrRoot,
            source.ConfigJson, source.CredentialSecretId?.ToString("N") ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
