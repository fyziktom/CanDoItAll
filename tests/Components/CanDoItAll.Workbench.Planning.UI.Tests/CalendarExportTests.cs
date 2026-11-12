using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Workbench.Planning.UI;

namespace CanDoItAll.Tests.WorkbenchPlanning;

public sealed class CalendarExportTests {
    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    public void Export_uses_only_requested_accepted_rows_and_ignores_submitted_field_values(string format) {
        var surface = Fixture();
        var request = new CanvasCalendarExportRequest(format,
            [new() { EventId = "accepted", Title = "Spoofed title", StartUtc = DateTimeOffset.MinValue }], new());
        var file = CalendarListExport.Create(surface, request);
        string text;
        if (format == "csv") {
            text = Encoding.UTF8.GetString(file.Content);
            Assert.Equal("project-calendar.csv", file.Name);
        } else {
            using var archive = new ZipArchive(new MemoryStream(file.Content));
            Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
            var document = XDocument.Load(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
            text = document.ToString();
            Assert.Equal(2, document.Descendants().Count(element => element.Name.LocalName == "row"));
            Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "f");
        }
        Assert.Contains("Accepted title", text);
        Assert.Contains("2026-11-01 01:30 -04:00", text);
        Assert.Contains("2026-11-01 01:30 -05:00", text);
        Assert.DoesNotContain("Spoofed", text);
        Assert.DoesNotContain("Invisible neighbor", text);
    }

    [Theory]
    [InlineData("missing", "csv")]
    [InlineData("missing", "xlsx")]
    public void Unknown_event_is_refused(string eventId, string format) {
        Assert.Throws<InvalidOperationException>(() => CalendarListExport.Create(Fixture(), new(format, [new() { EventId = eventId }], new())));
    }

    [Fact]
    public void Unsupported_format_and_duplicate_rows_are_refused() {
        Assert.Throws<ArgumentException>(() => CalendarListExport.Create(Fixture(), new("html", [], new())));
        Assert.Throws<InvalidOperationException>(() => CalendarListExport.Create(Fixture(), new("csv",
            [new() { EventId = "accepted" }, new() { EventId = "accepted" }], new())));
    }

    [Fact]
    public void Csv_quotes_fields_and_neutralizes_formula_prefixes() {
        var fixture = Fixture();
        fixture.Calendar!.Events[0].Title = "=SUM(1,2)\n\"quoted\"";
        var csv = Encoding.UTF8.GetString(CalendarListExport.Create(fixture, new("csv", [new() { EventId = "accepted" }], new())).Content);
        Assert.Contains("\"'=SUM(1,2)\n\"\"quoted\"\"\"", csv);
    }

    private static CalendarPresentation Fixture() => new(Guid.NewGuid()) {
        State = PlanningReadState.Ready,
        Calendar = new() {
            Timezone = "America/New_York",
            Events = [
                new() { EventId = "accepted", Title = "Accepted title", StartUtc = new(2026, 11, 1, 5, 30, 0, TimeSpan.Zero), EndUtc = new(2026, 11, 1, 6, 30, 0, TimeSpan.Zero) },
                new() { EventId = "neighbor", Title = "Invisible neighbor" }
            ]
        }
    };
}
