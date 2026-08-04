using Bunit;
using CanDoItAll.AppComponents.FileTools;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tools.Documents;
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
    public void Workbench_registration_selects_the_spreadsheet_preview_renderer()
    {
        var services = new ServiceCollection();
        services.AddWorkbenchModule();
        using ServiceProvider provider = services.BuildServiceProvider();
        FileInteractionComponentComposition composition =
            provider.GetRequiredService<FileInteractionComponentComposition>();
        var request = new FileInteractionRequest(
            new FileReference("test", "forecast"),
            "forecast.xlsx",
            FileInteractionMode.View,
            ProjectStructureFileInteractionPolicy.XlsxMediaType);

        FileInteractionResolution profileResolution = composition.Core.Profiles.Resolve(request);

        Assert.Equal(FileInteractionResolutionStatus.Resolved, profileResolution.Status);
        Assert.Equal(WorkbenchFileInteractionProfileIds.SpreadsheetPreview, profileResolution.Profile?.Id);

        FileInteractionRendererResolution rendererResolution = composition.Renderers.Resolve(
            WorkbenchFileInteractionProfileIds.SpreadsheetPreview,
            FileInteractionMode.View);
        Assert.True(rendererResolution.IsResolved);
        FileInteractionRendererDescriptor renderer = Assert.IsType<FileInteractionRendererDescriptor>(
            rendererResolution.Renderer);
        Assert.Equal(WorkbenchFileInteractionRendererIds.SpreadsheetPreviewView, renderer.Id);
        Assert.Equal(typeof(WorkbenchSpreadsheetFileView), renderer.ComponentType);
        Assert.Equal(FileInteractionContentKind.Binary, renderer.ContentKind);
        Assert.Equal(FileInteractionContentRequirement.FullContent, renderer.ContentRequirement);
    }

    [Fact]
    public void Spreadsheet_renderer_presents_bounded_typed_cells_from_managed_content()
    {
        using var context = CreateBunitContext();
        var spreadsheets = new RecordingSpreadsheetContentPreviewService();
        context.Services.AddSingleton<ISpreadsheetWorkbookContentPreviewService>(spreadsheets);
        byte[] content = [1, 2, 3];

        var cut = context.Render<WorkbenchSpreadsheetFileView>(parameters => parameters
            .Add(component => component.Context, CreateRenderContext(
                "acceptance.xlsx",
                ProjectStructureFileInteractionPolicy.XlsxMediaType,
                content: content)));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(content, spreadsheets.ContentPreviewRequest?.Content.ToArray());
            Assert.Equal("acceptance.xlsx", spreadsheets.ContentPreviewRequest?.WorkbookName);
            Assert.NotNull(cut.Find("[data-testid='workbench-spreadsheet-preview']"));
            Assert.NotNull(cut.Find("[data-testid='workbench-spreadsheet-grid']"));
            Assert.Contains("Acceptance", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Metric", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("=COUNTA(A1:A2)", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("metadata only", cut.Markup, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Spreadsheet_renderer_reloads_same_length_replacement_content()
    {
        using var context = CreateBunitContext();
        var spreadsheets = new RecordingSpreadsheetContentPreviewService();
        context.Services.AddSingleton<ISpreadsheetWorkbookContentPreviewService>(spreadsheets);
        var cut = context.Render<WorkbenchSpreadsheetFileView>(parameters => parameters
            .Add(component => component.Context, CreateRenderContext(
                "acceptance.xlsx",
                ProjectStructureFileInteractionPolicy.XlsxMediaType,
                content: [1, 2, 3])));
        cut.WaitForAssertion(() => Assert.Equal(1, spreadsheets.ContentPreviewCallCount));

        cut.Render(parameters => parameters
            .Add(component => component.Context, CreateRenderContext(
                "acceptance.xlsx",
                ProjectStructureFileInteractionPolicy.XlsxMediaType,
                content: [9, 8, 7])));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, spreadsheets.ContentPreviewCallCount);
            Assert.Equal([9, 8, 7], spreadsheets.ContentPreviewRequest?.Content.ToArray());
        });
    }

    [Fact]
    public void Spreadsheet_renderer_truncates_long_cell_text_for_the_dom()
    {
        using var context = CreateBunitContext();
        string longCell = new('x', 600);
        context.Services.AddSingleton<ISpreadsheetWorkbookContentPreviewService>(
            new RecordingSpreadsheetContentPreviewService(previewCellValue: longCell));

        var cut = context.Render<WorkbenchSpreadsheetFileView>(parameters => parameters
            .Add(component => component.Context, CreateRenderContext(
                "long-cell.xlsx",
                ProjectStructureFileInteractionPolicy.XlsxMediaType,
                content: [1])));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Long cell text truncated", cut.Markup, StringComparison.Ordinal);
            Assert.Contains(new string('x', 512), cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(longCell, cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Spreadsheet_renderer_sanitizes_invalid_workbook_failures()
    {
        using var context = CreateBunitContext();
        context.Services.AddSingleton<ISpreadsheetWorkbookContentPreviewService>(
            new RecordingSpreadsheetContentPreviewService(
                new InvalidDataException("TopSecret workbook detail")));

        var cut = context.Render<WorkbenchSpreadsheetFileView>(parameters => parameters
            .Add(component => component.Context, CreateRenderContext(
                "invalid.xlsx",
                ProjectStructureFileInteractionPolicy.XlsxMediaType,
                content: [1])));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='workbench-spreadsheet-preview-unavailable']"));
            Assert.DoesNotContain("TopSecret", cut.Markup, StringComparison.Ordinal);
        });
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

    private sealed class RecordingSpreadsheetContentPreviewService(
        InvalidDataException? contentPreviewException = null,
        string? previewCellValue = null) : ISpreadsheetWorkbookContentPreviewService
    {
        private readonly object gate = new();
        private SpreadsheetWorkbookContentPreviewRequest? contentPreviewRequest;
        private int contentPreviewCallCount;

        public SpreadsheetWorkbookContentPreviewRequest? ContentPreviewRequest
        {
            get
            {
                lock (gate)
                {
                    return contentPreviewRequest;
                }
            }
        }

        public int ContentPreviewCallCount => Volatile.Read(ref contentPreviewCallCount);

        public SpreadsheetWorkbookContentPreviewResult PreviewWorkbook(
            SpreadsheetWorkbookContentPreviewRequest request)
        {
            lock (gate)
            {
                contentPreviewRequest = request;
            }

            Interlocked.Increment(ref contentPreviewCallCount);
            if (contentPreviewException is not null)
            {
                throw contentPreviewException;
            }

            return new SpreadsheetWorkbookContentPreviewResult(
                request.WorkbookName,
                TotalWorksheetCount: 1,
                [
                    new SpreadsheetWorksheetPreview(
                        "Acceptance",
                        Position: 1,
                        UsedRangeAddress: "A1:B2",
                        UsedRowCount: 2,
                        UsedColumnCount: 2,
                        Values:
                        [
                            ["Metric", "Value"],
                            ["Rows", previewCellValue ?? "=COUNTA(A1:A2)"]
                        ],
                        MarkdownTable: string.Empty,
                        RowsTruncated: false,
                        ColumnsTruncated: false)
                ],
                WorksheetsTruncated: false);
        }
    }
}
