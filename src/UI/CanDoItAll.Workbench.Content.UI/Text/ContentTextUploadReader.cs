using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Workbench.Content.UI;

internal static class ContentTextUploadReader {
    public static async Task<ContentTextUpload> ReadAsync(IBrowserFile file, int maximumBytes, CancellationToken cancellationToken) {
        var size = file.Size;
        var name = file.Name;
        var contentType = file.ContentType;
        if (maximumBytes <= 0) {
            throw new InvalidOperationException("A positive text upload limit is required.");
        }
        if (size < 0 || size > maximumBytes) {
            throw new InvalidDataException($"Text assets are limited to {maximumBytes / (1024 * 1024)} MiB.");
        }
        await using var source = file.OpenReadStream(maximumBytes, cancellationToken);
        using var destination = new MemoryStream((int)Math.Min(size, 64 * 1024));
        var buffer = new byte[16 * 1024];
        int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0) {
            if (destination.Length + count > maximumBytes || destination.Length + count > size) {
                throw new InvalidDataException("The selected file exceeds its declared size or the text upload limit.");
            }
            await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
        if (destination.Length != size) {
            throw new InvalidDataException("The selected file was truncated while reading. Choose the file again.");
        }
        return new(name, contentType, size, destination.ToArray());
    }
}
