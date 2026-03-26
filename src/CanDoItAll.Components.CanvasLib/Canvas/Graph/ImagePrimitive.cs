namespace CanDoItAll.Components.CanvasLib;

public sealed class ImagePrimitiveSnapshot
{
    public string TestHookId { get; init; } = "image-primitive";

    public string Label { get; init; } = "Image primitive";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public bool HasImage { get; init; }

    public string ImageUrl { get; init; } = string.Empty;

    public string AltText { get; init; } = string.Empty;

    public string ModeLabel { get; init; } = string.Empty;

    public string PlaceholderLabel { get; init; } = string.Empty;
}

public static class ImagePrimitiveFactory
{
    public static ImagePrimitiveSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var mediaNode = surface.Nodes.FirstOrDefault(node =>
            !string.IsNullOrWhiteSpace(node.MediaPreviewUrl)
            || !string.IsNullOrWhiteSpace(node.MediaKind)
            || !string.IsNullOrWhiteSpace(node.MediaFileName));
        var imageUrl = !string.IsNullOrWhiteSpace(mediaNode?.MediaPreviewUrl)
            ? mediaNode.MediaPreviewUrl
            : BuildFallbackArt(mediaNode?.Title ?? "Canvas preview");
        var hasImage = mediaNode is not null;
        var modeLabel = mediaNode?.MediaKind switch
        {
            "image" => "Cover",
            "document" => "Poster",
            "link" => "Thumbnail",
            _ => hasImage ? "Preview" : "Placeholder"
        };

        return new ImagePrimitiveSnapshot
        {
            Title = "Canvas media thumbnails now have one image primitive with placeholder and fit-mode rules",
            Summary = "Attachment previews, future cover images, and inspector thumbnails can all inherit the same loading, fallback, and aspect-ratio conventions.",
            StatePill = hasImage ? "Preview" : "Placeholder",
            Metrics =
            [
                $"{surface.Nodes.Count(node => !string.IsNullOrWhiteSpace(node.MediaPreviewUrl) || !string.IsNullOrWhiteSpace(node.MediaFileName))} media-capable nodes",
                $"{surface.Nodes.Count(node => string.Equals(node.MediaKind, "image", StringComparison.OrdinalIgnoreCase))} image nodes",
                $"{surface.Nodes.Count(node => string.Equals(node.MediaKind, "document", StringComparison.OrdinalIgnoreCase))} document previews",
                $"{surface.Nodes.Count(node => !string.IsNullOrWhiteSpace(node.MediaFileName))} named media assets"
            ],
            HasImage = hasImage,
            ImageUrl = imageUrl,
            AltText = mediaNode?.MediaPreviewAlt ?? mediaNode?.Title ?? "Canvas media preview",
            ModeLabel = modeLabel,
            PlaceholderLabel = hasImage ? "Media loaded" : "Preview placeholder"
        };
    }

    private static string BuildFallbackArt(string label)
    {
        var svg = $"""
            <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 320 180'>
              <defs>
                <linearGradient id='g' x1='0' y1='0' x2='1' y2='1'>
                  <stop offset='0%' stop-color='#e2e8f0' />
                  <stop offset='100%' stop-color='#bae6fd' />
                </linearGradient>
              </defs>
              <rect width='320' height='180' rx='24' fill='url(#g)' />
              <circle cx='90' cy='72' r='18' fill='#38bdf8' opacity='0.72' />
              <path d='M48 138 L126 92 L176 126 L238 74 L286 138 Z' fill='#0f172a' opacity='0.12' />
              <text x='50%' y='146' dominant-baseline='middle' text-anchor='middle' fill='#0f172a' font-family='Segoe UI' font-size='20'>{label}</text>
            </svg>
            """;
        return "data:image/svg+xml;utf8," + Uri.EscapeDataString(svg);
    }
}


