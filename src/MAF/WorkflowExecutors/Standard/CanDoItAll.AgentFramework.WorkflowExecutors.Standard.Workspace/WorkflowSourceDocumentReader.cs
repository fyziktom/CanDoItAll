using System.IO.Compression;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using ExcelDataReader;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

internal sealed class WorkflowSourceDocumentReader(
    IWorkspaceDocumentMarkdownConverter documentMarkdownConverter)
{
    private static readonly HashSet<string> MarkdownConversionExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".docx",
        ".html",
        ".htm",
        ".xlsx"
    };

    static WorkflowSourceDocumentReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<WorkflowSourceReadResult> ReadAsync(
        WorkflowSourceIngestionFile file,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        cancellationToken.ThrowIfCancellationRequested();
        var effectiveMaxCharacters = Math.Max(0, maxCharacters);
        var extension = Path.GetExtension(file.FullPath).ToLowerInvariant();
        if (MarkdownConversionExtensions.Contains(extension))
        {
            return await ReadConvertedDocumentAsync(
                    file,
                    extension,
                    effectiveMaxCharacters,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return extension switch
        {
            ".zip" => ReadZipManifest(file.FullPath, effectiveMaxCharacters, cancellationToken),
            ".xls" => ReadLegacyXls(file.FullPath, effectiveMaxCharacters, cancellationToken),
            _ => ReadText(file.FullPath, effectiveMaxCharacters, cancellationToken)
        };
    }

    private async Task<WorkflowSourceReadResult> ReadConvertedDocumentAsync(
        WorkflowSourceIngestionFile file,
        string extension,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        var conversion = await documentMarkdownConverter
            .ConvertToMarkdownAsync(
                new WorkspaceDocumentMarkdownConversionRequest(file.FullPath, maxCharacters),
                cancellationToken)
            .ConfigureAwait(false);
        if (!conversion.Succeeded)
        {
            var detail = string.IsNullOrWhiteSpace(conversion.Message)
                ? conversion.Diagnostics
                : conversion.Message;
            throw new InvalidOperationException(
                $"Document conversion failed for source '{file.DisplayPath}'. {detail}".Trim());
        }

        ValidateConversionContract(conversion, file.DisplayPath, maxCharacters);
        return new WorkflowSourceReadResult(
            conversion.Markdown,
            conversion.TotalMarkdownCharacters,
            conversion.IsTruncated,
            ResolveConversionStatus(extension));
    }

    private static void ValidateConversionContract(
        WorkspaceDocumentMarkdownConversionResult conversion,
        string displayPath,
        int maxCharacters)
    {
        if (conversion.Markdown is null)
        {
            throw CreateContractViolation(displayPath, "Markdown is null.");
        }

        if (conversion.Markdown.Length > maxCharacters)
        {
            throw CreateContractViolation(
                displayPath,
                $"Markdown length {conversion.Markdown.Length} exceeds the requested maximum of {maxCharacters} characters.");
        }

        if (conversion.TotalMarkdownCharacters < conversion.Markdown.Length)
        {
            throw CreateContractViolation(
                displayPath,
                $"TotalMarkdownCharacters {conversion.TotalMarkdownCharacters} is less than the returned Markdown length {conversion.Markdown.Length}.");
        }

        var contentWasTruncated = conversion.TotalMarkdownCharacters > conversion.Markdown.Length;
        if (conversion.IsTruncated != contentWasTruncated)
        {
            throw CreateContractViolation(
                displayPath,
                $"IsTruncated is {conversion.IsTruncated} while the returned and total character counts indicate truncation is {contentWasTruncated}.");
        }
    }

    private static InvalidOperationException CreateContractViolation(
        string displayPath,
        string detail)
        => new($"Document converter violated its result contract for source '{displayPath}'. {detail}");

    private static string ResolveConversionStatus(string extension)
        => extension switch
        {
            ".pdf" => "markitdown-pdf",
            ".docx" => "markitdown-docx",
            ".html" or ".htm" => "markitdown-html",
            ".xlsx" => "markitdown-xlsx",
            _ => "markitdown"
        };

    private static WorkflowSourceReadResult ReadText(
        string fullPath,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(fullPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return ReadBounded(reader, maxCharacters, "text", cancellationToken);
    }

    private static WorkflowSourceReadResult ReadZipManifest(
        string fullPath,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(fullPath);
        var builder = new StringBuilder(Math.Min(maxCharacters, 8192));
        var totalCharacters = 0;
        var truncated = false;
        foreach (var entry in archive.Entries
                     .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
                     .Take(200))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = $"{entry.FullName}\t{entry.Length} bytes";
            totalCharacters += line.Length;
            AppendBounded(builder, line + Environment.NewLine, maxCharacters, ref truncated);
            if (truncated)
            {
                break;
            }
        }

        return new WorkflowSourceReadResult(
            builder.ToString().Trim(),
            totalCharacters,
            truncated || archive.Entries.Count > 200,
            "zip-manifest");
    }

    private static WorkflowSourceReadResult ReadLegacyXls(
        string fullPath,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        try
        {
            using var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var builder = new StringBuilder(Math.Min(maxCharacters, 8192));
            var totalCharacters = 0;
            var truncated = false;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var worksheetName = string.IsNullOrWhiteSpace(reader.Name) ? "Sheet" : reader.Name;
                AppendBounded(builder, $"## Worksheet: {worksheetName}{Environment.NewLine}", maxCharacters, ref truncated);
                var rowIndex = 0;
                while (!truncated && reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    rowIndex++;
                    if (rowIndex > 80)
                    {
                        truncated = true;
                        break;
                    }

                    var cells = new List<string>();
                    var fieldCount = Math.Min(reader.FieldCount, 20);
                    for (var index = 0; index < fieldCount; index++)
                    {
                        var value = reader.GetValue(index)?.ToString()?.Trim() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(value) || cells.Count > 0)
                        {
                            cells.Add(value);
                        }
                    }

                    if (cells.Count == 0)
                    {
                        continue;
                    }

                    var line = string.Join(" | ", cells);
                    totalCharacters += line.Length;
                    AppendBounded(builder, line + Environment.NewLine, maxCharacters, ref truncated);
                }
            }
            while (!truncated && reader.NextResult());

            return new WorkflowSourceReadResult(
                builder.ToString().Trim(),
                totalCharacters,
                truncated,
                "legacy-xls-text");
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not IOException and not UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Legacy XLS extraction failed for '{Path.GetFileName(fullPath)}'. {exception.Message}",
                exception);
        }
    }

    private static WorkflowSourceReadResult ReadBounded(
        TextReader reader,
        int maxCharacters,
        string extractionStatus,
        CancellationToken cancellationToken)
    {
        var buffer = new char[Math.Min(Math.Max(maxCharacters, 1), 8192)];
        var builder = new StringBuilder(Math.Min(maxCharacters, 8192));
        var totalCharacters = 0;
        var truncated = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = reader.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            totalCharacters += read;
            AppendBounded(builder, new string(buffer, 0, read), maxCharacters, ref truncated);
            if (truncated)
            {
                break;
            }
        }

        return new WorkflowSourceReadResult(builder.ToString(), totalCharacters, truncated, extractionStatus);
    }

    private static void AppendBounded(
        StringBuilder builder,
        string value,
        int maxCharacters,
        ref bool truncated)
    {
        if (maxCharacters <= 0 || builder.Length >= maxCharacters)
        {
            truncated = true;
            return;
        }

        var remaining = maxCharacters - builder.Length;
        if (value.Length <= remaining)
        {
            builder.Append(value);
            return;
        }

        builder.Append(value.AsSpan(0, remaining));
        truncated = true;
    }
}
