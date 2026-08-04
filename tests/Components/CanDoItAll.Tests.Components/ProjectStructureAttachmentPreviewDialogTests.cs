using Bunit;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Markdown;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Components.Mermaid;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tools.Documents;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureAttachmentPreviewDialogTests
{
    [Theory]
    [InlineData(ProjectObjectType.ImageAsset, "image/png", "asset.png")]
    [InlineData(ProjectObjectType.File, "application/pdf", "report.pdf")]
    [InlineData(ProjectObjectType.File, "text/plain", "notes.txt")]
    [InlineData(ProjectObjectType.VideoAsset, "video/mp4", "clip.mp4")]
    [InlineData(ProjectObjectType.File, "image/svg+xml", "hostile.svg")]
    public void Storage_backed_dialogs_render_direct_interaction_without_a_browser_or_route(
        ProjectObjectType objectType,
        string mediaType,
        string fileName)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterComposition(context);
        var node = CreateNode(objectType, mediaType, fileName);
        var file = new FileReference("authorized", $"handle-{fileName}");
        var session = new FileToolsKnownFileSession(
            file,
            new StaticContentSource(mediaType),
            FileToolsKnownFileIntent.ReadOnly);
        var interaction = new ProjectStructureKnownFileInteraction(
            new FileInteractionRequest(file, fileName, FileInteractionMode.View, mediaType, 3),
            session,
            new NoopSessionReleaser());

        var cut = context.Render<ProjectStructureAttachmentPreviewDialog>(parameters => parameters
            .Add(component => component.Node, node)
            .Add(component => component.Interaction, interaction)
            .Add(component => component.Close, EventCallback.Empty)
            .Add(component => component.CanShowLocalOpen, _ => false)
            .Add(component => component.CanOpenInPreferredApplication, _ => false)
            .Add(component => component.CanEditNode, _ => false));

        cut.WaitForAssertion(() =>
        {
            bool expectsBrowserFrame = mediaType is "video/mp4" or "image/svg+xml";
            Assert.Single(cut.FindComponents<FileInteraction>());
            Assert.Empty(cut.FindComponents<FileBrowser>());
            Assert.NotNull(cut.Find("[data-testid='project-structure-direct-file-interaction']"));
            Assert.DoesNotContain($"/managed-files/{fileName}", cut.Markup, StringComparison.Ordinal);
            Assert.Equal(expectsBrowserFrame, cut.FindAll("iframe").Count == 1);
            Assert.DoesNotContain("<video", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<audio", cut.Markup, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Dialog_shows_node_notes_and_keeps_open_and_reveal_as_separate_actions()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterComposition(context);
        var node = CreateNode(
            ProjectObjectType.File,
            ProjectStructureFileInteractionPolicy.XlsxMediaType,
            "forecast.xlsx",
            "Reviewed by finance.");
        var file = new FileReference("authorized", "forecast-handle");
        var source = new StaticContentSource(ProjectStructureFileInteractionPolicy.XlsxMediaType);
        var session = new FileToolsKnownFileSession(
            file,
            source,
            FileToolsKnownFileIntent.ReadOnly);
        var interaction = new ProjectStructureKnownFileInteraction(
            new FileInteractionRequest(
                file,
                "forecast.xlsx",
                FileInteractionMode.View,
                ProjectStructureFileInteractionPolicy.XlsxMediaType,
                3),
            session,
            new NoopSessionReleaser());
        bool opened = false;
        bool revealed = false;

        var cut = context.Render<ProjectStructureAttachmentPreviewDialog>(parameters => parameters
            .Add(component => component.Node, node)
            .Add(component => component.Interaction, interaction)
            .Add(component => component.Close, EventCallback.Empty)
            .Add(component => component.OpenInPreferredApplication, (ProjectStructureNode _) => opened = true)
            .Add(component => component.OpenLocally, (ProjectStructureNode _) => revealed = true)
            .Add(component => component.CanShowLocalOpen, _ => true)
            .Add(component => component.CanOpenInPreferredApplication, _ => true)
            .Add(component => component.CanEditNode, _ => false));

        Assert.Contains("Reviewed by finance.", cut.Markup, StringComparison.Ordinal);
        var notes = cut.Find("[data-testid='project-structure-file-interaction-notes']");
        Assert.Contains("max-height:min(12rem,35%)", notes.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("overflow:auto", notes.GetAttribute("style"), StringComparison.Ordinal);
        var fileInteraction = cut.FindComponent<FileInteraction>();
        fileInteraction.WaitForAssertion(() =>
        {
            Assert.True(source.ReadCount > 0);
            Assert.Single(fileInteraction.FindComponents<WorkbenchSpreadsheetFileView>());
        });
        var spreadsheet = fileInteraction.FindComponent<WorkbenchSpreadsheetFileView>();
        spreadsheet.WaitForAssertion(() =>
        {
            Assert.NotNull(spreadsheet.Find("[data-testid='workbench-spreadsheet-preview']"));
            Assert.NotNull(spreadsheet.Find("[data-testid='workbench-spreadsheet-grid']"));
            Assert.Contains("Acceptance", spreadsheet.Markup, StringComparison.Ordinal);
            Assert.Contains("=COUNTA(A1:A2)", spreadsheet.Markup, StringComparison.Ordinal);
            Assert.Empty(cut.FindAll("iframe"));
        });
        await cut.FindAll("button")
            .Single(button => button.TextContent.Trim() == "Open in preferred app")
            .ClickAsync(new());
        await cut.FindAll("button")
            .Single(button => button.TextContent.Trim() == "Show in folder")
            .ClickAsync(new());

        Assert.True(opened);
        Assert.True(revealed);
    }

    [Fact]
    public void Managed_markdown_payload_is_not_repeated_as_node_notes()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterComposition(context);
        const string markdown = """
            # Result Summary

            The governed file content is the primary surface.
            """;
        var node = CreateNode(
            ProjectObjectType.File,
            "text/markdown",
            "result-summary.md",
            markdown,
            "markdown");
        var file = new FileReference("authorized", "handle-result-summary");
        var source = new StaticContentSource("text/markdown", markdown);
        var session = new FileToolsKnownFileSession(
            file,
            source,
            FileToolsKnownFileIntent.ReadOnly);
        var interaction = new ProjectStructureKnownFileInteraction(
            new FileInteractionRequest(
                file,
                "result-summary.md",
                FileInteractionMode.View,
                "text/markdown",
                Encoding.UTF8.GetByteCount(markdown)),
            session,
            new NoopSessionReleaser());
        IRenderedComponent<ProjectStructureAttachmentPreviewDialog> cut = Render(context, node, interaction);

        cut.WaitForAssertion(() =>
        {
            Assert.True(source.ReadCount >= 2);
            Assert.Empty(cut.FindAll("[data-testid='project-structure-file-interaction-notes']"));
            Assert.Single(cut.FindComponents<FileInteraction>());
            Assert.NotNull(cut.Find("[data-testid='interaction-markdown-view']"));
        });
    }

    [Fact]
    public void Managed_markdown_keeps_distinct_supplemental_notes()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterComposition(context);

        IRenderedComponent<ProjectStructureAttachmentPreviewDialog> cut = RenderReadOnly(
            context,
            "result-summary.md",
            "text/markdown",
            "# Result Summary\n\nFile payload.",
            notes: "Reviewed by finance.",
            objectSubtype: "markdown");

        cut.WaitForAssertion(() =>
        {
            var notes = cut.Find("[data-testid='project-structure-file-interaction-notes']");
            Assert.Contains("Reviewed by finance.", notes.TextContent, StringComparison.Ordinal);
            Assert.Contains("max-height:min(12rem,35%)", notes.GetAttribute("style"), StringComparison.Ordinal);
            Assert.Contains("overflow:auto", notes.GetAttribute("style"), StringComparison.Ordinal);
            Assert.NotNull(cut.Find("[data-testid='interaction-markdown-view']"));
        });
    }

    [Fact]
    public async Task Notes_comparison_bounds_unknown_length_content_by_bytes()
    {
        const string notes = "Keep this supplemental note.";
        var node = CreateNode(
            ProjectObjectType.File,
            "text/plain",
            "unknown-length.txt",
            notes);
        var file = new FileReference("authorized", "unknown-length-handle");
        var source = new UnknownLengthContentSource(
            ProjectStructureFileInteractionPolicy.MaximumContentBytes + 2L);
        var session = new FileToolsKnownFileSession(
            file,
            source,
            FileToolsKnownFileIntent.ReadOnly);
        var interaction = new ProjectStructureKnownFileInteraction(
            new FileInteractionRequest(
                file,
                "unknown-length.txt",
                FileInteractionMode.View,
                "text/plain",
                null),
            session,
            new NoopSessionReleaser());

        string? resolvedNotes = await ProjectStructureAttachmentNotesResolver.ResolveSupplementalNotesAsync(
            node,
            interaction);

        Assert.Equal(notes, resolvedNotes);
        Assert.Equal(
            ProjectStructureFileInteractionPolicy.MaximumContentBytes + 1L,
            source.RequestedLength);
        Assert.Equal(
            ProjectStructureFileInteractionPolicy.MaximumContentBytes + 1L,
            source.BytesRead);
    }

    [Fact]
    public async Task File_preview_can_maximize_and_restore_within_the_canvas()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterComposition(context);
        var cut = RenderReadOnly(context, "report.pdf", "application/pdf", "bounded pdf payload");

        var dialog = cut.Find("[role='dialog']");
        Assert.Equal("false", dialog.GetAttribute("data-maximized"));

        await cut.Find("[data-testid='project-structure-dialog-size-toggle']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("true", cut.Find("[role='dialog']").GetAttribute("data-maximized"));
            Assert.Contains("project-structure-preview-dialog--fullscreen", cut.Find("[role='dialog']").ClassList);
            Assert.Equal(
                "Restore preview size",
                cut.Find("[data-testid='project-structure-dialog-size-toggle']").GetAttribute("aria-label"));
        });

        await cut.Find("[data-testid='project-structure-dialog-size-toggle']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", cut.Find("[role='dialog']").GetAttribute("data-maximized"));
            Assert.DoesNotContain("project-structure-preview-dialog--fullscreen", cut.Find("[role='dialog']").ClassList);
        });
    }

    [Fact]
    public async Task Editable_text_dialog_guards_close_and_awaits_revisioned_save()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterComposition(context);
        var node = CreateNode(ProjectObjectType.File, "text/markdown", "notes.md");
        var file = new FileReference("authorized", "editable-handle", "revision-1");
        var saveTarget = new RecordingSaveTarget();
        var session = new FileToolsKnownFileSession(
            file,
            new StaticContentSource("text/markdown", "# Before"),
            FileToolsKnownFileIntent.Edit,
            saveTarget);
        var interaction = new ProjectStructureKnownFileInteraction(
            new FileInteractionRequest(
                file,
                "notes.md",
                FileInteractionMode.View,
                "text/markdown",
                8,
                new FileContentRevision("revision-1")),
            session,
            new NoopSessionReleaser());
        bool closed = false;
        var cut = context.Render<ProjectStructureAttachmentPreviewDialog>(parameters => parameters
            .Add(component => component.Node, node)
            .Add(component => component.Interaction, interaction)
            .Add(component => component.Close, EventCallback.Factory.Create(this, () => closed = true))
            .Add(component => component.CanShowLocalOpen, _ => false)
            .Add(component => component.CanOpenInPreferredApplication, _ => false)
            .Add(component => component.CanEditNode, _ => false));

        await cut.WaitForElement("[data-testid='interaction-mode-edit']").ClickAsync(new());
        var editor = cut.WaitForElement("[data-testid='interaction-text-editor']");
        await editor.InputAsync(new ChangeEventArgs { Value = "# Changed" });
        cut.WaitForAssertion(() => Assert.Contains("Unsaved", cut.Markup, StringComparison.Ordinal));

        await cut.FindAll("button")
            .Single(button => button.TextContent.Trim().Equals("Close", StringComparison.Ordinal))
            .ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            Assert.False(closed);
            Assert.NotNull(cut.Find("[data-testid='project-structure-interaction-close-guard']"));
        });

        await cut.Find("[data-testid='interaction-save']").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, saveTarget.CallCount);
            Assert.Equal("revision-1", saveTarget.LastRequest?.ExpectedRevision?.Value);
            Assert.Equal("# Changed", saveTarget.LastContent);
            Assert.DoesNotContain("Unsaved", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Markdown_uses_advanced_markdig_with_inert_content_and_strict_mermaid_fences()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterComposition(context);
        string markdown = """
            <script>window.pwned=true</script>
            [bad](javascript:alert(1))
            ![remote](https://example.invalid/x.png)

            | Feature | State |
            | --- | --- |
            | Markdig | **active** |

            ```mermaid
            flowchart LR
            A-->B
            ```
            """;
        IRenderedComponent<ProjectStructureAttachmentPreviewDialog> markdownCut = RenderReadOnly(
            context,
            "hostile.md",
            "text/markdown",
            markdown);

        markdownCut.WaitForAssertion(() =>
        {
            Assert.NotNull(markdownCut.Find("[data-testid='interaction-markdown-view']"));
            Assert.DoesNotContain("<script", markdownCut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("javascript:", markdownCut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<img", markdownCut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<table", markdownCut.Markup, StringComparison.OrdinalIgnoreCase);
            var diagram = markdownCut.FindComponent<MermaidDiagram>();
            Assert.Equal("strict", diagram.Instance.Options.SecurityLevel);
            Assert.False(diagram.Instance.Options.HtmlLabels);
            Assert.Equal(
                "flowchart LR\nA-->B",
                diagram.Instance.Source!.Replace("\r\n", "\n", StringComparison.Ordinal));
        });

        IRenderedComponent<ProjectStructureAttachmentPreviewDialog> mermaidCut = RenderReadOnly(
            context,
            "diagram.mmd",
            ProjectStructureFileInteractionPolicy.MermaidMediaType,
            "flowchart LR\nA-->B");

        mermaidCut.WaitForAssertion(() =>
        {
            var diagram = mermaidCut.FindComponent<MermaidDiagram>();
            Assert.Equal("strict", diagram.Instance.Options.SecurityLevel);
            Assert.False(diagram.Instance.Options.HtmlLabels);
        });
    }

    [Fact]
    public void Oversized_text_is_rejected_at_the_host_limit_before_stream_read()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterComposition(context);
        var node = CreateNode(ProjectObjectType.File, "text/plain", "oversized.txt");
        var file = new FileReference("authorized", "oversized-handle");
        var source = new MetadataOnlyContentSource(
            "text/plain",
            ProjectStructureFileInteractionPolicy.MaximumContentBytes + 1L);
        var session = new FileToolsKnownFileSession(file, source, FileToolsKnownFileIntent.ReadOnly);
        var interaction = new ProjectStructureKnownFileInteraction(
            new FileInteractionRequest(
                file,
                "oversized.txt",
                FileInteractionMode.View,
                "text/plain",
                ProjectStructureFileInteractionPolicy.MaximumContentBytes + 1L),
            session,
            new NoopSessionReleaser());

        var cut = Render(context, node, interaction);

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='interaction-error']"));
            Assert.Equal(
                ProjectStructureFileInteractionPolicy.MaximumContentBytes + 1L,
                source.RequestedLength);
            Assert.Equal(0, source.ReadCount);
        });
    }

    [Fact]
    public void Unknown_file_uses_sandboxed_browser_fallback_with_bounded_content()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RegisterComposition(context);
        var node = CreateNode(ProjectObjectType.File, "application/zip", "archive.zip");
        var file = new FileReference("authorized", "archive-handle");
        var source = new MetadataOnlyContentSource("application/zip", 128);
        var session = new FileToolsKnownFileSession(file, source, FileToolsKnownFileIntent.ReadOnly);
        var interaction = new ProjectStructureKnownFileInteraction(
            new FileInteractionRequest(file, "archive.zip", FileInteractionMode.View, "application/zip", 128),
            session,
            new NoopSessionReleaser());

        var cut = Render(context, node, interaction);

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='interaction-browser-view']"));
            Assert.True(source.ReadCount > 0);
            Assert.Equal(string.Empty, cut.Find("iframe").GetAttribute("sandbox"));
        });
    }

    private static void RegisterComposition(BunitContext context)
    {
        context.Services.AddLogging();
        context.Services.AddSingleton(new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddMarkdown()
            .AddWorkbenchMermaid()
            .AddWorkbenchSpreadsheetPreview()
            .Build());
        context.Services.AddSingleton<ISpreadsheetWorkbookContentPreviewService>(
            new StaticSpreadsheetContentPreviewService());
        context.Services.AddSingleton<IMarkdownFencedCodeComponentRegistration,
            WorkbenchMarkdownMermaidComponentRegistration>();
    }

    private static IRenderedComponent<ProjectStructureAttachmentPreviewDialog> RenderReadOnly(
        BunitContext context,
        string fileName,
        string mediaType,
        string content,
        string notes = "",
        string objectSubtype = "")
    {
        var node = CreateNode(ProjectObjectType.File, mediaType, fileName, notes, objectSubtype);
        var file = new FileReference("authorized", $"handle-{Guid.NewGuid():N}");
        var session = new FileToolsKnownFileSession(
            file,
            new StaticContentSource(mediaType, content),
            FileToolsKnownFileIntent.ReadOnly);
        var interaction = new ProjectStructureKnownFileInteraction(
            new FileInteractionRequest(file, fileName, FileInteractionMode.View, mediaType, Encoding.UTF8.GetByteCount(content)),
            session,
            new NoopSessionReleaser());
        return Render(context, node, interaction);
    }

    private static IRenderedComponent<ProjectStructureAttachmentPreviewDialog> Render(
        BunitContext context,
        ProjectStructureNode node,
        ProjectStructureKnownFileInteraction interaction)
        => context.Render<ProjectStructureAttachmentPreviewDialog>(parameters => parameters
            .Add(component => component.Node, node)
            .Add(component => component.Interaction, interaction)
            .Add(component => component.Close, EventCallback.Empty)
            .Add(component => component.CanShowLocalOpen, _ => false)
            .Add(component => component.CanOpenInPreferredApplication, _ => false)
            .Add(component => component.CanEditNode, _ => false));

    private static ProjectStructureNode CreateNode(
        ProjectObjectType objectType,
        string mediaType,
        string fileName,
        string notes = "",
        string objectSubtype = "")
        => new(
            Id: $"asset:{fileName}",
            ParentId: null,
            ObjectType: objectType,
            ObjectSubtype: objectSubtype,
            Title: fileName,
            Subtitle: string.Empty,
            Status: "Ready",
            Notes: notes,
            Route: $"/managed-files/{fileName}",
            ArtifactKind: string.Empty,
            ArtifactId: null,
            MediaRelativePath: fileName,
            MediaContentType: mediaType,
            MediaOriginalFileName: fileName,
            X: 0,
            Y: 0,
            VisualProfile: new ProjectObjectVisualProfile("rectangle", "#000000", "file", string.Empty),
            Badges: [],
            ProgressMode: string.Empty,
            ProgressPercent: 0,
            MarkerIcon: string.Empty,
            MarkerTone: string.Empty,
            MarkerLabel: string.Empty,
            Markers: [],
            Priority: 0,
            StorageObjectReferenceJson: "{}");

    private sealed class StaticContentSource(string mediaType, string? content = null) : IFileContentSource
    {
        private int readCount;

        public int ReadCount => Volatile.Read(ref readCount);

        public ValueTask<FileContentLease> OpenReadAsync(
            FileContentReadRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref readCount);
            byte[] bytes = content is null ? [1, 2, 3] : Encoding.UTF8.GetBytes(content);
            return ValueTask.FromResult(new FileContentLease(
                new MemoryStream(bytes),
                mediaType,
                bytes.LongLength));
        }
    }

    private sealed class StaticSpreadsheetContentPreviewService
        : ISpreadsheetWorkbookContentPreviewService
    {
        public SpreadsheetWorkbookContentPreviewResult PreviewWorkbook(
            SpreadsheetWorkbookContentPreviewRequest request)
            => new(
                request.WorkbookName,
                TotalWorksheetCount: 1,
                [
                    new SpreadsheetWorksheetPreview(
                        "Acceptance",
                        Position: 1,
                        UsedRangeAddress: "A1:B2",
                        UsedRowCount: 2,
                        UsedColumnCount: 2,
                        Values: [["Metric", "Value"], ["Rows", "=COUNTA(A1:A2)"]],
                        MarkdownTable: string.Empty,
                        RowsTruncated: false,
                        ColumnsTruncated: false)
                ],
                WorksheetsTruncated: false);
    }

    private sealed class UnknownLengthContentSource(long availableBytes) : IFileContentSource
    {
        public long? RequestedLength { get; private set; }

        public long BytesRead { get; private set; }

        public ValueTask<FileContentLease> OpenReadAsync(
            FileContentReadRequest request,
            CancellationToken cancellationToken = default)
        {
            RequestedLength = request.Length;
            return ValueTask.FromResult(new FileContentLease(
                new FixedLengthReadStream(
                    availableBytes,
                    read => BytesRead += read),
                "text/plain",
                length: null));
        }
    }

    private sealed class MetadataOnlyContentSource(string mediaType, long length) : IFileContentSource
    {
        public long? RequestedLength { get; private set; }

        public int ReadCount { get; private set; }

        public ValueTask<FileContentLease> OpenReadAsync(
            FileContentReadRequest request,
            CancellationToken cancellationToken = default)
        {
            RequestedLength = request.Length;
            return ValueTask.FromResult(new FileContentLease(
                new ReadTrackingStream(() => ReadCount++),
                mediaType,
                length));
        }
    }

    private sealed class ReadTrackingStream(Action onRead) : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            onRead();
            return base.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            onRead();
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class FixedLengthReadStream(long length, Action<int> onRead) : Stream
    {
        private long position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
            => ReadCore(buffer.AsSpan(offset, count));

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ReadCore(buffer.Span));
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        private int ReadCore(Span<byte> buffer)
        {
            int count = (int)Math.Min(buffer.Length, length - position);
            if (count == 0)
            {
                return 0;
            }

            buffer[..count].Fill((byte)'A');
            position += count;
            onRead(count);
            return count;
        }
    }

    private sealed class RecordingSaveTarget : IFileSaveTarget
    {
        public int CallCount { get; private set; }

        public FileSaveRequest? LastRequest { get; private set; }

        public string LastContent { get; private set; } = string.Empty;

        public async ValueTask<FileSaveTargetResult> SaveAsync(
            FileSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            await using Stream stream = await request.Content.OpenReadAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            LastContent = await reader.ReadToEndAsync(cancellationToken);
            return new FileSaveTargetResult(new FileContentRevision("revision-2"));
        }
    }

    private sealed class NoopSessionReleaser : IFileToolsKnownFileSessionReleaser
    {
        public ValueTask ReleaseAsync(
            FileReference file,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
