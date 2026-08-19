using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Infrastructure.Storage;

public static class StorageJson
{
    public const int MaximumProviderConfigurationJsonLength = 64 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = BuildSerializerOptions();

    public static string SerializeReference(StorageObjectReference? reference)
    {
        return reference is null
            ? string.Empty
            : JsonSerializer.Serialize(NormalizeReference(reference), SerializerOptions);
    }

    public static StorageObjectReference? ParseReference(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        StorageObjectReference? reference = JsonSerializer.Deserialize<StorageObjectReference>(json, SerializerOptions);
        return reference is null ? null : NormalizeReference(reference);
    }

    public static bool TryParseReference(string? json, out StorageObjectReference? reference)
    {
        reference = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            reference = ParseReference(json);
            return reference is not null;
        }
        catch
        {
            reference = null;
            return false;
        }
    }

    public static string SerializeProviderConfiguration(StorageProviderConfiguration? configuration)
    {
        StorageProviderConfiguration value = configuration ?? new StorageProviderConfiguration();
        value.Validate();
        string json = JsonSerializer.Serialize(value, SerializerOptions);
        ValidateProviderConfigurationLength(json);
        return json;
    }

    public static StorageProviderConfiguration ParseProviderConfiguration(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new StorageProviderConfiguration();
        }

        ValidateProviderConfigurationLength(json);

        StorageProviderConfiguration configuration =
            JsonSerializer.Deserialize<StorageProviderConfiguration>(json, SerializerOptions)
            ?? new StorageProviderConfiguration();
        configuration.Validate();
        return configuration;
    }

    public static IReadOnlyList<Guid> ParseGuidList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<Guid>>(json, SerializerOptions) ?? [];
    }

    public static string SerializeGuidList(IEnumerable<Guid> values)
    {
        return JsonSerializer.Serialize(values.Distinct().ToArray(), SerializerOptions);
    }

    public static string EncodeReferenceToken(StorageObjectReference reference)
    {
        var json = SerializeReference(reference);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecodeReferenceToken(string? token, out StorageObjectReference? reference)
    {
        reference = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var normalized = token
                .Replace('-', '+')
                .Replace('_', '/');
            var padding = normalized.Length % 4;
            if (padding > 0)
            {
                normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
            }

            var bytes = Convert.FromBase64String(normalized);
            var json = Encoding.UTF8.GetString(bytes);
            return TryParseReference(json, out reference);
        }
        catch
        {
            return false;
        }
    }

    public static string BuildPreviewUrl(StorageObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var token = EncodeReferenceToken(reference);
        return $"/storage/objects/preview?ref={Uri.EscapeDataString(token)}";
    }

    public static string BuildDownloadUrl(StorageObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var token = EncodeReferenceToken(reference);
        return $"/storage/objects/download?ref={Uri.EscapeDataString(token)}";
    }

    public static StorageObjectReference CreateLegacyManagedFileReference(
        string relativePath,
        string contentType,
        string originalFileName,
        long? contentLength = null)
    {
        string normalizedRelativePath = NormalizeLogicalLocator(relativePath);

        return new StorageObjectReference(
            null,
            StorageProviderKind.FileSystem,
            StorageLocatorKind.RelativePath,
            normalizedRelativePath,
            originalFileName,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim(),
            contentLength,
            $"/managed-files/{normalizedRelativePath}",
            "{}");
    }

    private static JsonSerializerOptions BuildSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static StorageObjectReference NormalizeReference(StorageObjectReference reference)
    {
        if (reference.FormatVersion > StorageObjectReference.CurrentFormatVersion || reference.FormatVersion < 1)
        {
            throw new InvalidOperationException(
                $"Unsupported storage reference format version '{reference.FormatVersion}'.");
        }

        string locator = reference.LocatorKind switch
        {
            StorageLocatorKind.RelativePath or StorageLocatorKind.RemotePath => NormalizeLogicalLocator(reference.Locator),
            _ => reference.Locator.Trim()
        };
        return reference with
        {
            Locator = locator,
            FormatVersion = StorageObjectReference.CurrentFormatVersion
        };
    }

    internal static string NormalizeLogicalLocator(string locator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locator);
        string candidate = locator.Trim();
        if (PhysicalPathSyntaxClassifier.Classify(candidate) != PhysicalPathSyntax.Relative ||
            candidate.StartsWith('\\'))
        {
            throw new InvalidOperationException(
                "A storage logical locator must be relative and cannot use rooted physical or URI syntax.");
        }

        string normalized = candidate.Replace('\\', '/');
        if (normalized.Length == 0 ||
            normalized.Split('/', StringSplitOptions.None).Any(segment =>
                segment.Length == 0 || segment is "." or ".."))
        {
            throw new InvalidOperationException(
                "A storage logical locator must contain canonical non-traversing segments.");
        }

        return normalized;
    }

    private static void ValidateProviderConfigurationLength(string json)
    {
        if (json.Length > MaximumProviderConfigurationJsonLength)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidConfiguration,
                "The storage provider configuration exceeds the supported bounded size."));
        }
    }
}
