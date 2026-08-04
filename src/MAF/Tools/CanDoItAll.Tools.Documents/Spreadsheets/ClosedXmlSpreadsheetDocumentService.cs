using System.Buffers;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using ClosedXML.Excel;
using ClosedXML.Graphics;

namespace CanDoItAll.Tools.Documents;

public sealed class ClosedXmlSpreadsheetDocumentService : ISpreadsheetDocumentService
{
    private const string FallbackFontResourceName =
        "ClosedXML.Graphics.Fonts.CarlitoBare-Regular.ttf";

    private static readonly Lazy<IXLGraphicEngine> PortableGraphicEngine =
        new(CreatePortableGraphicEngine, LazyThreadSafetyMode.ExecutionAndPublication);

    public SpreadsheetWorkbookSummary InspectWorkbook(string workbookPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookPath);
        using var workbook = new XLWorkbook(workbookPath, CreateLoadOptions());
        var worksheets = workbook.Worksheets
            .Select((worksheet, index) =>
            {
                var usedRange = worksheet.RangeUsed();
                return new SpreadsheetWorksheetSummary(
                    worksheet.Name,
                    index + 1,
                    usedRange?.RangeAddress.ToStringRelative() ?? string.Empty,
                    usedRange?.RowCount() ?? 0,
                    usedRange?.ColumnCount() ?? 0);
            })
            .ToArray();

