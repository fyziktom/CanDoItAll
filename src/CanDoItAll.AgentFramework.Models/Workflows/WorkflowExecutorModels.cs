using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel.Configuration;

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

    public static WorkflowExecutorId SourceIngestion { get; } = new("source.ingest");

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

public enum WorkflowExecutorSourceKind
{
    BuiltIn,
    BundledPlugin,
    LocalPackage,
    RemotePackage
}

public enum WorkflowExecutorTrustLevel
{
    Application,
    BundledPlugin,
    LocalPackage,
    RemotePackage,
    Untrusted
}

public enum UiIconKind
{
    MaterialIcon,
    StaticAsset,
    PackageAsset
}

public sealed record UiIconDescriptor
{
    public UiIconDescriptor(
        UiIconKind kind,
        string value,
        string packageId = "",
        string label = "")
    {
        Kind = kind;
        Value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        PackageId = string.IsNullOrWhiteSpace(packageId) ? string.Empty : packageId.Trim();
        Label = string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim();
    }

    public UiIconKind Kind { get; init; }

    public string Value { get; init; }

    public string PackageId { get; init; }

    public string Label { get; init; }

    public static UiIconDescriptor MaterialIcon(
        string iconName,
        string label = "")
        => new(UiIconKind.MaterialIcon, iconName, label: label);

    public static UiIconDescriptor StaticAsset(
        string assetPath,
        string label = "")
        => new(UiIconKind.StaticAsset, assetPath, label: label);

    public static UiIconDescriptor PackageAsset(
        string packageId,
        string assetPath,
        string label = "")
        => new(UiIconKind.PackageAsset, assetPath, packageId, label);

    public static UiIconDescriptor Default { get; } = MaterialIcon("extension");
}

public enum WorkflowExecutorAvailabilityKind
{
    Available,
    Planned,
    Disabled,
    Unavailable,
    Incompatible
}

public enum WorkflowExecutorSettingsSchemaKind
{
    None,
    JsonSchema
}

public static class WorkflowExecutorSourceIds
{
    public const string BuiltIn = "candoitall.builtins";
}

public sealed record WorkflowExecutorSourceDescriptor
{
    public WorkflowExecutorSourceDescriptor(
        WorkflowExecutorSourceKind kind,
        string sourceId,
        string sourceVersion,
        string pluginId,
        string packageId,
        WorkflowExecutorTrustLevel trustLevel,
        string displayName = "",
        UiIconDescriptor? icon = null)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Workflow executor source id cannot be empty.", nameof(sourceId));
        }

        Kind = kind;
        SourceId = sourceId.Trim();
        SourceVersion = string.IsNullOrWhiteSpace(sourceVersion) ? string.Empty : sourceVersion.Trim();
        PluginId = string.IsNullOrWhiteSpace(pluginId) ? string.Empty : pluginId.Trim();
        PackageId = string.IsNullOrWhiteSpace(packageId) ? string.Empty : packageId.Trim();
        TrustLevel = trustLevel;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
        Icon = icon ?? UiIconDescriptor.Default;
    }

    public WorkflowExecutorSourceKind Kind { get; init; }

    public string SourceId { get; init; }

    public string SourceVersion { get; init; }

    public string PluginId { get; init; }

    public string PackageId { get; init; }

    public WorkflowExecutorTrustLevel TrustLevel { get; init; }

    public string DisplayName { get; init; }

    public UiIconDescriptor Icon { get; init; }

    public static WorkflowExecutorSourceDescriptor BuiltIn(string sourceVersion = "")
        => new(
            WorkflowExecutorSourceKind.BuiltIn,
            WorkflowExecutorSourceIds.BuiltIn,
            sourceVersion,
            pluginId: string.Empty,
            packageId: string.Empty,
            WorkflowExecutorTrustLevel.Application,
            displayName: "Built-in",
            UiIconDescriptor.MaterialIcon("bolt", "Built-in executor"));

    public static WorkflowExecutorSourceDescriptor BundledPlugin(
        string pluginId,
        string sourceVersion,
        string displayName = "",
        UiIconDescriptor? icon = null)
        => new(
            WorkflowExecutorSourceKind.BundledPlugin,
            pluginId,
            sourceVersion,
            pluginId,
            packageId: string.Empty,
            WorkflowExecutorTrustLevel.BundledPlugin,
            displayName,
            icon);

    public static WorkflowExecutorSourceDescriptor Package(
        WorkflowExecutorSourceKind kind,
        string pluginId,
        string packageId,
        string sourceVersion,
        WorkflowExecutorTrustLevel trustLevel,
        string displayName,
        UiIconDescriptor icon)
        => new(
            kind,
            string.IsNullOrWhiteSpace(packageId) ? pluginId : packageId,
            sourceVersion,
            pluginId,
            packageId,
            trustLevel,
            displayName,
            icon);
}

