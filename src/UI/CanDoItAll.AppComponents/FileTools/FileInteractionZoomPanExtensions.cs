using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;

namespace CanDoItAll.AppComponents.FileTools;

public static class FileInteractionZoomPanRendererIds
{
    public const string ImageView = "app-image-zoom-pan-view";
    public const string SvgView = "app-svg-zoom-pan-view";
}

internal readonly record struct FileInteractionZoomPanResetKey(
    FileReference File,
    FileContentRevision? ContentRevision,
    long EditRevision,
    ReadOnlyMemory<byte> Content);

public static class FileInteractionZoomPanExtensions
{
    private const int RendererPriority = 100;

    public static FileInteractionComponentBuilder AddZoomPanRenderers(
        this FileInteractionComponentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddRenderer(new FileInteractionRendererDescriptor(
                FileInteractionZoomPanRendererIds.ImageView,
                FileInteractionBuiltInProfileIds.Image,
                FileInteractionMode.View,
                typeof(ZoomPanImageFileView),
                FileInteractionContentKind.Binary,
                priority: RendererPriority))
            .AddRenderer(new FileInteractionRendererDescriptor(
                FileInteractionZoomPanRendererIds.SvgView,
                FileInteractionBuiltInProfileIds.Svg,
                FileInteractionMode.View,
                typeof(ZoomPanSvgFileView),
                FileInteractionContentKind.Binary,
                priority: RendererPriority));
    }
}
