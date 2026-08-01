using System.Text;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.Pages;

internal static class ProjectStructureAttachmentNotesResolver
{
    public static bool RequiresContentComparison(
        ProjectStructureNode node,
        ProjectStructureKnownFileInteraction? interaction)
        => interaction is not null &&
           node.ObjectType == ProjectObjectType.File &&
           !string.IsNullOrWhiteSpace(node.Notes) &&
           ProjectStructureNodeHelpers.ResolveAttachmentPreviewKind(node) == AttachmentPreviewKind.TextDocument;

    public static async ValueTask<string?> ResolveSupplementalNotesAsync(
        ProjectStructureNode node,
        ProjectStructureKnownFileInteraction interaction,
        CancellationToken cancellationToken = default)
    {
        if (!RequiresContentComparison(node, interaction))
        {
            return string.IsNullOrWhiteSpace(node.Notes) ? null : node.Notes;
        }

        const int maximumContentBytes = ProjectStructureFileInteractionPolicy.MaximumContentBytes;
        await using FileContentLease content = await interaction.Session.ContentSource.OpenReadAsync(
            new FileContentReadRequest(
                interaction.Session.File,
                0,
                maximumContentBytes + 1L),
            cancellationToken);
        if (content.Length is > maximumContentBytes)
        {
            return node.Notes;
        }

        byte[]? fileBytes = await ReadBoundedBytesAsync(content.Stream, cancellationToken);
        if (fileBytes is null)
        {
            return node.Notes;
        }

        string fileText = await DecodeTextAsync(fileBytes, cancellationToken);
        return string.Equals(
            NormalizeText(node.Notes),
            NormalizeText(fileText),
            StringComparison.Ordinal)
            ? null
            : node.Notes;
    }

    private static async Task<byte[]?> ReadBoundedBytesAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        const int maximumContentBytes = ProjectStructureFileInteractionPolicy.MaximumContentBytes;
        const int readBufferBytes = 81920;
        using var content = new MemoryStream(capacity: readBufferBytes);
        var buffer = new byte[readBufferBytes];
        while (content.Length <= maximumContentBytes)
        {
            int remaining = maximumContentBytes + 1 - checked((int)content.Length);
            int read = await stream.ReadAsync(
                buffer.AsMemory(0, Math.Min(buffer.Length, remaining)),
                cancellationToken);
            if (read == 0)
            {
                return content.ToArray();
            }

            content.Write(buffer, 0, read);
        }

        return null;
    }

    private static async Task<string> DecodeTextAsync(
        byte[] content,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static string NormalizeText(string value)
        => value
            .TrimStart('\uFEFF')
            .Trim();
}