public sealed record WorkflowExecutorAvailabilityDescriptor
{
    public WorkflowExecutorAvailabilityDescriptor(
        WorkflowExecutorAvailabilityKind kind,
        bool isRunnable,
        string reasonCode,
        string message)
    {
        Kind = kind;
        IsRunnable = isRunnable;
        ReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? string.Empty : reasonCode.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
    }

    public WorkflowExecutorAvailabilityKind Kind { get; init; }

    public bool IsRunnable { get; init; }

    public string ReasonCode { get; init; }

    public string Message { get; init; }

    public static WorkflowExecutorAvailabilityDescriptor Available()
        => new(
            WorkflowExecutorAvailabilityKind.Available,
            isRunnable: true,
            reasonCode: string.Empty,
            message: "Executor is available.");

    public static WorkflowExecutorAvailabilityDescriptor Planned(string message)
        => new(
            WorkflowExecutorAvailabilityKind.Planned,
            isRunnable: false,
            reasonCode: "planned",
            message: message);

    public static WorkflowExecutorAvailabilityDescriptor Disabled(string message)
        => new(
            WorkflowExecutorAvailabilityKind.Disabled,
            isRunnable: false,
            reasonCode: "disabled",
            message: message);

    public static WorkflowExecutorAvailabilityDescriptor Unavailable(string reasonCode, string message)
        => new(
            WorkflowExecutorAvailabilityKind.Unavailable,
            isRunnable: false,
            reasonCode,
            message);

    public static WorkflowExecutorAvailabilityDescriptor Incompatible(string message)
        => new(
            WorkflowExecutorAvailabilityKind.Incompatible,
            isRunnable: false,
            reasonCode: "incompatible",
            message: message);
}

public sealed record WorkflowExecutorSettingsSchemaDescriptor
{
    public WorkflowExecutorSettingsSchemaDescriptor(
        WorkflowExecutorSettingsSchemaKind kind,
        string version,
        string schemaJson)
    {
        Kind = kind;
        Version = string.IsNullOrWhiteSpace(version) ? string.Empty : version.Trim();
        SchemaJson = string.IsNullOrWhiteSpace(schemaJson) ? string.Empty : schemaJson.Trim();
    }

    public WorkflowExecutorSettingsSchemaKind Kind { get; init; }

    public string Version { get; init; }

    public string SchemaJson { get; init; }

    public bool HasSchema => Kind != WorkflowExecutorSettingsSchemaKind.None && !string.IsNullOrWhiteSpace(SchemaJson);

    public static WorkflowExecutorSettingsSchemaDescriptor None()
        => new(WorkflowExecutorSettingsSchemaKind.None, version: string.Empty, schemaJson: string.Empty);

    public static WorkflowExecutorSettingsSchemaDescriptor JsonSchema(
        string version,
        string schemaJson)
        => new(WorkflowExecutorSettingsSchemaKind.JsonSchema, version, schemaJson);
}

public enum WorkflowStorageFileOperation
{
    List,
    Exists,
    Tree,
    Stat,
    ReadText,
    WriteText,
    AppendText,
    CreateDirectory,
    Delete,
    Copy,
    Move,
    Hash,
    Zip,
    Unzip,
    SearchText,
    DiffText
}

public enum WorkflowJsonTransformOperation
{
    Select,
    Set,
    Remove,
    Merge,
    Count,
    Template,
    ArrayMap,
    ArrayFilter,
    ArraySort,
    ArrayDistinct,
    ArrayTake,
    ValidateSchema
}

public enum WorkflowMarkdownMissingPlaceholderBehavior
{
    Fail,
    Empty
}

public enum WorkflowHttpMethodKind
{
    Get,
    Post,
    Put,
    Patch,
    Delete
}

public enum WorkflowHttpSecretValueFormat
{
    Raw,
    Bearer,
    Basic,
    CustomPrefix
}

public static class WorkflowSecretPurposes
{
    public const string HttpHeader = "workflow-http-header";
}

public sealed record WorkflowHttpSecretHeaderBinding
{
    public Guid? SecretId { get; init; }

    public string SecretNameSnapshot { get; init; } = string.Empty;

    public string Purpose { get; init; } = WorkflowSecretPurposes.HttpHeader;

    public string HeaderName { get; init; } = "Authorization";

    public WorkflowHttpSecretValueFormat ValueFormat { get; init; } = WorkflowHttpSecretValueFormat.Bearer;

