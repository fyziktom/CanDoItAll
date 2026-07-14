using Microsoft.AspNetCore.Components.Forms;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public static class AgentAvatarUploadFormatter
{
    public const long MaxAvatarUploadBytes = AgentAvatarImagePolicy.MaxAvatarBytes;

    public static bool IsSupportedContentType(string? contentType)
        => AgentAvatarImagePolicy.IsSupportedContentType(contentType);

    public static async Task<string> BuildDataUrlAsync(
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        var contentType = file.ContentType?.Trim() ?? string.Empty;
        if (!AgentAvatarImagePolicy.IsSupportedContentType(contentType))
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

        return AgentAvatarImagePolicy.BuildDataUrl(contentType, memory.ToArray());
    }
}
