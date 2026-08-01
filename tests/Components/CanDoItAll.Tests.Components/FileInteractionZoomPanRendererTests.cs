using Bunit;
using CanDoItAll.AppComponents.FileTools;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class FileInteractionZoomPanRendererTests
{
    private const string ZoomPanModulePath =
        "./_content/CanDoItAll.Components.BaseLib/Components/Layout/ZoomPanFrame.razor.js";
    private const string FileObjectViewModulePath =
        "./_content/CanDoItAll.FileTools.FileInteraction.Components/Components/FileObjectView.razor.js";

    [Theory]
    [InlineData(
        FileInteractionBuiltInProfileIds.Image,
        FileInteractionZoomPanRendererIds.ImageView,
        typeof(ZoomPanImageFileView))]
    [InlineData(
        FileInteractionBuiltInProfileIds.Svg,
        FileInteractionZoomPanRendererIds.SvgView,
        typeof(ZoomPanSvgFileView))]
    public void App_renderers_override_the_matching_built_in_viewer(
        string profileId,
        string expectedRendererId,
        Type expectedComponentType)
    {
        FileInteractionComponentComposition composition = new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddZoomPanRenderers()
            .Build();

        FileInteractionRendererResolution resolution = composition.Renderers.Resolve(
            profileId,
            FileInteractionMode.View);

        Assert.True(resolution.IsResolved);
        FileInteractionRendererDescriptor renderer = Assert.IsType<FileInteractionRendererDescriptor>(
            resolution.Renderer);
        Assert.Equal(expectedRendererId, renderer.Id);
        Assert.Equal(expectedComponentType, renderer.ComponentType);
        Assert.Equal(FileInteractionContentKind.Binary, renderer.ContentKind);
        Assert.True(renderer.Priority > 0);
    }

    [Fact]
    public void Workbench_registration_selects_the_app_renderers()
    {
        var services = new ServiceCollection();
        services.AddWorkbenchModule();
        using ServiceProvider provider = services.BuildServiceProvider();
        FileInteractionComponentComposition composition =
            provider.GetRequiredService<FileInteractionComponentComposition>();

        Assert.Equal(
            typeof(ZoomPanImageFileView),
            composition.Renderers.Resolve(FileInteractionBuiltInProfileIds.Image, FileInteractionMode.View)
                .Renderer?.ComponentType);
        Assert.Equal(
            typeof(ZoomPanSvgFileView),
            composition.Renderers.Resolve(FileInteractionBuiltInProfileIds.Svg, FileInteractionMode.View)
                .Renderer?.ComponentType);
    }

    [Fact]
    public async Task Image_renderer_frames_the_generated_image_target()
    {
        using var context = CreateBunitContext();
        FileInteractionRenderContext renderContext = CreateRenderContext(
            "photo.png",
            "image/png");

        var cut = context.Render<ZoomPanImageFileView>(parameters => parameters
            .Add(component => component.Context, renderContext));

        var objectView = cut.FindComponent<FileObjectView>();
        var frame = cut.FindComponent<ZoomPanFrame>();
        string frameStyle = Assert.IsType<string>(frame.Instance.Style);
        Assert.Equal(FileObjectViewKind.Image, objectView.Instance.Kind);
        Assert.False(frame.Instance.SuppressContentInteraction);
        Assert.Equal("Zoom and pan photo.png", frame.Instance.AriaLabel);
        Assert.Contains("place-self: stretch", frameStyle, StringComparison.Ordinal);
        Assert.Contains("--cda-zoom-pan-background: transparent", frameStyle, StringComparison.Ordinal);

        await frame.Find("img").TriggerEventAsync("onload", EventArgs.Empty);

        Assert.False(cut.Find("[data-testid='interaction-image-view']").HasAttribute("hidden"));
    }

    [Fact]
    public void Svg_renderer_frames_the_sandboxed_target_and_suppresses_embedded_interaction()
    {
        using var context = CreateBunitContext();
        FileInteractionRenderContext renderContext = CreateRenderContext(
            "diagram.svg",
            "image/svg+xml");

        var cut = context.Render<ZoomPanSvgFileView>(parameters => parameters
            .Add(component => component.Context, renderContext));

        var objectView = cut.FindComponent<FileObjectView>();
        var frame = cut.FindComponent<ZoomPanFrame>();
        var iframe = frame.Find("iframe");
        Assert.Equal(FileObjectViewKind.Browser, objectView.Instance.Kind);
        Assert.True(frame.Instance.SuppressContentInteraction);
        Assert.NotNull(iframe.Closest("[inert]"));
        Assert.True(iframe.HasAttribute("sandbox"));
        Assert.Equal(string.Empty, iframe.GetAttribute("sandbox"));
        Assert.Equal("no-referrer", iframe.GetAttribute("referrerpolicy"));
        cut.WaitForAssertion(() =>
            Assert.False(cut.Find("[data-testid='interaction-browser-view']").HasAttribute("hidden")));
    }

    [Fact]
    public void Reset_key_tracks_file_content_and_edit_identity()
    {
        using var context = CreateBunitContext();
        byte[] content = [1];
        var cut = context.Render<ZoomPanImageFileView>(parameters => parameters
            .Add(component => component.Context, CreateRenderContext(
                "photo.png",
                "image/png",
                fileValue: "first",
                contentRevision: "r1",
                editRevision: 0,
                content: content)));
        object? initialKey = cut.FindComponent<ZoomPanFrame>().Instance.ResetKey;
        Assert.NotNull(initialKey);

        cut.Render(parameters => parameters
            .Add(component => component.Context, CreateRenderContext(
                "photo.png",
                "image/png",
                fileValue: "first",
                contentRevision: "r2",
                editRevision: 0,
                content: content)));
        object? contentRevisionKey = cut.FindComponent<ZoomPanFrame>().Instance.ResetKey;
        Assert.NotNull(contentRevisionKey);

        cut.Render(parameters => parameters
            .Add(component => component.Context, CreateRenderContext(
                "photo.png",
                "image/png",
                fileValue: "first",
                contentRevision: "r2",
                editRevision: 1,
                content: content)));
        object? editRevisionKey = cut.FindComponent<ZoomPanFrame>().Instance.ResetKey;
        Assert.NotNull(editRevisionKey);

        cut.Render(parameters => parameters
            .Add(component => component.Context, CreateRenderContext(
                "photo.png",
                "image/png",
                fileValue: "second",
                contentRevision: "r2",
                editRevision: 1,
                content: content)));
        object? fileKey = cut.FindComponent<ZoomPanFrame>().Instance.ResetKey;
        Assert.NotNull(fileKey);

        cut.Render(parameters => parameters
            .Add(component => component.Context, CreateRenderContext(
                "photo.png",
                "image/png",
                fileValue: "second",
                contentRevision: "r2",
                editRevision: 1,
                content: [2])));
        object? contentKey = cut.FindComponent<ZoomPanFrame>().Instance.ResetKey;
        Assert.NotNull(contentKey);

        Assert.NotEqual(initialKey, contentRevisionKey);
        Assert.NotEqual(contentRevisionKey, editRevisionKey);
        Assert.NotEqual(editRevisionKey, fileKey);
        Assert.NotEqual(fileKey, contentKey);
    }

    private static BunitContext CreateBunitContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var zoomPanModule = context.JSInterop.SetupModule(ZoomPanModulePath);
        zoomPanModule.SetupVoid("initialize", _ => true).SetVoidResult();
        zoomPanModule.SetupVoid("configure", _ => true).SetVoidResult();
        zoomPanModule.SetupVoid("reset", _ => true).SetVoidResult();
        zoomPanModule.SetupVoid("destroy", _ => true).SetVoidResult();
        var fileObjectViewModule = context.JSInterop.SetupModule(FileObjectViewModulePath);
        fileObjectViewModule.SetupVoid("applyObjectUrl", _ => true).SetVoidResult();
        fileObjectViewModule.SetupVoid("revokeObjectUrl", _ => true).SetVoidResult();
        return context;
    }

    private static FileInteractionRenderContext CreateRenderContext(
        string fileName,
        string mediaType,
        string fileValue = "preview",
        string contentRevision = "r1",
        long editRevision = 0,
        byte[]? content = null)
    {
        var request = new FileInteractionRequest(
            new FileReference("test", fileValue),
            fileName,
            mediaType: mediaType,
            contentRevision: new FileContentRevision(contentRevision));
        return new FileInteractionRenderContext(
            request,
            FileInteractionMode.View,
            content ?? [1],
            editRevision,
            mediaType);
    }
}