    public string CustomPrefix { get; init; } = string.Empty;
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
    CreateAsset,
    CreateTaskNodes
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

public sealed record WorkflowExecutorSimulationDescriptor(
    bool SupportsPreviewSimulation,
    string OutputTemplateJson,
    string Description)
{
    public static WorkflowExecutorSimulationDescriptor None { get; } = new(
        SupportsPreviewSimulation: false,
        OutputTemplateJson: string.Empty,
        Description: string.Empty);

    public static WorkflowExecutorSimulationDescriptor JsonTemplate(
        string outputTemplateJson,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputTemplateJson);

        return new WorkflowExecutorSimulationDescriptor(
            SupportsPreviewSimulation: true,
            OutputTemplateJson: outputTemplateJson.Trim(),
            Description: string.IsNullOrWhiteSpace(description) ? "Simulate this workflow executor output." : description.Trim());
    }
}

[Flags]
public enum WorkflowExecutorCapabilityFlags
{
    None = 0,
    ReadsWorkspace = 1 << 0,
    WritesWorkspace = 1 << 1,
    ReadsExternalData = 1 << 2,
    WritesExternalData = 1 << 3,
    UsesNetwork = 1 << 4,
    UsesSecrets = 1 << 5,
    RunsHostCommand = 1 << 6,
    EmitsArtifacts = 1 << 7,
    SupportsDeterministicTestMode = 1 << 8,
    IdempotentExternalMarker = 1 << 9
}

public enum WorkflowExecutorApprovalRequirement
{
    NotRequired,
    RequiredForExternalEffect,
    AlwaysRequired
}

public sealed record WorkflowExecutorPermissionPolicy(
    WorkflowExecutorCapabilityFlags RequiredCapabilities,
    WorkflowExecutorApprovalRequirement ApprovalRequirement)
{
    public bool RequiresApproval => ApprovalRequirement != WorkflowExecutorApprovalRequirement.NotRequired;

    public static WorkflowExecutorPermissionPolicy None { get; } = new(
        WorkflowExecutorCapabilityFlags.None,
        WorkflowExecutorApprovalRequirement.NotRequired);
}

public sealed record WorkflowExecutorDeterministicTestModeDescriptor(
    bool IsSupported,
    string Description)
{
    public static WorkflowExecutorDeterministicTestModeDescriptor None { get; } = new(
        IsSupported: false,
        Description: string.Empty);

    public static WorkflowExecutorDeterministicTestModeDescriptor Supported(string description)
        => new(
            IsSupported: true,
            Description: string.IsNullOrWhiteSpace(description)
                ? "Executor can run with deterministic fake or preview inputs."
                : description.Trim());
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
    bool IsImplemented)
{
    public WorkflowExecutorSourceDescriptor Source { get; init; } = WorkflowExecutorSourceDescriptor.BuiltIn();

    public WorkflowExecutorAvailabilityDescriptor Availability { get; init; } = IsImplemented
        ? WorkflowExecutorAvailabilityDescriptor.Available()
        : WorkflowExecutorAvailabilityDescriptor.Planned("Executor is planned but not implemented.");

    public WorkflowExecutorSettingsSchemaDescriptor SettingsSchema { get; init; } =
        WorkflowExecutorSettingsSchemaDescriptor.JsonSchema("1.0", SettingsSchemaJson);

    public ConfigurationSchema ConfigurationSchema { get; init; } = ConfigurationSchema.Empty();

    public WorkflowExecutorSimulationDescriptor Simulation { get; init; } = WorkflowExecutorSimulationDescriptor.None;

    public WorkflowExecutorPermissionPolicy PermissionPolicy { get; init; } = WorkflowExecutorPermissionPolicy.None;

    public WorkflowExecutorDeterministicTestModeDescriptor DeterministicTestMode { get; init; } = WorkflowExecutorDeterministicTestModeDescriptor.None;

    public bool CanExecute => IsImplemented && Availability.IsRunnable;
}

public sealed record WorkflowStorageFileExecutorSettings
{
    public WorkflowStorageFileOperation Operation { get; init; } = WorkflowStorageFileOperation.ReadText;

    public string Path { get; init; } = string.Empty;

    public string DestinationPath { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public bool ContentFromInput { get; init; }

    public string Query { get; init; } = string.Empty;

    public string SearchPattern { get; init; } = "*";

    public IReadOnlyList<string> IncludeGlobs { get; init; } = [];

    public IReadOnlyList<string> ExcludeGlobs { get; init; } = [];

    public int MaxResults { get; init; } = 100;

    public int MaxFiles { get; init; } = 200;

    public long MaxBytes { get; init; } = 10 * 1024 * 1024;

    public int MaxCharacters { get; init; } = 12000;

    public int MaxLines { get; init; } = 160;

    public bool Overwrite { get; init; } = true;

    public bool Recursive { get; init; }

    public bool DryRun { get; init; }
}

public sealed record WorkflowJsonTransformStep
{
    public WorkflowJsonTransformOperation Operation { get; init; } = WorkflowJsonTransformOperation.Select;

