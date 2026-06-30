using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Infrastructure.Storage;

public static class StorageJson
{
    private static readonly JsonSerializerOptions SerializerOptions = BuildSerializerOptions();

    public static string SerializeReference(StorageObjectReference? reference)
    {
        return reference is null
            ? string.Empty
            : JsonSerializer.Serialize(reference, SerializerOptions);
    }

    public static StorageObjectReference? ParseReference(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<StorageObjectReference>(json, SerializerOptions);
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
        return JsonSerializer.Serialize(configuration ?? new StorageProviderConfiguration(), SerializerOptions);
    }

    public static StorageProviderConfiguration ParseProviderConfiguration(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new StorageProviderConfiguration();
        }

        return JsonSerializer.Deserialize<StorageProviderConfiguration>(json, SerializerOptions)
            ?? new StorageProviderConfiguration();
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
        var normalizedRelativePath = relativePath
            .Trim()
            .Replace('\\', '/')
            .TrimStart('/');

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
}
