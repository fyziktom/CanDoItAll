using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

internal static class WorkflowExecutorCanvasCatalog
{
    private const string CreateExecutorActionPrefix = "workflow-executor:create:";

    public static IReadOnlyList<CanvasWorkbenchAction> BuildQuickCreateActions(
        IReadOnlyList<WorkflowExecutorDescriptor> executors)
    {
        var implemented = executors
            .Where(executor => executor.IsImplemented)
            .OrderBy(executor => executor.Category)
            .ThenBy(executor => executor.Name, StringComparer.OrdinalIgnoreCase)
            .Select(BuildCreateAction)
            .ToList();
        if (implemented.Count == 0)
        {
            return [];
        }

        return
        [
            new CanvasWorkbenchAction
            {
                ActionId = "workflow-executor:menu",
                Label = "Executors",
                MenuLabel = "Executors",
                Description = "Run typed tools with explicit settings, timeout, retry, and result contracts.",
                Icon = "bolt",
                Tone = "info",
                Children = implemented
            }
        ];
    }

    public static CanvasWorkbenchAction BuildCreateAction(WorkflowExecutorDescriptor descriptor)
        => new()
        {
            ActionId = BuildCreateActionId(descriptor.Id),
            Label = descriptor.Name,
            MenuLabel = TrimMenuLabel(descriptor.Name),
            Description = descriptor.Description,
            Icon = descriptor.IconName,
            Tone = ResolveTone(descriptor.Category),
            RequiresInput = true,
            CreateMode = "dialog",
            TitlePlaceholder = descriptor.Name,
            NotesPlaceholder = descriptor.Description,
            SubmitLabel = "Add executor",
            ObjectSubtype = descriptor.Id.Value
        };

    public static string BuildCreateActionId(WorkflowExecutorId executorId)
        => $"{CreateExecutorActionPrefix}{executorId.Value}";

    public static bool TryParseCreateActionId(string actionId, out WorkflowExecutorId executorId)
    {
        if (actionId.StartsWith(CreateExecutorActionPrefix, StringComparison.Ordinal) &&
            actionId.Length > CreateExecutorActionPrefix.Length)
        {
            executorId = new WorkflowExecutorId(actionId[CreateExecutorActionPrefix.Length..]);
            return true;
        }

        executorId = default;
        return false;
    }

    public static string ResolveTone(WorkflowExecutorCategoryKind category)
        => category switch
        {
            WorkflowExecutorCategoryKind.Storage => "success",
            WorkflowExecutorCategoryKind.ProjectStructure => "accent",
            WorkflowExecutorCategoryKind.Http => "info",
            WorkflowExecutorCategoryKind.Image => "danger",
            WorkflowExecutorCategoryKind.Spreadsheet => "warning",
            WorkflowExecutorCategoryKind.Data => "accent",
            WorkflowExecutorCategoryKind.Markdown => "info",
            WorkflowExecutorCategoryKind.Human => "warning",
            WorkflowExecutorCategoryKind.Command => "danger",
            _ => "neutral"
        };

    public static string ResolveCategoryLabel(WorkflowExecutorCategoryKind category)
        => category switch
        {
            WorkflowExecutorCategoryKind.ProjectStructure => "Project structure",
            WorkflowExecutorCategoryKind.Http => "HTTP",
            WorkflowExecutorCategoryKind.Spreadsheet => "Spreadsheets",
            WorkflowExecutorCategoryKind.Data => "Data",
            WorkflowExecutorCategoryKind.Markdown => "Markdown",
            WorkflowExecutorCategoryKind.Human => "Human",
            WorkflowExecutorCategoryKind.Command => "Commands",
            WorkflowExecutorCategoryKind.Image => "Images",
            WorkflowExecutorCategoryKind.Storage => "Storage",
            _ => category.ToString()
        };

    public static string ResolveCategoryDescription(WorkflowExecutorCategoryKind category)
        => category switch
        {
            WorkflowExecutorCategoryKind.Storage => "Workspace storage reads, writes, searches, stats, and diffs.",
            WorkflowExecutorCategoryKind.ProjectStructure => "Project tree reads and typed asset creation.",
            WorkflowExecutorCategoryKind.Http => "Bounded HTTP and HTTPS calls.",
            WorkflowExecutorCategoryKind.Image => "Image generation and image-provider output.",
            WorkflowExecutorCategoryKind.Spreadsheet => "XLSX inspection, reading, writing, and Markdown extraction.",
            WorkflowExecutorCategoryKind.Data => "Structured payload transformations.",
            WorkflowExecutorCategoryKind.Markdown => "Markdown rendering and report assembly.",
            WorkflowExecutorCategoryKind.Human => "Human approvals and workflow pauses.",
            WorkflowExecutorCategoryKind.Command => "Bounded local process execution.",
            _ => "Workflow executor tools."
        };

    public static string ResolveCategoryIcon(WorkflowExecutorCategoryKind category)
        => category switch
        {
            WorkflowExecutorCategoryKind.Storage => "folder_open",
            WorkflowExecutorCategoryKind.ProjectStructure => "account_tree",
            WorkflowExecutorCategoryKind.Http => "public",
            WorkflowExecutorCategoryKind.Image => "image",
            WorkflowExecutorCategoryKind.Spreadsheet => "table_chart",
            WorkflowExecutorCategoryKind.Data => "data_object",
            WorkflowExecutorCategoryKind.Markdown => "article",
            WorkflowExecutorCategoryKind.Human => "approval",
            WorkflowExecutorCategoryKind.Command => "terminal",
            _ => "bolt"
        };

    private static string TrimMenuLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return "Tool";
        }

        var parts = label.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length <= 1 ? label.Trim() : parts[0];
    }
}
