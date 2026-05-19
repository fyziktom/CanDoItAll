using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace CanDoItAll.Modules.CognitiveMemory;

internal static class CognitiveMemoryExternalSourceTextExtractor
{
    private static readonly XNamespace WordprocessingNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace DrawingNamespace = "http://schemas.openxmlformats.org/drawingml/2006/main";

    public static async Task<string> ExtractAsync(
        string fileName,
        string contentType,
        Stream stream,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".docx" => await ExtractOpenXmlAsync(stream, maxCharacters, ExtractDocxText, cancellationToken),
            ".pptx" => await ExtractOpenXmlAsync(stream, maxCharacters, ExtractPptxText, cancellationToken),
            ".xlsx" => await ExtractOpenXmlAsync(stream, maxCharacters, ExtractXlsxText, cancellationToken),
            ".pdf" => await ExtractPdfAsync(stream, maxCharacters, cancellationToken),
            _ => await ReadTextAsync(stream, maxCharacters, cancellationToken)
        };
    }

    private static async Task<string> ExtractOpenXmlAsync(
        Stream stream,
        int maxCharacters,
        Func<ZipArchive, int, string> extractor,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: false);
        return EnsureExtractedText(extractor(archive, maxCharacters));
    }

    private static async Task<string> ExtractPdfAsync(
        Stream stream,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        var builder = new BoundedTextBuilder(maxCharacters);
        using var document = PdfDocument.Open(buffer);
        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.AppendLine($"## Page {page.Number.ToString(CultureInfo.InvariantCulture)}");
            builder.AppendLine(page.Text ?? string.Empty);
        }

        return EnsureExtractedText(builder.ToString());
    }

    private static async Task<string> ReadTextAsync(Stream stream, int maxCharacters, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var buffer = new char[8192];
        var builder = new BoundedTextBuilder(maxCharacters);
        while (true)
        {
            var read = await reader.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            builder.Append(buffer.AsSpan(0, read));
        }

        return EnsureExtractedText(builder.ToString());
    }

    private static string ExtractDocxText(ZipArchive archive, int maxCharacters)
    {
        var documentEntry = archive.GetEntry("word/document.xml")
            ?? throw new InvalidOperationException("DOCX file does not contain a word/document.xml part.");
        using var stream = documentEntry.Open();
        var document = XDocument.Load(stream);
        var builder = new BoundedTextBuilder(maxCharacters);
        foreach (var paragraph in document.Descendants(WordprocessingNamespace + "p"))
        {
            var text = string.Join(
                string.Empty,
                paragraph
                    .Descendants(WordprocessingNamespace + "t")
                    .Select(node => node.Value));
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine(text.Trim());
            }
        }

        return builder.ToString();
    }

    private static string ExtractPptxText(ZipArchive archive, int maxCharacters)
    {
        var builder = new BoundedTextBuilder(maxCharacters);
        var slideEntries = archive.Entries
            .Where(entry => entry.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) &&
                            entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => ResolveSlideNumber(entry.FullName))
            .ToArray();
        if (slideEntries.Length == 0)
        {
            throw new InvalidOperationException("PPTX file does not contain slide XML parts.");
        }

        foreach (var entry in slideEntries)
        {
            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            builder.AppendLine($"## Slide {ResolveSlideNumber(entry.FullName).ToString(CultureInfo.InvariantCulture)}");
            foreach (var text in document.Descendants(DrawingNamespace + "t").Select(node => node.Value.Trim()))
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    builder.AppendLine(text);
                }
            }
        }

        return builder.ToString();
    }

    private static string ExtractXlsxText(ZipArchive archive, int maxCharacters)
    {
        var sharedStrings = ReadSharedStrings(archive);
        var worksheetEntries = archive.Entries
            .Where(entry => entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                            entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => ResolveWorksheetNumber(entry.FullName))
            .ToArray();
        if (worksheetEntries.Length == 0)
        {
            throw new InvalidOperationException("XLSX file does not contain worksheet XML parts.");
        }

        var builder = new BoundedTextBuilder(maxCharacters);
        foreach (var entry in worksheetEntries)
        {
            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            builder.AppendLine($"## Worksheet {ResolveWorksheetNumber(entry.FullName).ToString(CultureInfo.InvariantCulture)}");
            foreach (var row in document.Descendants(SpreadsheetNamespace + "row"))
            {
                var values = row
                    .Elements(SpreadsheetNamespace + "c")
                    .Select(cell => ReadCellValue(cell, sharedStrings))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToArray();
                if (values.Length > 0)
                {
                    builder.AppendLine(string.Join(" | ", values));
                }
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document
            .Descendants(SpreadsheetNamespace + "si")
            .Select(item => string.Join(string.Empty, item.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = cell.Attribute("t")?.Value;
        if (string.Equals(type, "inlineStr", StringComparison.Ordinal))
        {
            return string.Join(string.Empty, cell.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)).Trim();
        }

        var value = cell.Element(SpreadsheetNamespace + "v")?.Value.Trim() ?? string.Empty;
        if (string.Equals(type, "s", StringComparison.Ordinal) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) &&
            index >= 0 &&
            index < sharedStrings.Count)
        {
            return sharedStrings[index].Trim();
        }

        return value;
    }

    private static int ResolveSlideNumber(string fullName)
        => ResolveTrailingNumber(Path.GetFileNameWithoutExtension(fullName), "slide");

    private static int ResolveWorksheetNumber(string fullName)
        => ResolveTrailingNumber(Path.GetFileNameWithoutExtension(fullName), "sheet");

    private static int ResolveTrailingNumber(string value, string prefix)
    {
        var text = value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..]
            : value;
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : int.MaxValue;
    }

    private static string EnsureExtractedText(string text)
        => string.IsNullOrWhiteSpace(text)
            ? throw new InvalidOperationException("External source text is empty.")
            : text.Trim();

    private sealed class BoundedTextBuilder(int maxCharacters)
    {
        private readonly StringBuilder builder = new(Math.Min(Math.Max(maxCharacters, 1), 8192));

        public void AppendLine(string value)
        {
            Append(value);
            Append(Environment.NewLine);
        }

        public void Append(ReadOnlySpan<char> value)
        {
            if (builder.Length + value.Length > maxCharacters)
            {
                throw new InvalidOperationException($"External source text exceeds the {maxCharacters} character ingestion limit.");
            }

            builder.Append(value);
        }

        public override string ToString() => builder.ToString();
    }
}
