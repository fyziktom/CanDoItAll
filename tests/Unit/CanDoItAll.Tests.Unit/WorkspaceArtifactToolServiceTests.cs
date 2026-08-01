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
            Assert.Null(converter.ObservedMaxCharacters);
            Assert.Equal(1, converter.CallCount);
            Assert.Equal(
                converter.Markdown,
                await File.ReadAllTextAsync(Path.Combine(root, "artifacts", "converted-documents", "quote.md")));
            Assert.NotEmpty(result.Receipt.ReceiptRelativePath);
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
            Assert.Equal(1, converter.CallCount);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConvertDocumentToMarkdown_overwrites_existing_output_with_full_markdown()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var documentPath = Path.Combine(root, "source", "quote.html");
            var outputPath = Path.Combine(root, "artifacts", "converted-documents", "quote.md");
            Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(documentPath, "<h1>Quote</h1>");
            await File.WriteAllTextAsync(outputPath, "old markdown");
            var converter = new FakeDocumentMarkdownConverter
            {
                Markdown = "# Complete replacement\n\nAll converted content."
            };
            var service = new WorkspaceArtifactToolService(
                root,
                new WorkspaceCommandExecutionService(root, new LocalWorkspaceProcessHost()),
                converter);

            var result = await service.ConvertDocumentToMarkdown(
                "source/quote.html",
                "artifacts/converted-documents/quote.md",
                previewCharacters: 10);

            Assert.True(result.Succeeded, result.Diagnostics);
            Assert.Equal(converter.Markdown, await File.ReadAllTextAsync(outputPath));
            Assert.Equal(converter.Markdown[..10], result.MarkdownPreview);
            Assert.True(result.PreviewTruncated);
            Assert.True(result.Receipt.MutatesWorkspace);
            Assert.DoesNotContain(
                Directory.EnumerateFiles(Path.GetDirectoryName(outputPath)!),
                path => path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConvertDocumentToMarkdown_output_write_failure_preserves_existing_path_and_has_no_mutation_receipt()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var documentPath = Path.Combine(root, "source", "quote.html");
            var blockingPath = Path.Combine(root, "artifacts", "converted-documents");
            Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(blockingPath)!);
            await File.WriteAllTextAsync(documentPath, "<h1>Quote</h1>");
            await File.WriteAllTextAsync(blockingPath, "existing workspace content");
            var converter = new FakeDocumentMarkdownConverter
            {
                Markdown = "# Converted markdown"
            };
            var service = new WorkspaceArtifactToolService(
                root,
                new WorkspaceCommandExecutionService(root, new LocalWorkspaceProcessHost()),
                converter);

            var result = await service.ConvertDocumentToMarkdown(
                "source/quote.html",
                "artifacts/converted-documents/quote.md");

            Assert.False(result.Succeeded);
            Assert.Contains("could not be written", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("existing workspace content", await File.ReadAllTextAsync(blockingPath));
            Assert.False(result.Receipt.MutatesWorkspace);
            Assert.Equal("Failed", result.Receipt.Outcome);
            Assert.Empty(result.Receipt.ReceiptRelativePath);
            Assert.Empty(result.Receipt.ArtifactReferences);
            Assert.Empty(result.MarkdownPreview);
            Assert.False(result.PreviewTruncated);
            Assert.Equal(1, converter.CallCount);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConvertDocumentToMarkdown_propagates_cancellation_to_converter_and_does_not_write_output()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var documentPath = Path.Combine(root, "source", "quote.html");
            var outputPath = Path.Combine(root, "artifacts", "converted-documents", "quote.md");
            Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
            await File.WriteAllTextAsync(documentPath, "<h1>Quote</h1>");
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var converter = new FakeDocumentMarkdownConverter
            {
                Handler = async (_, cancellationToken) =>
                {
                    entered.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    throw new InvalidOperationException("Unreachable after cancellation.");
                }
            };
            var service = new WorkspaceArtifactToolService(
                root,
                new WorkspaceCommandExecutionService(root, new LocalWorkspaceProcessHost()),
                converter);
            using var cancellationSource = new CancellationTokenSource();

            var conversion = service.ConvertDocumentToMarkdown(
                "source/quote.html",
                "artifacts/converted-documents/quote.md",
                cancellationToken: cancellationSource.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellationSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => conversion);
            Assert.Equal(cancellationSource.Token, converter.ObservedCancellationToken);
            Assert.False(File.Exists(outputPath));
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

        public int? ObservedMaxCharacters { get; private set; }

        public int CallCount { get; private set; }

        public CancellationToken ObservedCancellationToken { get; private set; }

        public Func<WorkspaceDocumentMarkdownConversionRequest, CancellationToken, Task<WorkspaceDocumentMarkdownConversionResult>>? Handler { get; init; }

        public Task<WorkspaceDocumentMarkdownConversionResult> ConvertToMarkdownAsync(
            WorkspaceDocumentMarkdownConversionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            ObservedSourcePath = request.SourcePath;
            ObservedMaxCharacters = request.MaxCharacters;
            ObservedCancellationToken = cancellationToken;

            if (Handler is not null)
            {
                return Handler(request, cancellationToken);
            }

            return Task.FromResult(new WorkspaceDocumentMarkdownConversionResult(
                Succeeded,
                Message,
                request.SourcePath,
                Succeeded ? Markdown : string.Empty,
                Succeeded ? Markdown.Length : 0,
                IsTruncated: false,
                Diagnostics));
        }
    }
}
