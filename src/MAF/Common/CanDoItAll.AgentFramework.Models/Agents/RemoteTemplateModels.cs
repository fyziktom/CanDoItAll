using System.ComponentModel;

namespace CanDoItAll.AgentFramework.Models;

[Description("The model capabilities needed by one part of a shared definition.")]
public sealed record RemoteTemplateModelRequirement(
    [property: Description("Name of the model-dependent step in the template.")] string Label,
    [property: Description("Provider purpose: 0 Chat, 1 ImageGeneration.")] ProviderProfilePurpose Purpose,
    [property: Description("Whether this step requires structured model output.")] bool StructuredOutput = false,
    [property: Description("Whether this step requires image inputs.")] bool Vision = false,
    [property: Description("Whether this step requires function tools.")] bool Tools = false,
    [property: Description("Human-readable model used on the publisher, when configured.")] string? ModelName = null,
    [property: Description("Public routing model of the publisher's shared provider, when published.")] string? PublishedModel = null);

public sealed record RemoteTemplateModelChoice(Guid ProviderId, string Model);

public static class RemoteTemplateAvatar {
    public static string? ForDisplay(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        if (AgentAvatarImageCatalog.IsBundledAvatarUrl(value)) {
            return value.Trim();
        }
        if (value.Length <= 2048 && Uri.TryCreate(value, UriKind.Absolute, out var image) &&
            (image.Scheme == Uri.UriSchemeHttps || image.Scheme == Uri.UriSchemeHttp) &&
            image.UserInfo.Length == 0) {
            return image.AbsoluteUri;
        }
        const string prefix = "data:";
        const string separator = ";base64,";
        var offset = value.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || offset <= prefix.Length ||
            !AgentAvatarImagePolicy.TryNormalizeContentType(value.AsSpan(prefix.Length, offset - prefix.Length), out var type)) {
            return null;
        }
        var payload = value.AsSpan(offset + separator.Length);
        if (payload.Length == 0 || payload.Length > (AgentAvatarImagePolicy.MaxAvatarBytes + 2) / 3 * 4) {
            return null;
        }
        var bytes = new byte[payload.Length / 4 * 3];
        if (!Convert.TryFromBase64Chars(payload, bytes, out var count) || count is 0 or > AgentAvatarImagePolicy.MaxAvatarBytes) {
            return null;
        }
        var data = bytes.AsSpan(0, count);
        var valid = type switch {
            "image/png" => data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
            "image/jpeg" => data.StartsWith((ReadOnlySpan<byte>)[0xff, 0xd8, 0xff]),
            "image/gif" => data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8),
            "image/webp" => data.Length >= 12 && data.StartsWith("RIFF"u8) && data[8..].StartsWith("WEBP"u8),
            _ => false
        };
        return valid ? $"data:{type};base64,{Convert.ToBase64String(data)}" : null;
    }
}
