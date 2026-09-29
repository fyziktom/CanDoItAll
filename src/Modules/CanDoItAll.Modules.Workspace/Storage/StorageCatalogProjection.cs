using System.Collections.Immutable;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;

namespace CanDoItAll.Modules.Workspace;

internal static class StorageCatalogProjection {
    internal static CatalogChoices Choices { get; } = new(
        [.. Enum.GetValues<StorageProviderKind>().Select(kind => new CatalogProviderChoice((CatalogProvider)kind,
            StoragePresentation.DescribeProvider(kind), FromTemplate(WorkspaceStoragePolicy.CreateDraft(kind)),
            (CatalogCapability)WorkspaceStoragePolicy.ResolveCapabilityMask(kind, false),
            (CatalogCapability)WorkspaceStoragePolicy.ResolveCapabilityMask(kind, true)))],
        [.. Enum.GetValues<StorageConnectionMode>().Select(mode => new CatalogChoice<CatalogConnection>((CatalogConnection)mode,
            StoragePresentation.DescribeConnectionMode(mode)))],
        [.. WorkspaceStorageDefaults.TrackedPurposes.Select(purpose => new CatalogChoice<CatalogPurpose>((CatalogPurpose)purpose,
            StoragePresentation.DescribeUsagePurpose(purpose)))],
        [.. Enum.GetValues<StorageCapability>().Where(capability => capability != StorageCapability.None)
            .Select(capability => new CatalogChoice<CatalogCapability>((CatalogCapability)capability, StoragePresentation.DescribeCapability(capability)))]);

    private static CatalogEdit FromTemplate(StorageCatalogEditorModel model) => new() {
        ProviderKind = (CatalogProvider)model.ProviderKind, ConnectionMode = (CatalogConnection)model.ConnectionMode,
        IsEnabled = model.IsEnabled, UseSsl = model.UseSsl, UsePassiveMode = model.UsePassiveMode, PinOnUpload = model.PinOnUpload
    };

    internal static CatalogRow Row(StorageCatalogSnapshot value) => new(value.Id, value.Name, (CatalogProvider)value.ProviderKind,
        (CatalogConnection)value.ConnectionMode, value.EndpointOrRoot, value.DisplayOrder, value.IsEnabled, value.IsSystemDefault,
        value.IsReadOnly, Health(value.HealthStatus, value.CapabilityMask, value.LastTestedAtUtc));

    internal static CatalogHealthFact Health(StorageHealthStatus status, StorageCapability capabilities, DateTimeOffset? time) =>
        new((CatalogHealth)status, (CatalogCapability)capabilities, time, status switch {
            StorageHealthStatus.Healthy => "The driver reported a healthy connection.",
            StorageHealthStatus.Degraded => "The driver reported degraded connectivity. Review the target configuration.",
            StorageHealthStatus.Unavailable => "The driver reported an unavailable connection. Review the target configuration.",
            _ => "No confirmed connection test is available."
        });

    internal static CatalogEdit Editor(StorageCatalogEditorSnapshot editor, IReadOnlyList<StorageRoutingRuleSnapshot> rules) {
        var row = editor.Catalog;
        var config = editor.Configuration;
        return new() {
            Id = row.Id, Name = row.Name, ProviderKind = (CatalogProvider)row.ProviderKind, ConnectionMode = (CatalogConnection)row.ConnectionMode,
            EndpointOrRoot = row.EndpointOrRoot, CredentialSecretId = row.CredentialSecretId, IsEnabled = row.IsEnabled,
            IsSystemDefault = row.IsSystemDefault, IsReadOnly = row.IsReadOnly, DisplayOrder = row.DisplayOrder,
            GatewayBaseUrl = config.GatewayBaseUrl, Port = config.Port, PinOnUpload = config.PinOnUpload,
            Username = config.Username, BasePath = config.BasePath, UseSsl = config.UseSsl, UsePassiveMode = config.UsePassiveMode,
            Health = Health(row.HealthStatus, row.CapabilityMask, row.LastTestedAtUtc),
            DefaultPurposes = [.. rules.Where(rule => rule.IsEnabled && rule.ScopeKind == StorageRoutingScopeKind.Workspace &&
                rule.PreferredStorageId == row.Id && WorkspaceStorageDefaults.TrackedPurposes.Contains(rule.UsagePurpose))
                .Select(rule => (CatalogPurpose)rule.UsagePurpose).Distinct()]
        };
    }

    internal static StorageCatalogSaveRequest Request(CatalogEdit draft, bool test) => new() {
        Id = draft.Id ?? Guid.NewGuid(), Name = string.IsNullOrWhiteSpace(draft.Name) && test ? $"Storage {draft.ProviderKind}" : draft.Name.Trim(),
        ProviderKind = (StorageProviderKind)draft.ProviderKind,
        ConnectionMode = WorkspaceStoragePolicy.ResolveConnectionMode((StorageProviderKind)draft.ProviderKind, (StorageConnectionMode)draft.ConnectionMode),
        EndpointOrRoot = draft.EndpointOrRoot.Trim(), CredentialSecretId = draft.CredentialSecretId,
        IsEnabled = draft.IsEnabled, IsSystemDefault = draft.IsSystemDefault, IsReadOnly = draft.IsReadOnly, DisplayOrder = draft.DisplayOrder,
        CapabilityMask = WorkspaceStoragePolicy.ResolveCapabilityMask((StorageProviderKind)draft.ProviderKind, draft.IsReadOnly),
        HealthStatus = (StorageHealthStatus)draft.Health.Status, LastTestedAtUtc = draft.Health.TestedAtUtc,
        LastHealthMessage = draft.Health.Message,
        Configuration = new() {
            GatewayBaseUrl = draft.GatewayBaseUrl.Trim(), Port = draft.Port, PinOnUpload = draft.PinOnUpload,
            Username = draft.Username.Trim(), BasePath = draft.BasePath.Trim(), UseSsl = draft.UseSsl, UsePassiveMode = draft.UsePassiveMode
        }
    };
}
