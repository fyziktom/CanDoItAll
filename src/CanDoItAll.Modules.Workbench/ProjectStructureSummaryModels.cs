using System.IO.Compression;
using System.Text;
using System.Xml;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureSummaryNode(
    string NodeId,
    string Title,
    string KindLabel,
    string Status,
    string ProgressLabel,
    int Depth,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    IReadOnlyList<ProjectStructureSummaryNode> Children);

public sealed record ProjectStructureSummary(
    ProjectStructureSummaryNode Root,
    IReadOnlyList<ProjectStructureSummaryNode> Rows,
    int CompletedCount,
    int ActiveCount,
    int BlockedCount,
    int ReviewCount,
    int UndatedCount);

internal static class ProjectStructureSummaryBuilder
{
    public static ProjectStructureSummary Build(ProjectStructureSurface surface, ProjectStructureNode rootNode)
    {
        var childrenByParent = surface.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
            .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var rows = new List<ProjectStructureSummaryNode>();
        var root = BuildNode(rootNode, depth: 0, childrenByParent, rows);

        return new ProjectStructureSummary(
            root,
            rows,
            rows.Count(row => IsDoneStatus(row.Status)),
            rows.Count(row => IsActiveStatus(row.Status)),
            rows.Count(row => IsBlockedStatus(row.Status)),
            rows.Count(row => IsReviewStatus(row.Status)),
            rows.Count(row => !row.StartUtc.HasValue || !row.EndUtc.HasValue));
    }

    private static ProjectStructureSummaryNode BuildNode(
        ProjectStructureNode node,
        int depth,
        IReadOnlyDictionary<string, List<ProjectStructureNode>> childrenByParent,
        List<ProjectStructureSummaryNode> rows)
    {
        var summaryNode = new ProjectStructureSummaryNode(
            node.Id,
            node.Title,
            ProjectStructureCanvasCatalog.ResolveNodeLabel(node),
            string.IsNullOrWhiteSpace(node.Status) ? "Draft" : node.Status,
            ResolveProgressLabel(node),
            depth,
            node.StartUtc,
            node.EndUtc,
            []);
        rows.Add(summaryNode);
        var children = childrenByParent.TryGetValue(node.Id, out var descendants)
            ? descendants
                .OrderBy(child => child.Y)
                .ThenBy(child => child.X)
                .Select(child => BuildNode(child, depth + 1, childrenByParent, rows))
                .ToList()
            : [];
        return summaryNode with { Children = children };
    }

    private static string ResolveProgressLabel(ProjectStructureNode node)
    {
        if (string.Equals(node.ProgressMode, "complete", StringComparison.OrdinalIgnoreCase))
        {
            return "100%";
        }

        if (string.Equals(node.ProgressMode, "na", StringComparison.OrdinalIgnoreCase))
        {
            return "N/A";
        }

        if (string.Equals(node.ProgressMode, "started", StringComparison.OrdinalIgnoreCase))
        {
            return "Started";
        }

        return $"{Math.Clamp(node.ProgressPercent, 0, 100)}%";
    }

    private static bool IsDoneStatus(string status)
        => status.Contains("done", StringComparison.OrdinalIgnoreCase) ||
           status.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
           status.Contains("approved", StringComparison.OrdinalIgnoreCase);

    private static bool IsActiveStatus(string status)
        => status.Contains("progress", StringComparison.OrdinalIgnoreCase) ||
           status.Contains("active", StringComparison.OrdinalIgnoreCase) ||
           status.Contains("running", StringComparison.OrdinalIgnoreCase);

    private static bool IsBlockedStatus(string status)
        => status.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
           status.Contains("risk", StringComparison.OrdinalIgnoreCase);

    private static bool IsReviewStatus(string status)
        => status.Contains("review", StringComparison.OrdinalIgnoreCase) ||
           status.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
           status.Contains("test", StringComparison.OrdinalIgnoreCase) ||
           status.Contains("qa", StringComparison.OrdinalIgnoreCase);
}

internal static class ProjectStructureSummaryExporter
{
    public static string BuildMermaidGantt(ProjectStructureSummary summary, DateOnly anchorDate)
    {
        var builder = new StringBuilder();
        builder.AppendLine("gantt");
        builder.AppendLine($"    title {SanitizeMermaidText(summary.Root.Title)} progress summary");
        builder.AppendLine("    dateFormat YYYY-MM-DD");
        builder.AppendLine("    axisFormat %m-%d");
        builder.AppendLine("    excludes weekends");

        var currentSection = string.Empty;
        var rowIndex = 0;
        foreach (var row in summary.Rows)
        {
            rowIndex++;
            var section = ResolveSection(row, summary.Root.Title);
            if (!string.Equals(section, currentSection, StringComparison.Ordinal))
            {
                currentSection = section;
                builder.AppendLine($"    section {SanitizeMermaidText(section)}");
            }

            var (startDate, durationDays, usesSyntheticDates) = ResolveTaskSchedule(row, anchorDate, rowIndex);
            var stateToken = ResolveMermaidState(row.Status);
            var taskId = $"task{rowIndex}";
            var labelPrefix = usesSyntheticDates ? "[Undated] " : string.Empty;
            var label = $"{Indent(row.Depth)}{labelPrefix}{row.Title} ({row.Status})";

            builder.Append("    ");
            builder.Append(SanitizeMermaidText(label));
            builder.Append(" :");
            if (!string.IsNullOrWhiteSpace(stateToken))
            {
                builder.Append(stateToken);
                builder.Append(", ");
            }

            builder.Append(taskId);
            builder.Append(", ");
            builder.Append(startDate.ToString("yyyy-MM-dd"));
            builder.Append(", ");
            builder.Append(durationDays);
            builder.AppendLine("d");
        }

        return builder.ToString().TrimEnd();
    }

