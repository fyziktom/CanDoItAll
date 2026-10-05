using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Workbench.Planning.UI;

public enum CalendarExportFormat { Csv, Xlsx }
public sealed record CalendarExportFile(string Name, string MediaType, byte[] Content);

public static class CalendarListExport {
    private static readonly string[] Headers = ["Event", "Start", "End", "Timezone", "Status", "Type", "Location", "Customer", "Category"];
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static CalendarExportFile Create(CalendarPresentation accepted, CanvasCalendarExportRequest request) {
        var format = request.Format switch {
            "csv" => CalendarExportFormat.Csv,
            "xlsx" => CalendarExportFormat.Xlsx,
            _ => throw new ArgumentException("Unsupported calendar export format.", nameof(request))
        };
        var surface = accepted.Calendar ?? throw new InvalidOperationException("No accepted calendar is available.");
        var events = surface.Events.ToDictionary(item => item.EventId, StringComparer.Ordinal);
        var ids = request.VisibleEvents.Select(item => item.EventId).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length || ids.Any(id => !events.ContainsKey(id))) {
            throw new InvalidOperationException("The export contains an event outside the accepted calendar.");
        }
        var rows = new List<IReadOnlyList<string>> { Headers };
        rows.AddRange(ids.Select(id => events[id]).Select(item => (IReadOnlyList<string>) [
            item.Title, Format(item.StartUtc, surface.Timezone), Format(item.EndUtc, surface.Timezone), surface.Timezone,
            item.Status, item.EventType, item.LocationLabel, item.CustomerName, item.Category
        ]));
        return format switch {
            CalendarExportFormat.Csv => new("project-calendar.csv", "text/csv;charset=utf-8",
                Encoding.UTF8.GetBytes("\uFEFF" + string.Join("\r\n", rows.Select(row => string.Join(',', row.Select(CsvCell)))) + "\r\n")),
            CalendarExportFormat.Xlsx => new("project-calendar.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", BuildWorkbook(rows)),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
    }

    private static string Format(DateTimeOffset? value, string zone) => value.HasValue ? CalendarTimeDisplay.Format(value.Value, zone) : string.Empty;

    private static string CsvCell(string value) {
        var text = value ?? string.Empty;
        if (text.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@') {
            text = "'" + text;
        }
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static byte[] BuildWorkbook(IReadOnlyList<IReadOnlyList<string>> rows) {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true)) {
            Write(archive, "[Content_Types].xml", new XElement(ContentTypes + "Types",
                new XElement(ContentTypes + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ContentTypes + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(ContentTypes + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(ContentTypes + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))));
            Write(archive, "_rels/.rels", Relationship("officeDocument", "xl/workbook.xml"));
            XNamespace office = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            Write(archive, "xl/workbook.xml", new XElement(Spreadsheet + "workbook",
                new XElement(Spreadsheet + "sheets", new XElement(Spreadsheet + "sheet",
                    new XAttribute("name", "Calendar"), new XAttribute("sheetId", "1"), new XAttribute(office + "id", "rId1")))));
            Write(archive, "xl/_rels/workbook.xml.rels", Relationship("worksheet", "worksheets/sheet1.xml"));
            Write(archive, "xl/worksheets/sheet1.xml", new XElement(Spreadsheet + "worksheet", new XElement(Spreadsheet + "sheetData",
                rows.Select((row, index) => new XElement(Spreadsheet + "row", new XAttribute("r", index + 1),
                    row.Select(value => new XElement(Spreadsheet + "c", new XAttribute("t", "inlineStr"),
                        new XElement(Spreadsheet + "is", new XElement(Spreadsheet + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value ?? string.Empty)))))))));
        }
        return stream.ToArray();
    }

    private static XElement Relationship(string kind, string target)
        => new(Relationships + "Relationships", new XElement(Relationships + "Relationship", new XAttribute("Id", "rId1"),
            new XAttribute("Type", $"http://schemas.openxmlformats.org/officeDocument/2006/relationships/{kind}"), new XAttribute("Target", target)));

    private static void Write(ZipArchive archive, string name, XElement content) {
        using var output = archive.CreateEntry(name).Open();
        content.Save(output);
    }
}
