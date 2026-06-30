using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Xml.Linq;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using ExcelDataReader;
using UglyToad.PdfPig;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

public sealed partial class SourceIngestionWorkflowExecutor
{
    private IEnumerable<WorkflowSourceIngestionFile> ResolveCandidateFiles(
        WorkflowSourceCandidate candidate,
        WorkflowSourceIngestionExecutorSettings settings,
        IReadOnlySet<string> allowedExtensions,
        int take)
    {
        if (take <= 0)
        {
            yield break;
        }

        var kind = candidate.Kind;
        var resolvedAsDirectory = string.Equals(kind, "folderPath", StringComparison.OrdinalIgnoreCase) ||
                                  (!string.Equals(kind, "filePath", StringComparison.OrdinalIgnoreCase) && Directory.Exists(ResolvePathForProbe(candidate.Value, settings)));

        if (resolvedAsDirectory)
        {
            var directory = ResolveDirectory(candidate.Value, settings);
            var count = 0;
            foreach (var file in Directory.EnumerateFiles(
                         directory.FullPath,
                         "*",
                         new EnumerationOptions
                         {
                             RecurseSubdirectories = settings.RecursiveFolders,
                             IgnoreInaccessible = true,
                             AttributesToSkip = 0
                         })
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (!IsAllowedExtension(file, allowedExtensions))
                {
                    continue;
                }

                yield return new WorkflowSourceIngestionFile(
                    file,
                    ToDisplayPath(file, directory),
                    Path.GetFileName(file));
                count++;
                if (count >= take)
                {
                    yield break;
                }
            }

            yield break;
        }

        var resolvedFile = ResolveFile(candidate.Value, settings);
        if (!IsAllowedExtension(resolvedFile.FullPath, allowedExtensions))
        {
            throw new InvalidOperationException($"Source file '{resolvedFile.RelativePath}' has extension '{Path.GetExtension(resolvedFile.FullPath)}', which is not allowed by this workflow source-ingestion node.");
        }

        yield return new WorkflowSourceIngestionFile(
            resolvedFile.FullPath,
            resolvedFile.RelativePath,
            Path.GetFileName(resolvedFile.FullPath));
    }

    private WorkflowSourceIngestionDocument ReadSourceDocument(
        WorkflowSourceCandidate candidate,
        WorkflowSourceIngestionFile file,
        int maxCharactersPerFile,
        int remainingCharacters)
    {
        var maxCharacters = Math.Max(0, Math.Min(maxCharactersPerFile, remainingCharacters));
        var extension = Path.GetExtension(file.FullPath).ToLowerInvariant();
        var result = extension switch
        {
            ".pdf" => ReadPdf(file.FullPath, maxCharacters),
            ".docx" => ReadDocx(file.FullPath, maxCharacters),
            ".html" or ".htm" => ReadHtml(file.FullPath, maxCharacters),
            ".zip" => ReadZipManifest(file.FullPath, maxCharacters),
            ".xls" or ".xlsx" => ReadWorkbook(file.FullPath, maxCharacters),
            _ => ReadText(file.FullPath, maxCharacters)
        };

        return new WorkflowSourceIngestionDocument(
            candidate.Key,
            candidate.Label,
            candidate.Kind,
            candidate.Origin,
            file.DisplayPath,
            file.FileName,
            extension,
            result.Text,
            result.TotalCharacters,
            result.IsTruncated,
            result.ExtractionStatus);
    }

