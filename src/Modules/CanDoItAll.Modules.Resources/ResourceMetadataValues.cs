using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Resources;

internal sealed record PreparedResourceMetadata(ResourceMetadataStoredValues Values, SearchDocumentInput Search);
internal sealed class ResourceMetadataRefusedException(ResourceMetadataRejectionReason reason, string message)
    : InvalidOperationException(message) {
    public ResourceMetadataRejectionReason Reason { get; } = reason;
}

public sealed class ResourceMetadataPreparation(ResourceConnectorPluginRegistry plugins) {
    private readonly ImmutableDictionary<string, string> installed = plugins.ListManifests()
        .ToImmutableDictionary(manifest => manifest.PluginKey, manifest => manifest.ConfigurationSchema.Version, StringComparer.OrdinalIgnoreCase);
    private readonly ImmutableDictionary<ResourceKind, string> legacy = Enum.GetValues<ResourceKind>()
        .Select(kind => (Kind: kind, Plugin: plugins.TryResolve(new ProjectResource { ResourceKind = kind }, out var plugin) ? plugin : null))
        .Where(item => item.Plugin is not null)
        .ToImmutableDictionary(item => item.Kind, item => item.Plugin!.Manifest.PluginKey);

    internal PreparedResourceMetadata Prepare(ReplaceResourceMetadataCommand command) {
        var editor = command.Values.ToEditorModel();
        editor.ProjectId = command.TargetProject.ProjectId;
        editor.ExpectedProjectAdmission = command.TargetProject;
        if (string.IsNullOrWhiteSpace(editor.Name)) {
            throw Refuse("Resource name is required.");
        }
        var key = editor.ConnectorPluginKey.Trim();
        if (IsGoverned(key)) {
            throw new ResourceMetadataRefusedException(ResourceMetadataRejectionReason.GovernedResource,
                "Storage-object resources require an authorized Resources browse promotion.");
        }
        if (!installed.ContainsKey(key)) {
            throw new ResourceMetadataRefusedException(ResourceMetadataRejectionReason.UnsupportedMetadata,
                "The selected Resource connector is unavailable.");
        }
        var plugin = plugins.Resolve(key);
        var schema = string.IsNullOrWhiteSpace(editor.ConfigSchemaVersion)
            ? plugin.Manifest.ConfigurationSchema.Version : editor.ConfigSchemaVersion.Trim();
        if (!string.Equals(schema, plugin.Manifest.ConfigurationSchema.Version, StringComparison.Ordinal)) {
            throw Refuse("The selected Resource connector configuration version is unsupported.");
        }
        RequireConfiguration(editor.ConfigJson);
        if (string.Equals(key, WebhookResourceConnectorPlugin.PluginKey, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(editor.Configuration.GetText(ResourceConnectorFieldKeys.EndpointUrl)) &&
            !string.Equals(editor.ConfigJson.Trim(), "{}", StringComparison.Ordinal)) {
            plugin.ApplyConfig(editor, editor.ConfigJson);
        }
        if (plugin.Manifest.SecretRequirements.Any(requirement => requirement.IsRequired) && editor.LinkedSecretId is null) {
            throw Refuse("The selected Resource connector requires a linked secret reference.");
        }
        if (plugin.ValidateEditor(editor) is { } invalid) {
            throw Refuse(invalid.Message);
        }
        var location = plugin.BuildLocation(editor);
        var config = plugin.SerializeConfig(editor);
        var values = new ResourceMetadataStoredValues(plugin.LegacyResourceKind, editor.OwnerPartyId,
            editor.MaintainerPartyId, editor.Name.Trim(), editor.Description.Trim(), plugin.Manifest.PluginKey, schema,
            location, config, editor.LinkedSecretId is { } secret ? JsonSerializer.Serialize(new[] { secret }) : "[]",
            editor.ValidationStatus, editor.Sensitivity, editor.SupportsPreview, editor.SupportsIndexing);
        RequireNormalized(values);
        return new(values, Search(command.Expected.ResourceId, command.TargetProject.ProjectId, values, plugin.Manifest.DisplayName));
    }

    internal PreparedResourceMetadata Prepare(StorageObjectResourceWriteRequest request) {
        var config = StorageObjectResourceConnectorPlugin.Serialize(request.Config);
        var values = new ResourceMetadataStoredValues(null, null, null, request.Name.Trim(), string.Empty,
            StorageObjectResourceConnectorPlugin.PluginKey, StorageObjectResourceConnectorPlugin.SchemaVersion,
            StorageObjectResourceConnectorPlugin.BuildStableLocation(request.Config), config, "[]",
            ResourceValidationStatus.Valid, request.Sensitivity, true, false);
        RequireNormalized(values);
        return new(values, Search(request.ResourceId, request.ProjectId, values, "Storage object"));
    }

    public ResourceEditorModel CreateEditor(ResourceMetadataSnapshot snapshot) {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (Inspect(snapshot.Values) is { } reason && reason != ResourceMetadataReadOnlyReason.UnavailableConnector &&
            reason != ResourceMetadataReadOnlyReason.GovernedStorageObject) {
            throw new ResourceActionRefusedException("The retained Resource configuration cannot be projected into this editor.");
        }
        var values = snapshot.Values;
        var binding = snapshot.Expected.Project;
        var editor = new ResourceEditorModel {
            Id = snapshot.Expected.ResourceId,
            ProjectId = ProjectId(binding),
            ExpectedProjectAdmission = binding is ResourceProjectBinding.Bound bound
                ? new(snapshot.Expected.Context.DatabaseProfileId, bound.ProjectId, bound.LifetimeId) : null,
            OwnerPartyId = values.OwnerPartyId,
            MaintainerPartyId = values.MaintainerPartyId,
            Name = values.Name,
            Description = values.Description,
            ConnectorPluginKey = values.ConnectorPluginKey,
            ConfigSchemaVersion = values.ConfigSchemaVersion,
            LocationOrIdentifier = values.LocationOrIdentifier,
            ConfigJson = values.ConfigJson,
            Configuration = ConnectorConfigState.FromJson(values.ConfigJson),
            LinkedSecretId = ReadSecret(values.LinkedSecretIdsJson),
            ValidationStatus = values.ValidationStatus,
            Sensitivity = values.Sensitivity,
            SupportsPreview = values.SupportsPreview,
            SupportsIndexing = values.SupportsIndexing
        };
        if (InstalledKey(values) is { } key) {
            var plugin = plugins.Resolve(key);
            editor.ConnectorPluginKey = key;
            plugin.ApplyConfig(editor, values.ConfigJson);
            _ = ResourceMetadataValues.Capture(editor);
        }
        return editor;
    }

    internal ResourceMetadataReadOnlyReason? Inspect(ResourceMetadataStoredValues values) {
        if (values.OwnerPartyId == Guid.Empty || values.MaintainerPartyId == Guid.Empty ||
            !Enum.IsDefined(values.ValidationStatus) || !Enum.IsDefined(values.Sensitivity) ||
            values.LegacyResourceKind is { } kind && !Enum.IsDefined(kind)) {
            return ResourceMetadataReadOnlyReason.InvalidMetadata;
        }
        try {
            _ = ReadSecret(values.LinkedSecretIdsJson);
        } catch (Exception exception) when (exception is JsonException or ArgumentException) {
            return ResourceMetadataReadOnlyReason.UnsupportedSecretReferences;
        }
        try {
            RequireConfiguration(values.ConfigJson);
        } catch (Exception exception) when (exception is JsonException or ArgumentException or ResourceMetadataRefusedException) {
            return ResourceMetadataReadOnlyReason.InvalidConfiguration;
        }
        if (IsGoverned(values.ConnectorPluginKey)) {
            return ResourceMetadataReadOnlyReason.GovernedStorageObject;
        }
        var installedKey = InstalledKey(values);
        if (installedKey is null) {
            return ResourceMetadataReadOnlyReason.UnavailableConnector;
        }
        return !string.IsNullOrWhiteSpace(values.ConfigSchemaVersion) &&
            !string.Equals(values.ConfigSchemaVersion, installed[installedKey], StringComparison.Ordinal)
            ? ResourceMetadataReadOnlyReason.InvalidConfiguration : null;
    }

    internal static Guid? ProjectId(ResourceProjectBinding binding) => binding switch {
        ResourceProjectBinding.Bound bound => bound.ProjectId,
        ResourceProjectBinding.RetainedProject retained => retained.ProjectId,
        ResourceProjectBinding.RetainedUnbound => null,
        _ => throw new ArgumentOutOfRangeException(nameof(binding))
    };

    internal static bool IsGoverned(string key)
        => string.Equals(key.Trim(), StorageObjectResourceConnectorPlugin.PluginKey, StringComparison.OrdinalIgnoreCase);

    private string? InstalledKey(ResourceMetadataStoredValues values) {
        if (!string.IsNullOrWhiteSpace(values.ConnectorPluginKey)) {
            return installed.ContainsKey(values.ConnectorPluginKey.Trim()) ? values.ConnectorPluginKey.Trim() : null;
        }
        return values.LegacyResourceKind is { } kind && legacy.TryGetValue(kind, out var key) && installed.ContainsKey(key) ? key : null;
    }

    internal static Guid? ReadSecret(string json) {
        ResourceMetadataLimits.RequireText(json, ResourceMetadataLimits.LinkedSecretIdsJsonBytes, nameof(json));
        using var document = JsonDocument.Parse(json, new() { MaxDepth = 4 });
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() > 1) {
            throw new ArgumentException("The retained Resource secret references are unsupported.");
        }
        if (document.RootElement.GetArrayLength() == 0) {
            return null;
        }
        var value = document.RootElement[0];
        if (value.ValueKind != JsonValueKind.String || !value.TryGetGuid(out var id) || id == Guid.Empty) {
            throw new ArgumentException("The retained Resource secret reference is invalid.");
        }
        return id;
    }

