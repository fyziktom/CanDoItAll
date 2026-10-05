using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Insights.UI;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage {
    private InsightsOutlineNode PresentOutlineNode(ProjectStructureNode node) => new(node.Id, node.Title, node.Status,
        ProjectStructureCanvasCatalog.ResolveNodeLabel(node), ResolveOutlineIcon(node), node.Subtitle ?? string.Empty,
        node.ObjectSubtype ?? string.Empty, ResolveSupportPanelContextActions(node).ToArray());

    private static string ResolveOutlineIcon(ProjectStructureNode node)
    {
        return node.ObjectType switch
        {
            ProjectObjectType.ProjectRoot => "folder",
            ProjectObjectType.Phase => "phase",
            ProjectObjectType.Milestone => "date",
            ProjectObjectType.Decision => "choice",
            ProjectObjectType.ProjectBlock => "block",
            ProjectObjectType.Meeting => "meeting",
            ProjectObjectType.Recording => "recording",
            ProjectObjectType.Transcript => "transcript",
            ProjectObjectType.Participant => "people",
            ProjectObjectType.WorkItem => "task",
            ProjectObjectType.PromptFlow => "flow",
            ProjectObjectType.PromptSession => "session",
            ProjectObjectType.PromptStep => "step",
            ProjectObjectType.Repository => "repo",
            ProjectObjectType.File => ResolveFileIcon(node.ObjectSubtype),
            ProjectObjectType.ImageAsset => "image",
            ProjectObjectType.VideoAsset => "video",
            ProjectObjectType.Link => "link",
            ProjectObjectType.Connector => "plug",
            ProjectObjectType.Script => "draw",
            ProjectObjectType.Environment => "runtime",
            ProjectObjectType.Infrastructure => "infra",
            ProjectObjectType.ValidationRun => "qa",
            ProjectObjectType.TestPlan => "test",
            ProjectObjectType.TestEvidence => "evidence",
            ProjectObjectType.SecretReference => "shield",
            _ => "section"
        };
    }

    private static string ResolveFileIcon(string? objectSubtype)
    {
        return objectSubtype?.Trim().ToLowerInvariant() switch
        {
            "pdf" => "pdf",
            "excel" => "excel",
            "docx" => "docx",
            "markdown" => "markdown",
            "mermaid" => "mermaid",
            "screenshot" => "screenshot",
            "log" => "log",
            "json" => "json",
            "text" => "text",
            "archive" => "archive",
            "audio" => "audio",
            _ => "file"
        };
    }

}
