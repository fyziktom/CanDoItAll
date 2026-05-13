using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

public static class BuiltInWorkflowExecutorDescriptors
{
    private static readonly WorkflowValueShape JsonShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "JSON payload");

    public static WorkflowExecutorDescriptor StorageFile { get; } = Create(
        WorkflowExecutorIds.StorageFile,
        "Workspace files",
        "Lists, reads, writes, appends, searches, stats, and diffs files through the workspace storage boundary.",
        WorkflowExecutorCategoryKind.Storage,
        "folder_open",
        "builtin.storage-file",
        new WorkflowStorageFileExecutorSettings());

    public static WorkflowExecutorDescriptor SourceIngestion { get; } = Create(
        WorkflowExecutorIds.SourceIngestion,
        "Source ingestion",
        "Loads explicit project-structure workflow file and folder sources into bounded text for downstream LLM nodes.",
        WorkflowExecutorCategoryKind.Data,
        "drive_folder_upload",
        "builtin.source-ingest",
        new WorkflowSourceIngestionExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 90, CaptureOutputArtifact = true });

    public static WorkflowExecutorDescriptor HttpFetch { get; } = Create(
        WorkflowExecutorIds.HttpFetch,
        "HTTP fetch",
        "Fetches bounded HTTP/HTTPS content with explicit method, headers, body, and size settings.",
        WorkflowExecutorCategoryKind.Http,
        "public",
        "builtin.http-fetch",
        new WorkflowHttpExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 20 });

    public static WorkflowExecutorDescriptor Spreadsheet { get; } = Create(
        WorkflowExecutorIds.Spreadsheet,
        "Spreadsheet",
        "Inspects, reads, writes, and Markdown-renders XLSX workbooks through the document wrapper.",
        WorkflowExecutorCategoryKind.Spreadsheet,
        "table_chart",
        "builtin.spreadsheet",
        new WorkflowSpreadsheetExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 });

    public static WorkflowExecutorDescriptor ProjectStructure { get; } = Create(
        WorkflowExecutorIds.ProjectStructure,
        "Project structure",
        "Reads project structures and creates typed asset and task nodes through the project-structure service.",
        WorkflowExecutorCategoryKind.ProjectStructure,
        "account_tree",
        "builtin.project-structure",
        new WorkflowProjectStructureExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 45 });

    public static WorkflowExecutorDescriptor ImageGeneration { get; } = Create(
        WorkflowExecutorIds.ImageGeneration,
        "Image generation",
        "Prepares image generation through configured image providers and managed workspace output.",
        WorkflowExecutorCategoryKind.Image,
        "image",
        "builtin.image-generation",
        new WorkflowImageGenerationExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 120, CaptureOutputArtifact = true });

    public static IReadOnlyList<WorkflowExecutorDescriptor> Planned { get; } =
    [
        CreatePlanned(WorkflowExecutorIds.JsonTransform, "JSON transform", "Transforms JSON using a typed projection expression.", WorkflowExecutorCategoryKind.Data, "data_object", "planned.json-transform"),
        CreatePlanned(WorkflowExecutorIds.MarkdownRender, "Markdown render", "Builds Markdown from structured workflow values.", WorkflowExecutorCategoryKind.Markdown, "article", "planned.markdown-render"),
        CreatePlanned(WorkflowExecutorIds.Delay, "Delay", "Waits or schedules a workflow continuation.", WorkflowExecutorCategoryKind.Utility, "timer", "planned.delay"),
        CreatePlanned(WorkflowExecutorIds.ApprovalRequest, "Approval request", "Creates a human approval/request node during workflow execution.", WorkflowExecutorCategoryKind.Human, "approval", "planned.approval-request"),
        CreatePlanned(WorkflowExecutorIds.CommandProcess, "Command process", "Runs a bounded local process through the existing workspace command service.", WorkflowExecutorCategoryKind.Command, "terminal", "planned.command-process")
    ];

    private static WorkflowExecutorDescriptor Create<TSettings>(
        WorkflowExecutorId id,
        string name,
        string description,
        WorkflowExecutorCategoryKind category,
        string iconName,
        string setupRendererKey,
        TSettings defaultSettings,
        WorkflowExecutorExecutionPolicy? defaultPolicy = null)
    {
        return new WorkflowExecutorDescriptor(
            id,
            name,
            description,
            category,
            iconName,
            setupRendererKey,
            WorkflowValueShape.Text,
            JsonShape,
            "{\"type\":\"object\"}",
            WorkflowExecutorJson.Serialize(defaultSettings),
            defaultPolicy ?? WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true);
    }

    private static WorkflowExecutorDescriptor CreatePlanned(
        WorkflowExecutorId id,
        string name,
        string description,
        WorkflowExecutorCategoryKind category,
        string iconName,
        string setupRendererKey)
    {
        return new WorkflowExecutorDescriptor(
            id,
            name,
            description,
            category,
            iconName,
            setupRendererKey,
            WorkflowValueShape.Text,
            JsonShape,
            "{\"type\":\"object\"}",
            "{}",
            WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: false);
    }
}