    private static void RequireConfiguration(string json) {
        ResourceMetadataLimits.RequireText(json, ResourceMetadataLimits.ConfigJsonBytes, nameof(json));
        using var document = JsonDocument.Parse(json, new() { MaxDepth = 64 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) {
            throw Refuse("Resource configuration must be a JSON object.");
        }
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject()) {
            if (keys.Count == ResourceMetadataLimits.ConfigurationEntries || !keys.Add(property.Name)) {
                throw Refuse("Resource configuration has too many or ambiguous entries.");
            }
            ResourceMetadataLimits.RequireText(property.Name, ResourceMetadataLimits.ConfigurationKeyBytes, "configuration key");
            var value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()! : property.Value.GetRawText();
            ResourceMetadataLimits.RequireText(value, ResourceMetadataLimits.ConfigurationValueBytes, "configuration value");
        }
    }

    private static void RequireNormalized(ResourceMetadataStoredValues values) {
        if (string.IsNullOrWhiteSpace(values.Name) || values.Name.Length > 200 || values.ConnectorPluginKey.Length > 160 ||
            values.ConfigSchemaVersion.Length > 40 || values.LocationOrIdentifier.Length > 1000 ||
            !Enum.IsDefined(values.ValidationStatus) || !Enum.IsDefined(values.Sensitivity)) {
            throw Refuse("The normalized Resource metadata exceeds its supported field limits.");
        }
        ResourceMetadataLimits.RequireText(values.Description, ResourceMetadataLimits.DescriptionBytes, "description");
        RequireConfiguration(values.ConfigJson);
        _ = ReadSecret(values.LinkedSecretIdsJson);
    }

    private static SearchDocumentInput Search(Guid id, Guid projectId, ResourceMetadataStoredValues values, string connectorName) {
        ResourceMetadataLimits.RequireText(connectorName, ResourceMetadataLimits.ScalarBytes, "connector display name");
        var input = new SearchDocumentInput(SearchDocument.ResourceSourceType, id.ToString(), "Resources", values.Name, values.Description,
            $"{values.LocationOrIdentifier}\nConnector: {connectorName}\nSensitivity: {values.Sensitivity}\nValidation: {values.ValidationStatus}",
            $"/resources?resourceId={id}", projectId);
        var bytes = new[] { input.SourceType, input.SourceKey, input.Category, input.Title, input.Summary, input.Body, input.Route }
            .Sum(value => (long)Encoding.UTF8.GetByteCount(value));
        if (bytes > ResourceMetadataLimits.SearchBytes) {
            throw Refuse("The Resource search projection exceeds its supported byte limit.");
        }
        return input;
    }

    private static ResourceMetadataRefusedException Refuse(string message)
        => new(ResourceMetadataRejectionReason.InvalidValues, message);
}