    public static byte[] BuildWorkbook(ProjectStructureSummary summary)
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[]
            {
                "Depth",
                "Title",
                "Kind",
                "Status",
                "Progress",
                "Start",
                "End",
                "Children"
            }
        };

        rows.AddRange(summary.Rows.Select(row =>
            (IReadOnlyList<string>)
            [
                row.Depth.ToString(),
                $"{Indent(row.Depth)}{row.Title}",
                row.KindLabel,
                row.Status,
                row.ProgressLabel,
                row.StartUtc?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty,
                row.EndUtc?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty,
                row.Children.Count.ToString()
            ]));

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteZipEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
            WriteZipEntry(archive, "_rels/.rels", BuildRootRelationshipsXml());
            WriteZipEntry(archive, "xl/workbook.xml", BuildWorkbookXml());
            WriteZipEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationshipsXml());
            WriteZipEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(rows));
        }

        return stream.ToArray();
    }

    private static (DateOnly StartDate, int DurationDays, bool UsesSyntheticDates) ResolveTaskSchedule(
        ProjectStructureSummaryNode row,
        DateOnly anchorDate,
        int index)
    {
        var hasExplicitDates = row.StartUtc.HasValue && row.EndUtc.HasValue;
        var startDate = row.StartUtc.HasValue
            ? DateOnly.FromDateTime(row.StartUtc.Value.UtcDateTime)
            : anchorDate.AddDays(index - 1);
        var endDate = row.EndUtc.HasValue
            ? DateOnly.FromDateTime(row.EndUtc.Value.UtcDateTime)
            : startDate.AddDays(Math.Max(1, row.Children.Count));
        if (endDate < startDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        var durationDays = Math.Max(1, endDate.DayNumber - startDate.DayNumber + 1);
        return (startDate, durationDays, !hasExplicitDates);
    }

    private static string ResolveSection(ProjectStructureSummaryNode row, string rootTitle)
    {
        if (row.Depth == 0)
        {
            return rootTitle;
        }

        return row.Depth == 1
            ? row.Title
            : rootTitle;
    }

    private static string ResolveMermaidState(string status)
    {
        if (status.Contains("done", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("approved", StringComparison.OrdinalIgnoreCase))
        {
            return "done";
        }

        if (status.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("risk", StringComparison.OrdinalIgnoreCase))
        {
            return "crit";
        }

        if (status.Contains("review", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("progress", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("active", StringComparison.OrdinalIgnoreCase))
        {
            return "active";
        }

        return string.Empty;
    }

    private static string BuildContentTypesXml()
        => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
          <Default Extension="xml" ContentType="application/xml" />
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
        </Types>
        """;

    private static string BuildRootRelationshipsXml()
        => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" />
        </Relationships>
        """;

    private static string BuildWorkbookXml()
        => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="Progress Summary" sheetId="1" r:id="rId1" />
          </sheets>
        </workbook>
        """;

    private static string BuildWorkbookRelationshipsXml()
        => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
        </Relationships>
        """;

    private static string BuildWorksheetXml(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false,
            Indent = true
        };
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartDocument(true);
            writer.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            writer.WriteStartElement("sheetData");

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                writer.WriteStartElement("row");
                writer.WriteAttributeString("r", (rowIndex + 1).ToString());

                var row = rows[rowIndex];
                for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
                {
                    writer.WriteStartElement("c");
                    writer.WriteAttributeString("r", $"{ToColumnName(columnIndex + 1)}{rowIndex + 1}");
                    writer.WriteAttributeString("t", "inlineStr");
                    writer.WriteStartElement("is");
                    writer.WriteElementString("t", row[columnIndex] ?? string.Empty);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string ToColumnName(int index)
    {
        var builder = new StringBuilder();
        var current = index;
        while (current > 0)
        {
            current--;
            builder.Insert(0, (char)('A' + (current % 26)));
            current /= 26;
        }

        return builder.ToString();
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Indent(int depth)
        => depth <= 0 ? string.Empty : new string('>', depth);

    private static string SanitizeMermaidText(string value)
        => string.IsNullOrWhiteSpace(value)
            ? "Untitled"
            : value
                .Replace(":", " -", StringComparison.Ordinal)
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Trim();
}