    public string Path { get; init; } = "$";

    public string DestinationPath { get; init; } = "$";

    public string ValueJson { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string PredicatePath { get; init; } = string.Empty;

    public string ExpectedValueJson { get; init; } = string.Empty;

    public int Take { get; init; }

    public IReadOnlyDictionary<string, string> Template { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<string> RequiredPaths { get; init; } = [];
}

public sealed record WorkflowJsonTransformExecutorSettings
{
    public IReadOnlyList<WorkflowJsonTransformStep> Operations { get; init; } = [];

    public int MaxOutputCharacters { get; init; } = 500000;
}

public sealed record WorkflowMarkdownTableBinding
{
    public string JsonPath { get; init; } = "$";

    public string Placeholder { get; init; } = string.Empty;

    public IReadOnlyList<string> Columns { get; init; } = [];
}

public sealed record WorkflowMarkdownRenderExecutorSettings
{
    public string Template { get; init; } = string.Empty;

    public string TemplatePath { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Bindings { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<WorkflowMarkdownTableBinding> Tables { get; init; } = [];

    public string OutputPath { get; init; } = string.Empty;

    public bool Append { get; init; }

    public bool Overwrite { get; init; } = true;

    public WorkflowMarkdownMissingPlaceholderBehavior MissingPlaceholderBehavior { get; init; } = WorkflowMarkdownMissingPlaceholderBehavior.Fail;
}

public sealed record WorkflowDelayExecutorSettings
{
    public int DelayMilliseconds { get; init; } = 1000;

    public int MaxDelayMilliseconds { get; init; } = 30000;
}

public sealed record WorkflowApprovalExecutorSettings
{
    public string Prompt { get; init; } = string.Empty;

    public bool IncludeInputPayload { get; init; } = true;
}

public sealed record WorkflowSourceIngestionExecutorSettings
{
    public IReadOnlyList<string> SourceKeys { get; init; } = [];

    public IReadOnlyList<string> AllowedExtensions { get; init; } =
    [
        ".md",
        ".txt",
        ".eml",
        ".csv",
        ".html",
        ".htm",
        ".json",
        ".pdf",
        ".docx",
        ".zip",
        ".xls",
        ".xlsx"
    ];

    public bool IncludeAdditionalSources { get; init; } = true;

    public bool IncludeParentNodePath { get; init; } = true;

    public bool IncludeSelectedNodePaths { get; init; } = true;

    public bool IncludeParentSubtreePaths { get; init; } = true;

    public bool RecursiveFolders { get; init; } = true;

    public bool AllowAbsoluteInputPaths { get; init; }

    public int MaxFiles { get; init; } = 12;

    public int MaxCharactersPerFile { get; init; } = 12000;

    public int MaxTotalCharacters { get; init; } = 60000;
}

public sealed record WorkflowHttpExecutorSettings
{
    public WorkflowHttpMethodKind Method { get; init; } = WorkflowHttpMethodKind.Get;

    public string Url { get; init; } = string.Empty;

    public string UrlJsonPath { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    public WorkflowHttpSecretHeaderBinding SecretHeader { get; init; } = new();

    public string Body { get; init; } = string.Empty;

    public int MaxResponseBytes { get; init; } = 262144;

    public bool IncludeInputPayload { get; init; }

    public bool AllowPrivateNetworkTargets { get; init; }

    public bool DownloadToWorkspace { get; init; }

    public string OutputPath { get; init; } = string.Empty;

    public bool Overwrite { get; init; } = true;
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

    public string ProjectIdJsonPath { get; init; } = string.Empty;

    public string NodeId { get; init; } = string.Empty;

    public string NodeIdJsonPath { get; init; } = string.Empty;

    public string AssetKind { get; init; } = "md";

    public string Title { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public bool ContentFromInput { get; init; }

    public bool IncludeInputPayload { get; init; }

    public string SourceWorkspacePath { get; init; } = string.Empty;

    public string ContentType { get; init; } = "text/markdown";

    public string TaskItemsJsonPath { get; init; } = "$.tasks";

    public string TaskObjectSubtype { get; init; } = "task";

    public int MaxTaskNodes { get; init; } = 20;

    public string IdempotencyKey { get; init; } = string.Empty;

    public string IdempotencyKeyJsonPath { get; init; } = string.Empty;

    public string IdempotencyKeySuffix { get; init; } = string.Empty;
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
