using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.ControlPlane;

internal static class LegacyDatabaseProfileCatalogQuarantine
{
    private const string ProfilesPropertyName = "profiles";
    private const string ProviderKindPropertyName = "providerKind";
    private const string SourceKindPropertyName = "sourceKind";
    private const string IdPropertyName = "id";
    public const string RetiredProviderName = "Sqlite";
    public const string RetiredConnectionPropertyName = "sqlite";
    public const string RetiredManagedSourceName = "ManagedSqlite";
    public const string RetiredExternalFileSourceName = "ExternalSqliteFile";
    public const string RetiredImportedSourceName = "ImportedSqlite";
    public const string RetiredSnapshotCacheSourceName = "SnapshotCache";
    public const string RetiredIpfsSnapshotSourceName = "IpfsSnapshot";

    private static readonly IReadOnlySet<string> RetiredSourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        RetiredManagedSourceName,
        RetiredExternalFileSourceName,
        RetiredImportedSourceName,
        RetiredSnapshotCacheSourceName,
        RetiredIpfsSnapshotSourceName
    };
    private static readonly IReadOnlySet<int> RetiredSourceValues = new HashSet<int>
    {
        0,
        1,
        2,
        4,
        5
    };
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static LegacyDatabaseProfileCatalogQuarantineResult QuarantineIfNeeded(
        string controlPlaneRoot,
        string catalogPath,
        string activeProfileStatePath,
        DurableFileWriter durableFileWriter,
        ILogger? logger)
    {
        if (!File.Exists(catalogPath))
        {
            return LegacyDatabaseProfileCatalogQuarantineResult.None;
        }

        var json = File.ReadAllText(catalogPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return LegacyDatabaseProfileCatalogQuarantineResult.None;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(document.RootElement, ProfilesPropertyName, out var profilesElement) ||
            profilesElement.ValueKind != JsonValueKind.Array)
        {
            return LegacyDatabaseProfileCatalogQuarantineResult.None;
        }

        var retainedProfiles = new List<JsonElement>();
        var quarantinedProfileIds = new HashSet<Guid>();
        var quarantinedProfileCount = 0;

        foreach (var profile in profilesElement.EnumerateArray())
        {
            if (IsRetiredProfile(profile))
            {
                quarantinedProfileCount++;
                if (TryReadProfileId(profile, out var profileId))
                {
                    quarantinedProfileIds.Add(profileId);
                }

                continue;
            }

            retainedProfiles.Add(profile.Clone());
        }

        if (quarantinedProfileCount == 0)
        {
            return LegacyDatabaseProfileCatalogQuarantineResult.None;
        }

        var catalogDirectory = Path.GetDirectoryName(catalogPath)
            ?? throw new InvalidOperationException($"Unable to resolve a directory for '{catalogPath}'.");
        var quarantineDirectory = Path.Combine(catalogDirectory, "quarantine");
        Directory.CreateDirectory(quarantineDirectory);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var quarantinePath = Path.Combine(quarantineDirectory, $"legacy-database-profiles-{timestamp}.json");
        durableFileWriter.WriteText(
            controlPlaneRoot,
            quarantinePath,
            json,
            DurableFileWriteOptions.Private);
        WriteOperatorNote(
            controlPlaneRoot,
            quarantineDirectory,
            timestamp,
            quarantinePath,
            quarantinedProfileCount,
            durableFileWriter);
        var activeProfileReset = ResetActiveProfileIfNeeded(
            controlPlaneRoot,
            activeProfileStatePath,
            quarantinedProfileIds,
            durableFileWriter);
        WriteSanitizedCatalog(
            controlPlaneRoot,
            document.RootElement,
            retainedProfiles,
            catalogPath,
            durableFileWriter);
        logger?.LogWarning(
            "Quarantined {ProfileCount} retired {ProviderName} database profile entries before control-plane catalog deserialization. BackupPath={BackupPath}. ActiveProfileReset={ActiveProfileReset}. Create or select a PostgreSQL profile before migrating data manually.",
            quarantinedProfileCount,
            RetiredProviderName,
            quarantinePath,
            activeProfileReset);

        return new LegacyDatabaseProfileCatalogQuarantineResult(
            WasQuarantined: true,
            QuarantinedProfileCount: quarantinedProfileCount,
            RetainedProfileCount: retainedProfiles.Count,
            QuarantinePath: quarantinePath,
            ActiveProfileReset: activeProfileReset);
    }

    private static bool IsRetiredProfile(JsonElement profile)
    {
        return HasRetiredProvider(profile) ||
            HasRetiredSource(profile) ||
            HasRetiredConnectionMetadata(profile);
    }

    private static bool HasRetiredProvider(JsonElement profile)
    {
        if (!TryGetProperty(profile, ProviderKindPropertyName, out var providerKind))
        {
            return false;
        }

        return providerKind.ValueKind switch
        {
            JsonValueKind.String => string.Equals(providerKind.GetString(), RetiredProviderName, StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => providerKind.TryGetInt32(out var providerValue) && providerValue == 0,
            _ => false
        };
    }

    private static bool HasRetiredSource(JsonElement profile)
    {
        if (!TryGetProperty(profile, SourceKindPropertyName, out var sourceKind))
        {
            return false;
        }

        return sourceKind.ValueKind switch
        {
            JsonValueKind.String => RetiredSourceNames.Contains(sourceKind.GetString() ?? string.Empty),
            JsonValueKind.Number => sourceKind.TryGetInt32(out var sourceValue) && RetiredSourceValues.Contains(sourceValue),
            _ => false
        };
    }

    private static bool HasRetiredConnectionMetadata(JsonElement profile)
    {
        foreach (var property in profile.EnumerateObject())
        {
            if (string.Equals(property.Name, RetiredConnectionPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
            }
        }

        return false;
    }

    private static bool TryReadProfileId(JsonElement profile, out Guid profileId)
    {
        profileId = Guid.Empty;
        return TryGetProperty(profile, IdPropertyName, out var idElement) &&
            idElement.ValueKind == JsonValueKind.String &&
            Guid.TryParse(idElement.GetString(), out profileId);
    }

    private static void WriteSanitizedCatalog(
        string controlPlaneRoot,
        JsonElement root,
        IReadOnlyList<JsonElement> retainedProfiles,
        string catalogPath,
        DurableFileWriter durableFileWriter)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true
        }))
        {
            writer.WriteStartObject();
            foreach (var property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, ProfilesPropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                property.WriteTo(writer);
            }

            writer.WritePropertyName(ProfilesPropertyName);
            writer.WriteStartArray();
            foreach (var profile in retainedProfiles)
            {
                profile.WriteTo(writer);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        durableFileWriter.WriteBytes(
            controlPlaneRoot,
            catalogPath,
            stream.ToArray(),
            DurableFileWriteOptions.Private);
    }

    private static bool ResetActiveProfileIfNeeded(
        string controlPlaneRoot,
        string activeProfileStatePath,
        IReadOnlySet<Guid> quarantinedProfileIds,
        DurableFileWriter durableFileWriter)
    {
        if (quarantinedProfileIds.Count == 0 || !File.Exists(activeProfileStatePath))
        {
            return false;
        }

        var activeState = ReadDocument(activeProfileStatePath, static () => new DatabaseActiveProfileState());
        if (!activeState.ActiveProfileId.HasValue || !quarantinedProfileIds.Contains(activeState.ActiveProfileId.Value))
        {
            return false;
        }

        activeState.ActiveProfileId = null;
        durableFileWriter.WriteBytes(
            controlPlaneRoot,
            activeProfileStatePath,
            JsonSerializer.SerializeToUtf8Bytes(activeState, SerializerOptions),
            DurableFileWriteOptions.Private);
        return true;
    }

    private static void WriteOperatorNote(
        string controlPlaneRoot,
        string quarantineDirectory,
        string timestamp,
        string quarantinePath,
        int quarantinedProfileCount,
        DurableFileWriter durableFileWriter)
    {
        var notePath = Path.Combine(quarantineDirectory, $"legacy-database-profiles-{timestamp}.md");
        var content =
            $"""
            # Legacy Database Profile Quarantine

            Quarantined profile count: {quarantinedProfileCount}

            Original catalog backup: {quarantinePath}

            The quarantined entries used retired {RetiredProviderName} profile metadata. The main runtime now requires PostgreSQL profiles. Create or select a PostgreSQL profile, then migrate data manually from the backed-up catalog/database files when needed.
            """;
        durableFileWriter.WriteText(
            controlPlaneRoot,
            notePath,
            content,
            DurableFileWriteOptions.Private);
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static T ReadDocument<T>(string path, Func<T> createDefault)
    {
        if (!File.Exists(path))
        {
            return createDefault();
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return createDefault();
        }

        return JsonSerializer.Deserialize<T>(json, SerializerOptions) ?? createDefault();
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

internal sealed record LegacyDatabaseProfileCatalogQuarantineResult(
    bool WasQuarantined,
    int QuarantinedProfileCount,
    int RetainedProfileCount,
    string? QuarantinePath,
    bool ActiveProfileReset)
{
    public static LegacyDatabaseProfileCatalogQuarantineResult None { get; } = new(
        WasQuarantined: false,
        QuarantinedProfileCount: 0,
        RetainedProfileCount: 0,
        QuarantinePath: null,
        ActiveProfileReset: false);
}
