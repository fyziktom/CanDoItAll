using System.IO.Compression;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.Tools.Documents;

namespace CanDoItAll.Tests.Unit;

public sealed class ManagedCodeMarkItDownDocumentMarkdownConverterTests
{
    [Fact]
    public async Task ConvertToMarkdownAsync_returns_markdown_content_without_creating_output()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var sourcePath = Path.Combine(root, "quote.html");
            await File.WriteAllTextAsync(
                sourcePath,
                "<html><body><h1>Quote</h1><p>Model XR-1 costs 12500 USD.</p></body></html>");
            var converter = new ManagedCodeMarkItDownDocumentMarkdownConverter();

            var result = await converter.ConvertToMarkdownAsync(
                new WorkspaceDocumentMarkdownConversionRequest(sourcePath));

            Assert.True(result.Succeeded, result.Diagnostics);
            Assert.Equal(Path.GetFullPath(sourcePath), result.SourcePath);
            Assert.Contains("Quote", result.Markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Model XR-1", result.Markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(result.Markdown.Length, result.TotalMarkdownCharacters);
            Assert.False(result.IsTruncated);
            Assert.Equal([sourcePath], Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConvertToMarkdownAsync_bounds_returned_markdown_and_reports_full_length()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var sourcePath = Path.Combine(root, "quote.html");
            await File.WriteAllTextAsync(
                sourcePath,
                "<html><body><h1>Quote</h1><p>Model XR-1 costs 12500 USD.</p></body></html>");
            var converter = new ManagedCodeMarkItDownDocumentMarkdownConverter();
            var fullResult = await converter.ConvertToMarkdownAsync(
                new WorkspaceDocumentMarkdownConversionRequest(sourcePath));
            const int limit = 12;

            var boundedResult = await converter.ConvertToMarkdownAsync(
                new WorkspaceDocumentMarkdownConversionRequest(sourcePath, limit));

            Assert.True(fullResult.Succeeded, fullResult.Diagnostics);
            Assert.True(boundedResult.Succeeded, boundedResult.Diagnostics);
            Assert.True(fullResult.Markdown.Length > limit);
            Assert.Equal(fullResult.Markdown[..limit], boundedResult.Markdown);
            Assert.Equal(fullResult.TotalMarkdownCharacters, boundedResult.TotalMarkdownCharacters);
            Assert.True(boundedResult.IsTruncated);
            Assert.Equal([sourcePath], Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConvertToMarkdownAsync_missing_source_returns_explicit_failure()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var sourcePath = Path.Combine(root, "missing.pdf");
            var converter = new ManagedCodeMarkItDownDocumentMarkdownConverter();

            var result = await converter.ConvertToMarkdownAsync(
                new WorkspaceDocumentMarkdownConversionRequest(sourcePath));

            Assert.False(result.Succeeded);
            Assert.Equal(Path.GetFullPath(sourcePath), result.SourcePath);
            Assert.Contains("was not found", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(sourcePath, result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(root, result.Diagnostics, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(result.Markdown);
            Assert.Equal(0, result.TotalMarkdownCharacters);
            Assert.False(result.IsTruncated);
            Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConvertToMarkdownAsync_does_not_turn_unexpected_path_failure_into_model_visible_result()
    {
        const string sourcePath = "C:\\private\\document-converter-sentinel\0.pdf";
        var converter = new ManagedCodeMarkItDownDocumentMarkdownConverter();

        var exception = await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            converter.ConvertToMarkdownAsync(
                new WorkspaceDocumentMarkdownConversionRequest(sourcePath)));

        Assert.False(MafAgentToolFailureMapper.TryMap(exception, out _));
    }

    [Fact]
    public async Task ConvertToMarkdownAsync_rejects_negative_character_limit()
    {
        var converter = new ManagedCodeMarkItDownDocumentMarkdownConverter();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => converter.ConvertToMarkdownAsync(
            new WorkspaceDocumentMarkdownConversionRequest("document.html", MaxCharacters: -1)));
    }

    [Fact]
    public async Task ConvertToMarkdownAsync_propagates_cancellation_without_creating_output()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var sourcePath = Path.Combine(root, "quote.html");
            await File.WriteAllTextAsync(sourcePath, "<h1>Quote</h1>");
            var converter = new ManagedCodeMarkItDownDocumentMarkdownConverter();
            using var cancellationSource = new CancellationTokenSource();
            await cancellationSource.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => converter.ConvertToMarkdownAsync(
                new WorkspaceDocumentMarkdownConversionRequest(sourcePath),
                cancellationSource.Token));

            Assert.Equal([sourcePath], Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConvertToMarkdownAsync_extracts_recognizable_pdf_content()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var sourcePath = Path.Combine(root, "quarterly.pdf");
            await WriteMinimalPdfAsync(sourcePath, "Quarterly PDF marker 4242 USD");

            var result = await new ManagedCodeMarkItDownDocumentMarkdownConverter()
                .ConvertToMarkdownAsync(new WorkspaceDocumentMarkdownConversionRequest(sourcePath, 4096));

            Assert.True(result.Succeeded, result.Diagnostics);
            Assert.Contains("Quarterly", result.Markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("4242", result.Markdown, StringComparison.OrdinalIgnoreCase);
            Assert.False(result.IsTruncated);
            Assert.InRange(new FileInfo(sourcePath).Length, 1, 8192);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConvertToMarkdownAsync_extracts_recognizable_docx_content()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var sourcePath = Path.Combine(root, "quarterly.docx");
            await WriteMinimalDocxAsync(sourcePath, "Quarterly DOCX marker 7319 USD");

            var result = await new ManagedCodeMarkItDownDocumentMarkdownConverter()
                .ConvertToMarkdownAsync(new WorkspaceDocumentMarkdownConversionRequest(sourcePath, 4096));

            Assert.True(result.Succeeded, result.Diagnostics);
            Assert.Contains("Quarterly DOCX marker", result.Markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("7319", result.Markdown, StringComparison.OrdinalIgnoreCase);
            Assert.False(result.IsTruncated);
            Assert.InRange(new FileInfo(sourcePath).Length, 1, 16384);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConvertToMarkdownAsync_extracts_recognizable_xlsx_content()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var sourcePath = Path.Combine(root, "quarterly.xlsx");
            var spreadsheetService = new ClosedXmlSpreadsheetDocumentService();
            spreadsheetService.Write(new SpreadsheetWriteRequest(
                sourcePath,
                sourcePath,
                "Evidence",
                [
                    new SpreadsheetCellWrite("A1", "Quarterly XLSX marker"),
                    new SpreadsheetCellWrite("B1", "9851 USD")
                ],
                [],
                CreateWorkbookIfMissing: true,
                Overwrite: true));

            var result = await new ManagedCodeMarkItDownDocumentMarkdownConverter()
                .ConvertToMarkdownAsync(new WorkspaceDocumentMarkdownConversionRequest(sourcePath, 4096));

            Assert.True(result.Succeeded, result.Diagnostics);
            Assert.Contains("Quarterly XLSX marker", result.Markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("9851", result.Markdown, StringComparison.OrdinalIgnoreCase);
            Assert.False(result.IsTruncated);
            Assert.InRange(new FileInfo(sourcePath).Length, 1, 65536);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static async Task WriteMinimalPdfAsync(string path, string text)
    {
        var escapedText = text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
        var content = $"BT /F1 18 Tf 72 720 Td ({escapedText}) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream"
        ];
        await using var stream = new MemoryStream();
        await WriteAsciiAsync(stream, "%PDF-1.4\n");
        var offsets = new long[objects.Length + 1];
        for (var index = 0; index < objects.Length; index++)
        {
            offsets[index + 1] = stream.Position;
            await WriteAsciiAsync(stream, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = stream.Position;
        await WriteAsciiAsync(stream, $"xref\n0 {objects.Length + 1}\n");
        await WriteAsciiAsync(stream, "0000000000 65535 f \n");
        for (var index = 1; index < offsets.Length; index++)
        {
            await WriteAsciiAsync(stream, $"{offsets[index]:D10} 00000 n \n");
        }

        await WriteAsciiAsync(
            stream,
            $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        await File.WriteAllBytesAsync(path, stream.ToArray());
    }

    private static async Task WriteMinimalDocxAsync(string path, string text)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        await WriteZipEntryAsync(
            archive,
            "[Content_Types].xml",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
              <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml" />
            </Types>
            """);
        await WriteZipEntryAsync(
            archive,
            "_rels/.rels",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml" />
            </Relationships>
            """);
        await WriteZipEntryAsync(
            archive,
            "word/document.xml",
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:body><w:p><w:r><w:t>{text}</w:t></w:r></w:p><w:sectPr /></w:body>
            </w:document>
            """);
    }

    private static async Task WriteZipEntryAsync(
        ZipArchive archive,
        string entryName,
        string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.WriteAsync(content);
    }

    private static async Task WriteAsciiAsync(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        await stream.WriteAsync(bytes);
    }

    private static string CreateWorkspaceRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "candoitall-managedcode-markitdown-tests",
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
}
