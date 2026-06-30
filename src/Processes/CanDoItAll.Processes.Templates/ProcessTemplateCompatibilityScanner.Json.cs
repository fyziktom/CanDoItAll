using System.Text.Json;

namespace CanDoItAll.Processes.Templates;

public static partial class ProcessTemplateCompatibilityScanner
{
    private static async Task<IReadOnlyList<TemplateProcessEntry>> ReadProcessEntriesAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        using var manifest = await ReadJsonDocumentAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        if (!TryGetProperty(manifest.RootElement, "processes", out var processes) ||
            processes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var entries = new List<TemplateProcessEntry>();
        var index = 0;
        foreach (var item in processes.EnumerateArray())
        {
            if (!TryGetString(item, "key", out var key) ||
                !TryGetString(item, "relativePath", out var relativePath))
            {
                throw new InvalidDataException(
                    $"Template pack manifest entry at index {index} must include non-empty key and relativePath values.");
            }

            entries.Add(new TemplateProcessEntry(key, relativePath));
            index++;
        }

        return entries;
    }

    private static async Task<JsonDocument> ReadJsonDocumentAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static bool TryGetString(JsonElement element, string name, out string value)
    {
        if (TryGetProperty(element, name, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string NormalizeRelative(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    private sealed record TemplateProcessEntry(string Key, string RelativePath);
}
