using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public static class AgentAvatarUploadFormatter
{
    public const long MaxAvatarUploadBytes = 128 * 1024;

    private static readonly IReadOnlyDictionary<string, string> SupportedContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = "image/png",
        ["image/jpeg"] = "image/jpeg",
        ["image/jpg"] = "image/jpeg",
        ["image/webp"] = "image/webp",
        ["image/gif"] = "image/gif"
    };

    public static bool IsSupportedContentType(string? contentType)
        => !string.IsNullOrWhiteSpace(contentType) &&
           SupportedContentTypes.ContainsKey(contentType.Trim());

    public static async Task<string> BuildDataUrlAsync(
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        var contentType = file.ContentType?.Trim() ?? string.Empty;
        if (!SupportedContentTypes.TryGetValue(contentType, out var normalizedContentType))
        {
            throw new InvalidOperationException("Avatar image must be PNG, JPEG, WebP, or GIF.");
        }

        if (file.Size <= 0)
        {
            throw new InvalidOperationException("Avatar image file is empty.");
        }

        if (file.Size > MaxAvatarUploadBytes)
        {
            throw new InvalidOperationException($"Avatar image must be {MaxAvatarUploadBytes / 1024} KB or smaller.");
        }

        await using var stream = file.OpenReadStream(MaxAvatarUploadBytes, cancellationToken);
        using var memory = new MemoryStream((int)file.Size);
        await stream.CopyToAsync(memory, cancellationToken);

        return $"data:{normalizedContentType};base64,{Convert.ToBase64String(memory.ToArray())}";
    }
}
