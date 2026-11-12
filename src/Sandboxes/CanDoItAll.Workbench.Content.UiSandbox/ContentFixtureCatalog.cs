using System.Globalization;
using System.Text;

namespace CanDoItAll.Workbench.Content.UiSandbox;

public sealed record ContentFixture(string Name, string MediaType, byte[] Bytes);

public static class ContentFixtureCatalog {
    public static IReadOnlyList<ContentFixture> Create() => [
        Text("notes.txt", "text/plain", "Project files fixture · Žluťoučký 東京\nRead-only content from an independent session."),
        Text("README.md", "text/markdown", "# Project files\n\n**Real Markdown** content with a bounded preview.\n\n- Inspect\n- Return to files"),
        Text("data.json", "application/json", "{\"project\":\"Portfolio\",\"readOnly\":true,\"items\":[1,2,3]}"),
        Text("diagram.mermaid", "text/vnd.mermaid", "flowchart LR\n    Project --> Files\n    Files --> Preview"),
        Text("diagram.svg", "image/svg+xml", "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"480\" height=\"180\" viewBox=\"0 0 480 180\"><rect width=\"480\" height=\"180\" fill=\"#eef2ff\"/><rect x=\"24\" y=\"45\" width=\"160\" height=\"80\" rx=\"12\" fill=\"#4338ca\"/><text x=\"55\" y=\"92\" fill=\"white\" font-size=\"22\">Project files</text><path d=\"M184 85h90\" stroke=\"#4338ca\" stroke-width=\"6\"/><circle cx=\"345\" cy=\"85\" r=\"45\" fill=\"#0891b2\"/></svg>"),
        new("image.png", "image/png", Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jJ1sAAAAASUVORK5CYII=")),
        new("manual.pdf", "application/pdf", Pdf()),
        Text("activity.log", "text/plain", "2026-10-06 12:00:00Z Scenario started\n2026-10-06 12:01:00Z Scenario accepted"),
        new("budget.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Workbook()),
        Text("unsupported.bin", "application/octet-stream", "Inert unknown content")
    ];

    private static ContentFixture Text(string name, string type, string text) => new(name, type, Encoding.UTF8.GetBytes(text));

    private static byte[] Workbook() {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.AddWorksheet("Content scenario");
        sheet.Cell("A1").Value = "Item";
        sheet.Cell("B1").Value = "Amount";
        sheet.Cell("A2").Value = "Exact scenario";
        sheet.Cell("B2").Value = 42.5;
        sheet.Cell("A3").Value = "Formula";
        sheet.Cell("B3").FormulaA1 = "SUM(B2:B2)";
        using var buffer = new MemoryStream();
        workbook.SaveAs(buffer);
        return buffer.ToArray();
    }

    private static byte[] Pdf() {
        const string content = "BT /F1 20 Tf 40 110 Td (Project files: read-only PDF fixture) Tj ET\n";
        string[] objects = [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 500 180] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {content.Length} >>\nstream\n{content}endstream"
        ];
        var document = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (int index = 0; index < objects.Length; index++) {
            offsets.Add(document.Length);
            document.Append(CultureInfo.InvariantCulture, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        int xref = document.Length;
        document.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (int offset in offsets) {
            document.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }
        document.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(document.ToString());
    }
}