    private static WorkflowSourceReadResult ReadText(string fullPath, int maxCharacters)
    {
        using var reader = new StreamReader(fullPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return ReadBounded(reader, maxCharacters, "text");
    }

    private static WorkflowSourceReadResult ReadPdf(string fullPath, int maxCharacters)
    {
        var builder = new StringBuilder(Math.Min(maxCharacters, 8192));
        var totalCharacters = 0;
        var isTruncated = false;

        using var pdf = PdfDocument.Open(fullPath);
        foreach (var page in pdf.GetPages())
        {
            var pageText = page.Text ?? string.Empty;
            totalCharacters += pageText.Length;
            AppendBounded(builder, $"# Page {page.Number}{Environment.NewLine}{pageText}{Environment.NewLine}", maxCharacters, ref isTruncated);
            if (isTruncated)
            {
                break;
            }
        }

        var text = builder.ToString().Trim();
        return new WorkflowSourceReadResult(
            text,
            totalCharacters,
            isTruncated,
            string.IsNullOrWhiteSpace(text)
                ? $"pdf-pages-{pdf.NumberOfPages}-no-extractable-text"
                : $"pdf-pages-{pdf.NumberOfPages}-text");
    }

    private static WorkflowSourceReadResult ReadDocx(string fullPath, int maxCharacters)
    {
        using var archive = ZipFile.OpenRead(fullPath);
        var documentEntry = archive.GetEntry("word/document.xml");
        if (documentEntry is null)
        {
            return new WorkflowSourceReadResult(string.Empty, 0, false, "docx-missing-document-xml");
        }

        using var stream = documentEntry.Open();
        var document = XDocument.Load(stream);
        var text = string.Join(
            Environment.NewLine,
            document.Descendants()
                .Where(element => element.Name.LocalName == "t")
                .Select(element => element.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        var truncated = text.Length > maxCharacters;
        return new WorkflowSourceReadResult(
            truncated ? text[..maxCharacters] : text,
            text.Length,
            truncated,
            "docx-text");
    }

    private static WorkflowSourceReadResult ReadHtml(string fullPath, int maxCharacters)
    {
        var html = File.ReadAllText(fullPath);
        var text = Regex.Replace(html, "<script[\\s\\S]*?</script>|<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        text = Regex.Replace(text, "<[^>]+>", " ", RegexOptions.CultureInvariant);
        text = Regex.Replace(text, "\\s+", " ", RegexOptions.CultureInvariant).Trim();
        var truncated = text.Length > maxCharacters;
        return new WorkflowSourceReadResult(
            truncated ? text[..maxCharacters] : text,
            text.Length,
            truncated,
            "html-text");
    }

    private static WorkflowSourceReadResult ReadZipManifest(string fullPath, int maxCharacters)
    {
        using var archive = ZipFile.OpenRead(fullPath);
        var builder = new StringBuilder(Math.Min(maxCharacters, 8192));
        var totalCharacters = 0;
        var truncated = false;
        foreach (var entry in archive.Entries.OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase).Take(200))
        {
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

    private static WorkflowSourceReadResult ReadWorkbook(string fullPath, int maxCharacters)
    {
        using var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var builder = new StringBuilder(Math.Min(maxCharacters, 8192));
        var totalCharacters = 0;
        var isTruncated = false;

        do
        {
            var worksheetName = string.IsNullOrWhiteSpace(reader.Name) ? "Sheet" : reader.Name;
            AppendBounded(builder, $"## Worksheet: {worksheetName}{Environment.NewLine}", maxCharacters, ref isTruncated);
            var rowIndex = 0;
            while (reader.Read())
            {
                rowIndex++;
                if (rowIndex > 80)
                {
                    isTruncated = true;
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
                AppendBounded(builder, line + Environment.NewLine, maxCharacters, ref isTruncated);
                if (isTruncated)
                {
                    break;
                }
            }
        }
        while (!isTruncated && reader.NextResult());

        return new WorkflowSourceReadResult(builder.ToString().Trim(), totalCharacters, isTruncated, "workbook-text");
    }

    private static WorkflowSourceReadResult ReadBounded(TextReader reader, int maxCharacters, string extractionStatus)
    {
        var buffer = new char[Math.Min(Math.Max(maxCharacters, 1), 8192)];
        var builder = new StringBuilder(Math.Min(maxCharacters, 8192));
        var totalCharacters = 0;
        var isTruncated = false;

        while (true)
        {
            var read = reader.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            totalCharacters += read;
            AppendBounded(builder, new string(buffer, 0, read), maxCharacters, ref isTruncated);
            if (isTruncated)
            {
                break;
            }
        }

        return new WorkflowSourceReadResult(builder.ToString(), totalCharacters, isTruncated, extractionStatus);
    }

    private static void AppendBounded(StringBuilder builder, string value, int maxCharacters, ref bool isTruncated)
    {
        if (maxCharacters <= 0 || builder.Length >= maxCharacters)
        {
            isTruncated = true;
            return;
        }

        var remaining = maxCharacters - builder.Length;
        if (value.Length <= remaining)
        {
            builder.Append(value);
            return;
        }

        builder.Append(value.AsSpan(0, remaining));
        isTruncated = true;
    }

}
