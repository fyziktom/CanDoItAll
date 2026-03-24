using Bunit;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.ComponentKit.Components;

namespace CanDoItAll.Tests.Components;

public sealed class ImagePrimitiveTests
{
    [Fact]
    public void Factory_uses_media_preview_when_available()
    {
        var surface = new CanvasWorkbenchSurface
        {
            Nodes =
            [
                new CanvasWorkbenchNode
                {
                    Id = "alpha",
                    Title = "Preview node",
                    MediaKind = "image",
                    MediaPreviewUrl = "data:image/svg+xml;utf8,preview"
                }
            ]
        };

        var snapshot = ImagePrimitiveFactory.CreateForWorkbench(surface);

        Assert.True(snapshot.HasImage);
        Assert.Equal("Preview", snapshot.StatePill);
    }

    [Fact]
    public void Component_renders_image_shell()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<ImagePrimitive>(
            parameters => parameters.Add(component => component.Snapshot, new ImagePrimitiveSnapshot
            {
                Title = "Canvas media thumbnails now have one image primitive with placeholder and fit-mode rules",
                Summary = "Preview images share one shell.",
                StatePill = "Preview",
                Metrics = ["1 media-capable nodes"],
                HasImage = true,
                ImageUrl = "data:image/svg+xml;utf8,preview",
                AltText = "Preview",
                ModeLabel = "Cover",
                PlaceholderLabel = "Media loaded"
            }));

        Assert.Contains("Canvas media thumbnails now have one image primitive", cut.Markup);
        Assert.Contains("Media loaded", cut.Markup);
    }
}
