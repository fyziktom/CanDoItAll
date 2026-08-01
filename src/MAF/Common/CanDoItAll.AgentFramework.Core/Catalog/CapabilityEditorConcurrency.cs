using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class CapabilityEditorConcurrency
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string ComputeFingerprint(CapabilityEditorModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var payload = JsonSerializer.Serialize(new
        {
            editor.Id,
            editor.Kind,
            editor.Key,
            editor.Name,
            editor.Description,
            editor.EndpointOrPath,
            editor.ConfigurationJson,
            editor.IsBuiltIn,
            Tags = NormalizeTags(editor.Tags)
        }, SerializerOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
    {
        return tags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim().TrimStart('#').ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }
}
