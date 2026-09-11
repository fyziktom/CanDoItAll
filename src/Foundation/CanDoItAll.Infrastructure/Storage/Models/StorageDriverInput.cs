using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Infrastructure.Storage;

public sealed record StorageDriverInput : StorageCatalogSnapshot {
    private readonly string configurationJson;

    internal StorageDriverInput(StorageCatalogSnapshot catalog, string configurationJson) : base(catalog) {
        this.configurationJson = configurationJson;
    }

    internal string OriginalConfigurationJson => configurationJson;

    public static StorageDriverInput FromDraft(StorageCatalogSaveRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        return StorageCatalogMapping.CreateDraft(request).ToDriverInput();
    }

    public StorageProviderConfiguration ReadConfiguration() => StorageJson.ParseProviderConfiguration(configurationJson);

    public string BuildBrowseFingerprint(string root) {
        string endpoint = EndpointOrRoot ?? string.Empty;
        string configuration = configurationJson ?? "{}";
        if (endpoint.Length > StorageBrowseContainer.MaximumKeyLength ||
            configuration.Length > StorageJson.MaximumProviderConfigurationJsonLength) {
            throw new StorageBrowseException(new StorageBrowseError(StorageBrowseErrorCode.InvalidConfiguration,
                "The storage source configuration exceeds the bounded fingerprint contract."));
        }
        string canonical = string.Join('\n', Id.ToString("N"), ((int)ProviderKind).ToString(CultureInfo.InvariantCulture),
            endpoint, configuration, ((int)CapabilityMask).ToString(CultureInfo.InvariantCulture),
            IsEnabled ? "1" : "0", IsReadOnly ? "1" : "0", CredentialSecretId?.ToString("N") ?? "none", root);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