        return new SpreadsheetWorkbookSummary(workbookPath, worksheets);
    }

    public SpreadsheetWorkbookPreviewResult PreviewWorkbook(SpreadsheetWorkbookPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkbookPath);
        ValidatePreviewLimits(request.MaxWorksheets, request.MaxRows, request.MaxColumns);

        using var workbook = OpenPreviewWorkbook(request.WorkbookPath);
        return CreateWorkbookPreview(
            request.WorkbookPath,
            workbook,
            request.MaxWorksheets,
            request.MaxRows,
            request.MaxColumns);
    }

    public SpreadsheetWorkbookContentPreviewResult PreviewWorkbook(
        SpreadsheetWorkbookContentPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkbookName);
        ValidatePreviewLimits(request.MaxWorksheets, request.MaxRows, request.MaxColumns);

        if (request.Content.IsEmpty)
        {
            throw new InvalidDataException(
                $"Spreadsheet workbook '{request.WorkbookName}' is empty.");
        }

        if (request.Content.Length > SpreadsheetWorkbookContentPreviewRequest.MaximumContentBytes)
        {
            throw new InvalidDataException(
                $"Spreadsheet workbook '{request.WorkbookName}' exceeds the " +
                $"{SpreadsheetWorkbookContentPreviewRequest.MaximumContentBytes} byte preview limit.");
        }

        ValidateWorkbookArchive(request.WorkbookName, request.Content);
        using var workbook = OpenPreviewWorkbook(request.WorkbookName, request.Content);
        var preview = CreateWorksheetPreviews(
            workbook,
            request.MaxWorksheets,
            request.MaxRows,
            request.MaxColumns);
        return new SpreadsheetWorkbookContentPreviewResult(
            request.WorkbookName,
            preview.TotalWorksheetCount,
            preview.Worksheets,
            WorksheetsTruncated: preview.TotalWorksheetCount > preview.Worksheets.Length);
    }

    private static SpreadsheetWorkbookPreviewResult CreateWorkbookPreview(
        string workbookReference,
        XLWorkbook workbook,
        int maxWorksheets,
        int maxRows,
        int maxColumns)
    {
        var preview = CreateWorksheetPreviews(workbook, maxWorksheets, maxRows, maxColumns);

        return new SpreadsheetWorkbookPreviewResult(
            workbookReference,
            preview.TotalWorksheetCount,
            preview.Worksheets,
            WorksheetsTruncated: preview.TotalWorksheetCount > preview.Worksheets.Length);
    }

    public SpreadsheetCellValue ReadCell(string workbookPath, string worksheetName, string cellAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(cellAddress);

        using var workbook = new XLWorkbook(workbookPath, CreateLoadOptions());
        var worksheet = GetWorksheet(workbook, worksheetName);
        var cell = worksheet.Cell(cellAddress);

        return new SpreadsheetCellValue(
            cell.Address.ToStringRelative(),
            CellToString(cell));
    }

    public SpreadsheetRangeReadResult ReadRange(
        string workbookPath,
        string worksheetName,
        string rangeAddress,
        int maxRows,
        int maxColumns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(rangeAddress);

        var rowLimit = Math.Clamp(maxRows, 1, 1000);
        var columnLimit = Math.Clamp(maxColumns, 1, 100);
        using var workbook = new XLWorkbook(workbookPath, CreateLoadOptions());
        var worksheet = GetWorksheet(workbook, worksheetName);
        var range = worksheet.Range(rangeAddress);
        var rowCount = Math.Min(range.RowCount(), rowLimit);
        var columnCount = Math.Min(range.ColumnCount(), columnLimit);
        var rows = new List<IReadOnlyList<string>>(rowCount);

        for (var rowIndex = 1; rowIndex <= rowCount; rowIndex++)
        {
            var row = new List<string>(columnCount);
            for (var columnIndex = 1; columnIndex <= columnCount; columnIndex++)
            {
                row.Add(CellToString(range.Cell(rowIndex, columnIndex)));
            }

            rows.Add(row);
        }

        return new SpreadsheetRangeReadResult(
            workbookPath,
            worksheet.Name,
            range.RangeAddress.ToStringRelative(),
            rows,
            BuildMarkdownTable(rows));
    }

    public SpreadsheetWriteResult Write(SpreadsheetWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkbookPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorksheetName);

        var outputPath = string.IsNullOrWhiteSpace(request.OutputWorkbookPath)
            ? request.WorkbookPath
            : request.OutputWorkbookPath;
        if (File.Exists(outputPath) && !request.Overwrite)
        {
            throw new InvalidOperationException($"Spreadsheet output path '{outputPath}' already exists.");
        }

        using var workbook = File.Exists(request.WorkbookPath)
            ? new XLWorkbook(request.WorkbookPath, CreateLoadOptions())
            : CreateWorkbook(request);
        var worksheet = workbook.Worksheets.FirstOrDefault(item => string.Equals(item.Name, request.WorksheetName, StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.Add(request.WorksheetName);

        foreach (var cellWrite in request.CellWrites)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cellWrite.CellAddress);
            WriteCellValue(worksheet.Cell(cellWrite.CellAddress), cellWrite.Value);
        }

        foreach (var rangeWrite in request.RangeWrites)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rangeWrite.RangeAddress);
            var range = worksheet.Range(rangeWrite.RangeAddress);
            WriteRange(range, rangeWrite.Values);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        workbook.SaveAs(outputPath);

        return new SpreadsheetWriteResult(
            outputPath,
            worksheet.Name,
            request.CellWrites.Count,
            request.RangeWrites.Count);
    }

    private static XLWorkbook CreateWorkbook(SpreadsheetWriteRequest request)
    {
        if (!request.CreateWorkbookIfMissing)
        {
            throw new FileNotFoundException($"Spreadsheet workbook '{request.WorkbookPath}' was not found.", request.WorkbookPath);
        }

        return new XLWorkbook();
    }

    private static IXLWorksheet GetWorksheet(XLWorkbook workbook, string worksheetName)
    {
        return workbook.Worksheets.FirstOrDefault(item => string.Equals(item.Name, worksheetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Spreadsheet worksheet '{worksheetName}' was not found.");
    }

    private static XLWorkbook OpenPreviewWorkbook(string workbookPath)
    {
        var fullPath = Path.GetFullPath(workbookPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Spreadsheet workbook '{fullPath}' was not found.", fullPath);
        }

        try
        {
            return new XLWorkbook(fullPath, CreateLoadOptions());
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new InvalidDataException($"Spreadsheet workbook '{fullPath}' could not be opened as an XLSX workbook.", exception);
        }
    }

    private static XLWorkbook OpenPreviewWorkbook(
        string workbookName,
        ReadOnlyMemory<byte> content)
    {
        try
        {
            using var stream = OpenContentStream(content);
            return new XLWorkbook(stream, CreateLoadOptions());
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new InvalidDataException(
                $"Spreadsheet workbook '{workbookName}' could not be opened as an XLSX workbook.",
                exception);
        }
    }

    private static void ValidateWorkbookArchive(
        string workbookName,
        ReadOnlyMemory<byte> content)
    {
        try
        {
            using var stream = OpenContentStream(content);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            if (archive.Entries.Count > SpreadsheetWorkbookContentPreviewRequest.MaximumArchiveEntries)
            {
                throw new InvalidDataException(
                    $"Spreadsheet workbook '{workbookName}' contains too many archive entries for preview.");
            }

            var entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long expandedBytes = 0;
            long worksheetXmlBytes = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                var normalizedName = NormalizeArchiveEntryName(entry.FullName);
                if (!entryNames.Add(normalizedName))
                {
                    throw new InvalidDataException(
                        $"Spreadsheet workbook '{workbookName}' contains duplicate package entries.");
                }

                var entryExpandedBytes = DrainArchiveEntry(
                    workbookName,
                    entry,
                    ref expandedBytes);
                ValidateXmlPartSize(workbookName, normalizedName, entryExpandedBytes);
                if (IsWorksheetPart(normalizedName))
                {
                    worksheetXmlBytes = checked(worksheetXmlBytes + entryExpandedBytes);
                    EnsureWithinLimit(
                        workbookName,
                        worksheetXmlBytes,
                        SpreadsheetWorkbookContentPreviewRequest.MaximumWorksheetXmlBytes,
                        "worksheet XML");
                }
            }

            ValidatePackageComplexity(workbookName, archive);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new InvalidDataException(
                $"Spreadsheet workbook '{workbookName}' is not a valid bounded XLSX archive.",
                exception);
        }
    }

    private static (int TotalWorksheetCount, SpreadsheetWorksheetPreview[] Worksheets)
        CreateWorksheetPreviews(
            XLWorkbook workbook,
            int maxWorksheets,
            int maxRows,
            int maxColumns)
    {
        var totalWorksheetCount = workbook.Worksheets.Count;
        var worksheets = workbook.Worksheets
            .Take(maxWorksheets)
            .Select((worksheet, index) => CreateWorksheetPreview(
                worksheet,
                index + 1,
                maxRows,
                maxColumns))
            .ToArray();
        return (totalWorksheetCount, worksheets);
    }

    private static Stream OpenContentStream(ReadOnlyMemory<byte> content)
    {
        if (MemoryMarshal.TryGetArray(content, out ArraySegment<byte> segment))
        {
            return new MemoryStream(
                segment.Array!,
                segment.Offset,
                segment.Count,
                writable: false,
                publiclyVisible: false);
        }

        return new MemoryStream(content.ToArray(), writable: false);
    }

    private static LoadOptions CreateLoadOptions()
        => new()
        {
            RecalculateAllFormulas = false,
            GraphicEngine = PortableGraphicEngine.Value
        };

    private static IXLGraphicEngine CreatePortableGraphicEngine()
    {
        using Stream fallbackFont = typeof(DefaultGraphicEngine).Assembly.GetManifestResourceStream(
            FallbackFontResourceName) ?? throw new InvalidOperationException(
            $"ClosedXML fallback font resource '{FallbackFontResourceName}' was not found.");
        return DefaultGraphicEngine.CreateOnlyWithFonts(fallbackFont);
    }

    private static long DrainArchiveEntry(
        string workbookName,
        ZipArchiveEntry entry,
        ref long totalExpandedBytes)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            using Stream entryStream = entry.Open();
            long entryExpandedBytes = 0;
            int bytesRead;
            while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                entryExpandedBytes = checked(entryExpandedBytes + bytesRead);
                totalExpandedBytes = checked(totalExpandedBytes + bytesRead);
                EnsureWithinLimit(
                    workbookName,
                    totalExpandedBytes,
                    SpreadsheetWorkbookContentPreviewRequest.MaximumExpandedBytes,
                    "expanded package");
            }

            return entryExpandedBytes;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void ValidateXmlPartSize(
        string workbookName,
        string entryName,
        long expandedBytes)
    {
        if (!IsXmlPart(entryName))
        {
            return;
        }

        if (entryName.Equals("xl/styles.xml", StringComparison.OrdinalIgnoreCase))
        {
            EnsureWithinLimit(
                workbookName,
                expandedBytes,
                SpreadsheetWorkbookContentPreviewRequest.MaximumStylesXmlBytes,
                "styles XML");
        }

        if (entryName.Equals("xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase))
        {
            EnsureWithinLimit(
                workbookName,
                expandedBytes,
                SpreadsheetWorkbookContentPreviewRequest.MaximumSharedStringsXmlBytes,
                "shared-strings XML");
        }

        EnsureWithinLimit(
            workbookName,
            expandedBytes,
            SpreadsheetWorkbookContentPreviewRequest.MaximumXmlPartBytes,
            "XML part");
    }

    private static void ValidatePackageComplexity(
        string workbookName,
        ZipArchive archive)
    {
        var worksheetCount = 0;
        var cellCount = 0;
        var styleCount = 0;
        var sharedStringCount = 0;

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            var entryName = NormalizeArchiveEntryName(entry.FullName);
            PackagePartKind partKind = ResolvePackagePartKind(entryName);
            if (partKind is PackagePartKind.None)
            {
                continue;
            }

            using Stream entryStream = entry.Open();
            using XmlReader reader = XmlReader.Create(entryStream, CreateSecureXmlReaderSettings());
            while (reader.Read())
            {
                if (reader.NodeType is not XmlNodeType.Element)
                {
                    continue;
                }

                switch (partKind, reader.LocalName)
                {
                    case (PackagePartKind.Workbook, "sheet"):
                        EnsureCountWithinLimit(
                            workbookName,
                            ++worksheetCount,
                            SpreadsheetWorkbookContentPreviewRequest.MaximumPackageWorksheets,
                            "worksheets");
                        break;
                    case (PackagePartKind.Worksheet, "c"):
                        EnsureCountWithinLimit(
                            workbookName,
                            ++cellCount,
                            SpreadsheetWorkbookContentPreviewRequest.MaximumPackageCells,
                            "cells");
                        break;
                    case (PackagePartKind.Styles, "xf"):
                        EnsureCountWithinLimit(
                            workbookName,
                            ++styleCount,
                            SpreadsheetWorkbookContentPreviewRequest.MaximumPackageStyles,
                            "styles");
                        break;
                    case (PackagePartKind.SharedStrings, "si"):
                        EnsureCountWithinLimit(
                            workbookName,
                            ++sharedStringCount,
                            SpreadsheetWorkbookContentPreviewRequest.MaximumPackageSharedStrings,
                            "shared strings");
                        break;
                }
            }
        }
    }

    private static XmlReaderSettings CreateSecureXmlReaderSettings()
        => new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            CloseInput = false,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        };

    private static PackagePartKind ResolvePackagePartKind(string entryName)
    {
        if (entryName.Equals("xl/workbook.xml", StringComparison.OrdinalIgnoreCase))
        {
            return PackagePartKind.Workbook;
        }

        if (IsWorksheetPart(entryName))
        {
            return PackagePartKind.Worksheet;
        }

        if (entryName.Equals("xl/styles.xml", StringComparison.OrdinalIgnoreCase))
        {
            return PackagePartKind.Styles;
        }

        return entryName.Equals("xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase)
            ? PackagePartKind.SharedStrings
            : PackagePartKind.None;
    }

    private static bool IsWorksheetPart(string entryName)
        => entryName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)
            && entryName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsXmlPart(string entryName)
        => entryName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            || entryName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeArchiveEntryName(string entryName)
        => entryName.Replace('\\', '/').TrimStart('/');

    private static void EnsureWithinLimit(
        string workbookName,
        long actual,
        long maximum,
        string subject)
    {
        if (actual > maximum)
        {
            throw new InvalidDataException(
                $"Spreadsheet workbook '{workbookName}' exceeds the {subject} preview limit.");
        }
    }

    private static void EnsureCountWithinLimit(
        string workbookName,
        int actual,
        int maximum,
        string subject)
    {
        if (actual > maximum)
        {
            throw new InvalidDataException(
                $"Spreadsheet workbook '{workbookName}' contains too many {subject} for preview.");
        }
    }

    private enum PackagePartKind
    {
        None,
        Workbook,
        Worksheet,
        Styles,
        SharedStrings
    }

    private static SpreadsheetWorksheetPreview CreateWorksheetPreview(
        IXLWorksheet worksheet,
        int position,
        int maxRows,
        int maxColumns)
    {
        var usedRange = worksheet.RangeUsed();
        if (usedRange is null)
        {
            return new SpreadsheetWorksheetPreview(
                worksheet.Name,
                position,
                UsedRangeAddress: string.Empty,
                UsedRowCount: 0,
                UsedColumnCount: 0,
                Values: [],
                MarkdownTable: string.Empty,
                RowsTruncated: false,
                ColumnsTruncated: false);
        }

        var usedRowCount = usedRange.RowCount();
        var usedColumnCount = usedRange.ColumnCount();
        var previewRowCount = Math.Min(usedRowCount, maxRows);
        var previewColumnCount = Math.Min(usedColumnCount, maxColumns);
        var values = new List<IReadOnlyList<string>>(previewRowCount);

        for (var rowIndex = 1; rowIndex <= previewRowCount; rowIndex++)
        {
            var row = new List<string>(previewColumnCount);
            for (var columnIndex = 1; columnIndex <= previewColumnCount; columnIndex++)
            {
                row.Add(CellToString(usedRange.Cell(rowIndex, columnIndex)));
            }

            values.Add(row);
        }

        return new SpreadsheetWorksheetPreview(
            worksheet.Name,
            position,
            usedRange.RangeAddress.ToStringRelative(),
            usedRowCount,
            usedColumnCount,
            values,
            BuildMarkdownTable(values),
            RowsTruncated: usedRowCount > previewRowCount,
            ColumnsTruncated: usedColumnCount > previewColumnCount);
    }

    private static void ValidatePreviewLimit(int value, int maximum, string parameterName)
    {
        if (value < 1 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Spreadsheet preview limit '{parameterName}' must be between 1 and {maximum}.");
        }
    }

    private static void ValidatePreviewLimits(int maxWorksheets, int maxRows, int maxColumns)
    {
        ValidatePreviewLimit(
            maxWorksheets,
            SpreadsheetWorkbookPreviewRequest.MaximumWorksheets,
            nameof(SpreadsheetWorkbookPreviewRequest.MaxWorksheets));
        ValidatePreviewLimit(
            maxRows,
            SpreadsheetWorkbookPreviewRequest.MaximumRows,
            nameof(SpreadsheetWorkbookPreviewRequest.MaxRows));
        ValidatePreviewLimit(
            maxColumns,
            SpreadsheetWorkbookPreviewRequest.MaximumColumns,
            nameof(SpreadsheetWorkbookPreviewRequest.MaxColumns));
    }

    private static void WriteRange(IXLRange range, IReadOnlyList<IReadOnlyList<string>> values)
    {
        if (values.Count > range.RowCount())
        {
            throw new InvalidOperationException($"Spreadsheet range '{range.RangeAddress.ToStringRelative()}' has {range.RowCount()} row(s), but {values.Count} row(s) were supplied.");
        }

        for (var rowIndex = 0; rowIndex < values.Count; rowIndex++)
        {
            var row = values[rowIndex];
            if (row.Count > range.ColumnCount())
            {
                throw new InvalidOperationException($"Spreadsheet range '{range.RangeAddress.ToStringRelative()}' has {range.ColumnCount()} column(s), but row {rowIndex + 1} supplied {row.Count} value(s).");
            }

            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                WriteCellValue(range.Cell(rowIndex + 1, columnIndex + 1), row[columnIndex]);
            }
        }
    }

    private static void WriteCellValue(IXLCell cell, string value)
    {
        var normalized = value ?? string.Empty;
        if (normalized.Length > 1 && normalized.StartsWith('='))
        {
            cell.FormulaA1 = normalized[1..];
            return;
        }

        cell.Value = normalized;
    }

    private static string BuildMarkdownTable(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows.Count == 0)
        {
            return string.Empty;
        }

        var columnCount = rows.Max(row => row.Count);
        var builder = new StringBuilder();
        AppendMarkdownRow(builder, NormalizeRow(rows[0], columnCount));
        AppendMarkdownRow(builder, Enumerable.Repeat("---", columnCount));

        foreach (var row in rows.Skip(1))
        {
            AppendMarkdownRow(builder, NormalizeRow(row, columnCount));
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<string> NormalizeRow(IReadOnlyList<string> row, int columnCount)
    {
        var values = new string[columnCount];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = index < row.Count ? row[index] : string.Empty;
        }

        return values;
    }

    private static void AppendMarkdownRow(StringBuilder builder, IEnumerable<string> cells)
    {
        builder.Append('|');
        foreach (var cell in cells)
        {
            builder
                .Append(' ')
                .Append(EscapeMarkdownCell(cell))
                .Append(" |");
        }

        builder.AppendLine();
    }

    private static string EscapeMarkdownCell(string value)
        => value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");

    private static string CellToString(IXLCell cell)
    {
        if (cell.HasFormula)
        {
            return "=" + cell.FormulaA1;
        }

        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        return cell.Value.Type switch
        {
            XLDataType.Blank => string.Empty,
            XLDataType.Boolean => cell.GetBoolean().ToString(CultureInfo.InvariantCulture),
            XLDataType.Number => cell.GetDouble().ToString(CultureInfo.InvariantCulture),
            XLDataType.DateTime => cell.GetDateTime().ToString("O", CultureInfo.InvariantCulture),
            XLDataType.TimeSpan => cell.GetTimeSpan().ToString(),
            _ => cell.GetFormattedString()
        };
    }
}
