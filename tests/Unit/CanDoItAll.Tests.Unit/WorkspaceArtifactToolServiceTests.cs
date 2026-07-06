using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceArtifactToolServiceTests
{
    [Fact]
    public async Task ConvertDocumentToMarkdown_rejects_image_assets_with_bounded_guidance()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var imagePath = Path.Combine(root, "managed-files", "project-media", "images", "proposal.png");
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
            await File.WriteAllBytesAsync(imagePath, MinimalPngBytes());

            var service = new WorkspaceArtifactToolService(
                root,
                new WorkspaceCommandExecutionService(root, new LocalWorkspaceProcessHost()),
                new FakeDocumentMarkdownConverter());

            var result = await service.ConvertDocumentToMarkdown(
                "managed-files/project-media/images/proposal.png",
                previewCharacters: 1200);

            Assert.False(result.Succeeded);
            Assert.Empty(result.MarkdownPreview);
            Assert.False(result.PreviewTruncated);
            Assert.Equal("workspace_convert_document", result.Receipt.Operation);
            Assert.Contains("image asset", result.Message, StringComparison.Ordinal);
            Assert.Contains("workspace_inspect_image", result.Message, StringComparison.Ordinal);
            Assert.Contains("workspace_analyze_image", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ReadImageFile_uses_supplied_operation_name_for_receipt()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var imagePath = Path.Combine(root, "artifacts", "images", "frame.png");
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
            await File.WriteAllBytesAsync(imagePath, MinimalPngBytes());

            var service = new WorkspaceArtifactToolService(
                root,
                new WorkspaceCommandExecutionService(root, new LocalWorkspaceProcessHost()),
                new FakeDocumentMarkdownConverter());

            var result = await service.ReadImageFile(
                "artifacts/images/frame.png",
                operationName: "workspace_analyze_images");

            Assert.True(result.Succeeded, result.Diagnostics);
            Assert.Equal("workspace_analyze_images", result.Receipt.Operation);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConvertDocumentToMarkdown_uses_document_converter_and_returns_preview()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var documentPath = Path.Combine(root, "source", "quote.html");
            Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
            await File.WriteAllTextAsync(documentPath, "<h1>Quote</h1>");
            var converter = new FakeDocumentMarkdownConverter
            {
                Markdown = "## Quote Summary\n\nModel XR-1 costs 12500 USD."
            };
            var service = new WorkspaceArtifactToolService(
                root,
                new WorkspaceCommandExecutionService(root, new LocalWorkspaceProcessHost()),
                converter);

            var result = await service.ConvertDocumentToMarkdown(
                "source/quote.html",
                "artifacts/converted-documents/quote.md",
                previewCharacters: 16);

            Assert.True(result.Succeeded, result.Diagnostics);
            Assert.Equal("source/quote.html", result.SourcePath);
            Assert.Equal("artifacts/converted-documents/quote.md", result.OutputPath);
            Assert.Equal("## Quote Summary", result.MarkdownPreview);
            Assert.True(result.PreviewTruncated);
            Assert.True(result.Receipt.MutatesWorkspace);
            Assert.Equal("workspace_convert_document", result.Receipt.Operation);
            Assert.Contains(result.Receipt.ArtifactReferences, item => item.RelativePath == result.OutputPath);
            Assert.Equal(Path.GetFullPath(documentPath), converter.ObservedSourcePath);
            Assert.Equal(Path.Combine(root, "artifacts", "converted-documents", "quote.md"), converter.ObservedOutputPath);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConvertDocumentToMarkdown_uses_bounded_receipt_file_name_for_long_media_paths()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var longFileName = $"{new string('x', 96)}.pdf";
            var relativeSourcePath = $"managed-files/project-media/files/f28c07cd982c4d2dbcf23e60a32eca72/{longFileName}";
            var documentPath = Path.Combine(root, relativeSourcePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
            await File.WriteAllTextAsync(documentPath, "%PDF");
            var converter = new FakeDocumentMarkdownConverter
            {
                Markdown = "Model ZM-x5600 costs 35000 USD."
            };
            var service = new WorkspaceArtifactToolService(
                root,
                new WorkspaceCommandExecutionService(root, new LocalWorkspaceProcessHost()),
                converter,
                WorkspaceScopeDescriptor.Organization("e5df9ad633dbc6974a0678a74976013c"));

            var result = await service.ConvertDocumentToMarkdown(relativeSourcePath);

            Assert.True(result.Succeeded, result.Diagnostics);
            Assert.True(result.Receipt.MutatesWorkspace);
            Assert.NotEmpty(result.Receipt.ReceiptRelativePath);
            Assert.True(Path.GetFileName(result.Receipt.ReceiptRelativePath).Length <= 120);
            var receiptFullPath = Path.Combine(root, result.Receipt.ReceiptRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(receiptFullPath), result.Receipt.ReceiptRelativePath);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConvertDocumentToMarkdown_returns_converter_failure_without_preview()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var documentPath = Path.Combine(root, "source", "quote.pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
            await File.WriteAllTextAsync(documentPath, "%PDF");
            var converter = new FakeDocumentMarkdownConverter
            {
                Succeeded = false,
                Message = "conversion failed",
                Diagnostics = "unsupported fixture"
            };
            var service = new WorkspaceArtifactToolService(
                root,
                new WorkspaceCommandExecutionService(root, new LocalWorkspaceProcessHost()),
                converter);

            var result = await service.ConvertDocumentToMarkdown(
                "source/quote.pdf",
                "artifacts/converted-documents/quote.md");

            Assert.False(result.Succeeded);
            Assert.Equal("conversion failed", result.Message);
            Assert.Equal("unsupported fixture", result.Diagnostics);
            Assert.Empty(result.MarkdownPreview);
            Assert.False(result.PreviewTruncated);
            Assert.False(result.Receipt.MutatesWorkspace);
            Assert.Equal("Failed", result.Receipt.Outcome);
            Assert.False(File.Exists(Path.Combine(root, "artifacts", "converted-documents", "quote.md")));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateWorkspaceRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "candoitall-workspace-artifact-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static byte[] MinimalPngBytes()
        =>
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01,
            0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00,
            0x1F, 0x15, 0xC4, 0x89,
            0x00, 0x00, 0x00, 0x0A,
            0x49, 0x44, 0x41, 0x54,
            0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00, 0x05, 0x00, 0x01,
            0x0D, 0x0A, 0x2D, 0xB4,
            0x00, 0x00, 0x00, 0x00,
            0x49, 0x45, 0x4E, 0x44,
            0xAE, 0x42, 0x60, 0x82
        ];

    private sealed class FakeDocumentMarkdownConverter : IWorkspaceDocumentMarkdownConverter
    {
        public bool Succeeded { get; init; } = true;

        public string Message { get; init; } = "converted";

        public string Markdown { get; init; } = "# Converted";

        public string Diagnostics { get; init; } = string.Empty;

        public string? ObservedSourcePath { get; private set; }

        public string? ObservedOutputPath { get; private set; }

        public async Task<WorkspaceDocumentMarkdownConversionResult> ConvertToMarkdownAsync(
            WorkspaceDocumentMarkdownConversionRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedSourcePath = request.SourcePath;
            ObservedOutputPath = request.OutputPath;
            if (Succeeded)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.OutputPath)!);
                await File.WriteAllTextAsync(request.OutputPath, Markdown, cancellationToken);
            }

            return new WorkspaceDocumentMarkdownConversionResult(
                Succeeded,
                Message,
                request.SourcePath,
                request.OutputPath,
                Succeeded ? Markdown.Length : 0,
                Diagnostics);
        }
    }
}
