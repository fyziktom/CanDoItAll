using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Resources;

internal sealed record StorageObjectResourceConfig(
    string SourceKey,
    Guid StorageId,
    StorageProviderKind ProviderKind,
    StorageLocatorKind LocatorKind,
    string Locator,
    string DisplayName,
    string ContentType,
    long? ContentLength);

public sealed class StorageObjectResourceConnectorPlugin : IResourceConnectorPlugin
{
    public const string PluginKey = ResourceConnectorPluginKeys.StorageObject;
    public const string SchemaVersion = "1.0";

    internal const int MaximumLocatorLength = 4096;
    internal const int MaximumDisplayNameLength = 512;
    internal const int MaximumContentTypeLength = 256;

    internal static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static readonly ConnectorPluginManifest PluginManifest = new(
        PluginKey,
        "Storage object",
        "1.0.0",
        ConnectorManifestCapability.ProjectResource | ConnectorManifestCapability.WorkbenchProjection,
        new ConnectorConfigurationSchema(SchemaVersion, []),
        [],
        new ConnectorHealthCheckDescriptor(
            "authorized reopen",
            "Re-resolves the current source and file-access authority before opening the stored object."),
        new ConnectorAgentExposure(
            "resource.read.storage-object",
            false,
            true,
            "Storage objects are not exposed to agents by this connector."),
        new ConnectorWorkbenchNodeHook(ProjectObjectType.File, "storage-object", "Governed storage object"));

    public ResourceKind? LegacyResourceKind => null;

    public ConnectorPluginManifest Manifest => PluginManifest;

    public Error? ValidateEditor(ResourceEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return TryDeserialize(model.ConfigJson, out _, out string? error)
            ? null
            : Error.Validation(error ?? "The storage-object resource configuration is invalid.");
    }

    public string BuildLocation(ResourceEditorModel model)
    {
        StorageObjectResourceConfig config = Deserialize(model.ConfigJson);
        return BuildStableLocation(config);
    }

    public string SerializeConfig(ResourceEditorModel model)
    {
        StorageObjectResourceConfig config = Deserialize(model.ConfigJson);
        return Serialize(config);
    }

    public void ApplyConfig(ResourceEditorModel model, string configJson)
    {
        ArgumentNullException.ThrowIfNull(model);
        StorageObjectResourceConfig config = Deserialize(configJson);
        model.ConfigJson = Serialize(config);
        model.LocationOrIdentifier = BuildStableLocation(config);
    }

    public ProjectObjectType ResolveWorkbenchObjectType(ProjectResource resource) => ProjectObjectType.File;

    public string ResolveWorkbenchObjectSubtype(ProjectResource resource) => "storage-object";

    internal static StorageObjectResourceConfig Deserialize(string configJson)
    {
        if (TryDeserialize(configJson, out StorageObjectResourceConfig? config, out string? error))
        {
            return config!;
        }

        throw new InvalidOperationException(error ?? "The storage-object resource configuration is invalid.");
    }

    internal static string Serialize(StorageObjectResourceConfig config)
    {
        Validate(config);
        return JsonSerializer.Serialize(config, JsonOptions);
    }

    internal static string BuildStableLocation(StorageObjectResourceConfig config)
    {
        Validate(config);
        string locatorHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(config.Locator))).ToLowerInvariant()[..12];
        return $"storage-object:{config.StorageId:N}:{config.ProviderKind}:{config.LocatorKind}:{locatorHash}";
    }

    internal static FileToolsKnownFileOccurrenceKind ToOccurrenceKind(StorageLocatorKind locatorKind)
        => locatorKind switch
        {
            StorageLocatorKind.RelativePath => FileToolsKnownFileOccurrenceKind.RelativePath,
            StorageLocatorKind.ContentAddress => FileToolsKnownFileOccurrenceKind.ContentAddress,
            StorageLocatorKind.RemotePath => FileToolsKnownFileOccurrenceKind.RemotePath,
            _ => throw new InvalidOperationException("The storage-object locator kind is unsupported.")
        };

    private static bool TryDeserialize(
        string? configJson,
        out StorageObjectResourceConfig? config,
        out string? error)
    {
        config = null;
        error = null;
        if (string.IsNullOrWhiteSpace(configJson))
        {
            error = "The storage-object resource configuration is required.";
            return false;
        }

        try
        {
            config = JsonSerializer.Deserialize<StorageObjectResourceConfig>(configJson, JsonOptions);
            Validate(config);
            return true;
        }
        catch (JsonException)
        {
            error = "The storage-object resource configuration is malformed.";
            return false;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static void Validate(StorageObjectResourceConfig? config)
    {
        if (config is null)
        {
            throw new ArgumentException("The storage-object resource configuration is required.");
        }

        if (!ResourceFileSourceKey.TryParse(config.SourceKey, out ResourceFileSourceKey sourceKey) ||
            config.StorageId == Guid.Empty)
        {
            throw new ArgumentException("The storage-object source identity is invalid.");
        }

        if (sourceKey.TryGetStorageId(out Guid sourceStorageId) && sourceStorageId != config.StorageId)
        {
            throw new ArgumentException("The storage-object storage source does not match its object identity.");
        }

        if (!Enum.IsDefined(config.ProviderKind) || !Enum.IsDefined(config.LocatorKind))
        {
            throw new ArgumentException("The storage-object provider or locator kind is invalid.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(config.Locator);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.ContentType);
        if (config.Locator.Trim().Length > MaximumLocatorLength ||
            config.DisplayName.Trim().Length > MaximumDisplayNameLength ||
            config.ContentType.Trim().Length > MaximumContentTypeLength ||
            config.ContentLength < 0)
        {
            throw new ArgumentException("The storage-object configuration exceeds its supported limits.");
        }

        bool supported = (config.ProviderKind, config.LocatorKind) switch
        {
            (StorageProviderKind.FileSystem, StorageLocatorKind.RelativePath) => true,
            (StorageProviderKind.Ipfs, StorageLocatorKind.ContentAddress) => true,
            (StorageProviderKind.Ipfs, StorageLocatorKind.RemotePath) => true,
            (StorageProviderKind.Ftp, StorageLocatorKind.RemotePath) => true,
            _ => false
        };
        if (!supported)
        {
            throw new ArgumentException("The storage-object provider and locator kind are incompatible.");
        }

        _ = new FileToolsKnownFileOccurrence(
            config.StorageId,
            ToOccurrenceKind(config.LocatorKind),
            config.Locator,
            config.DisplayName,
            config.ContentType,
            config.ContentLength);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
