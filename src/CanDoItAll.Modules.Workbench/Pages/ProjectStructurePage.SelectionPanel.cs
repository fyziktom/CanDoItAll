using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private const string AdvancedDetailsHelpBody = "This section keeps artifact metadata, canvas coordinates, and the few node-specific facts that only matter when you need to inspect or troubleshoot the selected item.";
    private const string AdvancedDetailsHelpTip = "The main card above stays focused on actions, status, and the badges already visible.";

    private IReadOnlyList<ProjectStructureSelectionBadgePresentation> SelectedNodeBadgePresentations
        => selectedNode is null
            ? []
            : BuildSelectedNodeBadgePresentations(selectedNode);

    private static IReadOnlyList<ProjectStructureSelectionBadgePresentation> BuildSelectedNodeBadgePresentations(ProjectStructureNode node)
        => node.Badges
            .Select(badge => BuildSelectedNodeBadgePresentation(node, badge))
            .ToList();

    private static ProjectStructureSelectionBadgePresentation BuildSelectedNodeBadgePresentation(ProjectStructureNode node, string badge)
    {
        var style = ResolveSelectedBadgeStyle(node, badge);
        return new(badge, style, BuildSelectedBadgeTestId(badge));
    }

    private static ProjectStructureSelectionBadgeStyle ResolveSelectedBadgeStyle(ProjectStructureNode node, string badge)
    {
        if (string.Equals(badge, "Uploaded", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectStructureSelectionBadgeStyle.Uploaded;
        }

        if (string.Equals(badge, "Scheduled", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectStructureSelectionBadgeStyle.Scheduled;
        }

        if (string.Equals(badge, "Synced", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectStructureSelectionBadgeStyle.Synced;
        }

        if (node.ObjectType == ProjectObjectType.File &&
            string.Equals(badge, ProjectStructureCanvasCatalog.ResolveNodeLabel(node), StringComparison.OrdinalIgnoreCase))
        {
            return ResolveFileSelectedBadgeStyle(node.ObjectSubtype);
        }

        return ProjectStructureSelectionBadgeStyle.Standard;
    }

    private static ProjectStructureSelectionBadgeStyle ResolveFileSelectedBadgeStyle(string objectSubtype)
        => objectSubtype switch
        {
            "pdf" => ProjectStructureSelectionBadgeStyle.FilePdf,
            "excel" => ProjectStructureSelectionBadgeStyle.FileExcel,
            "docx" => ProjectStructureSelectionBadgeStyle.FileDocx,
            "markdown" => ProjectStructureSelectionBadgeStyle.FileMarkdown,
            "mermaid" => ProjectStructureSelectionBadgeStyle.FileMermaid,
            "screenshot" => ProjectStructureSelectionBadgeStyle.FileScreenshot,
            "log" => ProjectStructureSelectionBadgeStyle.FileLog,
            "archive" => ProjectStructureSelectionBadgeStyle.FileArchive,
            "audio" => ProjectStructureSelectionBadgeStyle.FileAudio,
            "json" => ProjectStructureSelectionBadgeStyle.FileJson,
            "text" => ProjectStructureSelectionBadgeStyle.FileText,
            _ => ProjectStructureSelectionBadgeStyle.FileGeneric
        };

    private static string BuildSelectedBadgeTestId(string badge)
    {
        var sanitized = string.Concat(
            badge
                .Trim()
                .ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '-'));

        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
        }

        sanitized = sanitized.Trim('-');
        return string.IsNullOrWhiteSpace(sanitized)
            ? "project-structure-selection-badge"
            : $"project-structure-selection-badge-{sanitized}";
    }
}

internal sealed record ProjectStructureSelectionBadgePresentation(
    string Text,
    ProjectStructureSelectionBadgeStyle Style,
    string TestId);

internal enum ProjectStructureSelectionBadgeStyle
{
    Standard,
    Uploaded,
    Scheduled,
    Synced,
    FileGeneric,
    FilePdf,
    FileExcel,
    FileDocx,
    FileMarkdown,
    FileMermaid,
    FileScreenshot,
    FileLog,
    FileArchive,
    FileAudio,
    FileJson,
    FileText
}
