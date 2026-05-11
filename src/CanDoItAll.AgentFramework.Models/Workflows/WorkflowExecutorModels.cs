using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

[JsonConverter(typeof(WorkflowExecutorIdJsonConverter))]
public readonly record struct WorkflowExecutorId
{
    public WorkflowExecutorId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Workflow executor id cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public static class WorkflowExecutorIds
{
    public static WorkflowExecutorId StorageFile { get; } = new("storage.file");

    public static WorkflowExecutorId ProjectStructure { get; } = new("project-structure");

    public static WorkflowExecutorId HttpFetch { get; } = new("http.fetch");

    public static WorkflowExecutorId ImageGeneration { get; } = new("image.generate");

    public static WorkflowExecutorId Spreadsheet { get; } = new("spreadsheet");

    public static WorkflowExecutorId JsonTransform { get; } = new("json.transform");

    public static WorkflowExecutorId MarkdownRender { get; } = new("markdown.render");

    public static WorkflowExecutorId Delay { get; } = new("utility.delay");

    public static WorkflowExecutorId ApprovalRequest { get; } = new("human.approval");

    public static WorkflowExecutorId CommandProcess { get; } = new("command.process");
}

public enum WorkflowExecutorCategoryKind
{
    Storage,
    ProjectStructure,
    Http,
    Image,
    Spreadsheet,
    Data,
    Markdown,
    Human,
    Utility,
    Command
}

public enum WorkflowStorageFileOperation
{
    List,
    Stat,
    ReadText,
    WriteText,
    AppendText,
    SearchText,
    DiffText
}

public enum WorkflowHttpMethodKind
{
    Get,
    Post,
    Put,
    Patch,
    Delete
}

public enum WorkflowSpreadsheetOperation
{
    WorkbookSummary,
    ReadCell,
    ReadRange,
    WriteCell,
    WriteRange,
    ApplyBatch,
    RangeToMarkdown
}

public enum WorkflowProjectStructureOperation
{
    ListProjects,
    ReadTree,
    ReadNode,
    CreateAsset
}

public enum WorkflowImageGenerationOperation
{
    Generate,
    Edit
}

public sealed record WorkflowExecutorExecutionPolicy(
    int TimeoutSeconds,
    int MaxRetryAttempts,
    int RetryDelayMilliseconds,
    bool CaptureOutputArtifact)
{
    public static WorkflowExecutorExecutionPolicy Default { get; } = new(
        TimeoutSeconds: 30,
        MaxRetryAttempts: 0,
        RetryDelayMilliseconds: 250,
        CaptureOutputArtifact: false);
}

public sealed record WorkflowExecutorDescriptor(
    WorkflowExecutorId Id,
    string Name,
    string Description,
    WorkflowExecutorCategoryKind Category,
    string IconName,
    string SetupRendererKey,
    WorkflowValueShape InputShape,
    WorkflowValueShape ResultShape,
    string SettingsSchemaJson,
    string DefaultSettingsJson,
    WorkflowExecutorExecutionPolicy DefaultPolicy,
    bool IsImplemented);

public sealed record WorkflowStorageFileExecutorSettings
{
    public WorkflowStorageFileOperation Operation { get; init; } = WorkflowStorageFileOperation.ReadText;

    public string Path { get; init; } = string.Empty;

    public string DestinationPath { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public string Query { get; init; } = string.Empty;

    public string SearchPattern { get; init; } = "*";

    public int MaxResults { get; init; } = 100;

    public int MaxCharacters { get; init; } = 12000;

    public int MaxLines { get; init; } = 160;

    public bool Overwrite { get; init; } = true;
}

public sealed record WorkflowHttpExecutorSettings
{
    public WorkflowHttpMethodKind Method { get; init; } = WorkflowHttpMethodKind.Get;

    public string Url { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    public string Body { get; init; } = string.Empty;

    public int MaxResponseBytes { get; init; } = 262144;
}

public sealed record WorkflowSpreadsheetCellWrite(string CellAddress, string Value);

public sealed record WorkflowSpreadsheetRangeWrite(
    string RangeAddress,
    IReadOnlyList<IReadOnlyList<string>> Values);

public sealed record WorkflowSpreadsheetExecutorSettings
{
    public WorkflowSpreadsheetOperation Operation { get; init; } = WorkflowSpreadsheetOperation.WorkbookSummary;

    public string WorkbookPath { get; init; } = string.Empty;

    public string OutputWorkbookPath { get; init; } = string.Empty;

    public string WorksheetName { get; init; } = string.Empty;

    public string CellAddress { get; init; } = string.Empty;

    public string RangeAddress { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public IReadOnlyList<WorkflowSpreadsheetCellWrite> CellWrites { get; init; } = [];

    public IReadOnlyList<WorkflowSpreadsheetRangeWrite> RangeWrites { get; init; } = [];

    public bool CreateWorkbookIfMissing { get; init; }

    public bool Overwrite { get; init; } = true;

    public int MaxRows { get; init; } = 100;

    public int MaxColumns { get; init; } = 40;
}

public sealed record WorkflowProjectStructureExecutorSettings
{
    public WorkflowProjectStructureOperation Operation { get; init; } = WorkflowProjectStructureOperation.ReadTree;

    public Guid? ProjectId { get; init; }

    public string NodeId { get; init; } = string.Empty;

    public string AssetKind { get; init; } = "md";

    public string Title { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public string SourceWorkspacePath { get; init; } = string.Empty;

    public string ContentType { get; init; } = "text/markdown";
}

public sealed record WorkflowImageGenerationExecutorSettings
{
    public WorkflowImageGenerationOperation Operation { get; init; } = WorkflowImageGenerationOperation.Generate;

    public string Prompt { get; init; } = string.Empty;

    public Guid? ProviderProfileId { get; init; }

    public string Model { get; init; } = string.Empty;

    public string Size { get; init; } = "1024x1024";

    public string Quality { get; init; } = "low";

    public string OutputFormat { get; init; } = "png";

    public string OutputWorkspacePath { get; init; } = string.Empty;
}
