using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Identifier of a workflow executor, written as a non-empty string such as <c>storage.file</c>, <c>http.fetch</c> or
/// <c>human.approval</c>; surrounding whitespace is removed. Built-in and plugin executors and their identifiers are
/// listed by <c>GET /api/workflows/executor-catalog</c>. Readers also accept an object with a <c>value</c> member;
/// send the plain string.
/// </summary>
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
    public static WorkflowExecutorId Memory { get; } = new("memory.operation");

    public static WorkflowExecutorId StorageFile { get; } = new("storage.file");

    public static WorkflowExecutorId SourceIngestion { get; } = new("source.ingest");

    public static WorkflowExecutorId ProjectStructure { get; } = new("project-structure");

    public static WorkflowExecutorId HttpFetch { get; } = new("http.fetch");

    public static WorkflowExecutorId ImageGeneration { get; } = new("image.generate");

    public static WorkflowExecutorId DocumentToMarkdown { get; } = new("document.to-markdown");

    public static WorkflowExecutorId ImageInspect { get; } = new("image.inspect");

    public static WorkflowExecutorId ImageAnalyze { get; } = new("image.analyze");

    public static WorkflowExecutorId Spreadsheet { get; } = new("spreadsheet");

    public static WorkflowExecutorId JsonTransform { get; } = new("json.transform");

    public static WorkflowExecutorId MarkdownRender { get; } = new("markdown.render");

    public static WorkflowExecutorId Delay { get; } = new("utility.delay");

    public static WorkflowExecutorId ApprovalRequest { get; } = new("human.approval");

    public static WorkflowExecutorId CommandProcess { get; } = new("command.process");
}

/// <summary>
/// Category of a workflow executor, as a JSON integer: 0 Storage, 1 ProjectStructure, 2 Http, 3 Image,
/// 4 Spreadsheet, 5 Data, 6 Markdown, 7 Human, 8 Utility, 9 Command.
/// </summary>
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

/// <summary>
/// Where a workflow executor comes from, as a JSON integer: 0 BuiltIn (part of the application), 1 BundledPlugin
/// (a plugin shipped with the application), 2 LocalPackage, 3 RemotePackage (plugin packages installed from a local
/// or remote source).
/// </summary>
public enum WorkflowExecutorSourceKind
{
    BuiltIn,
    BundledPlugin,
    LocalPackage,
    RemotePackage
}

/// <summary>
/// Trust level of a workflow executor's source, as a JSON integer: 0 Application, 1 BundledPlugin, 2 LocalPackage,
/// 3 RemotePackage, 4 Untrusted.
/// </summary>
public enum WorkflowExecutorTrustLevel
{
    Application,
    BundledPlugin,
    LocalPackage,
    RemotePackage,
    Untrusted
}

/// <summary>
/// How an icon is identified, as a JSON integer: 0 MaterialIcon (<c>value</c> is a Material icon name), 1 StaticAsset
/// (<c>value</c> is the path of an application image asset), 2 PackageAsset (<c>value</c> is a path inside the plugin
/// package named by <c>packageId</c>, whose icon <c>GET /api/plugins/packages/{packageId}/icon</c> serves).
/// </summary>
public enum UiIconKind
{
    MaterialIcon,
    StaticAsset,
    PackageAsset
}

/// <summary>
/// Icon shown for an executor or its source in the user interface.
/// </summary>
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

    /// <summary>
    /// How the icon is identified, as a JSON integer: 0 MaterialIcon, 1 StaticAsset, 2 PackageAsset.
    /// </summary>
    public UiIconKind Kind { get; init; }

    /// <summary>
    /// The Material icon name, for example <c>extension</c>, or the asset path, depending on <c>kind</c>; trimmed.
    /// </summary>
    public string Value { get; init; }

    /// <summary>Plugin package that holds a PackageAsset icon; empty for the other kinds.</summary>
    public string PackageId { get; init; }

    /// <summary>Accessible label of the icon; may be empty.</summary>
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

/// <summary>
/// Availability of a workflow executor, as a JSON integer: 0 Available, 1 Planned (not implemented yet), 2 Disabled,
/// 3 Unavailable (for example missing configuration or a dependency), 4 Incompatible.
/// </summary>
public enum WorkflowExecutorAvailabilityKind
{
    Available,
    Planned,
    Disabled,
    Unavailable,
    Incompatible
}

/// <summary>
/// Kind of an executor settings schema, as a JSON integer: 0 None, 1 JsonSchema.
/// </summary>
public enum WorkflowExecutorSettingsSchemaKind
{
    None,
    JsonSchema
}

/// <summary>
/// How the workflow editor presents an executor's settings, as a JSON integer: 0 Schema (a form generated from the
/// configuration schema), 1 CustomRenderer (a dedicated settings editor of the application).
/// </summary>
public enum WorkflowExecutorSettingsPresentationMode
{
    Schema,
    CustomRenderer
}

public static class WorkflowExecutorSourceIds
{
    public const string BuiltIn = "candoitall.builtins";
}

/// <summary>
/// Origin of a workflow executor: built into the application or provided by a plugin or plugin package.
/// </summary>
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

    /// <summary>
    /// Kind of source, as a JSON integer: 0 BuiltIn, 1 BundledPlugin, 2 LocalPackage, 3 RemotePackage.
    /// </summary>
    public WorkflowExecutorSourceKind Kind { get; init; }

    /// <summary>
    /// Identifier of the source: <c>candoitall.builtins</c> for built-in executors, otherwise the plugin or package
    /// identifier.
    /// </summary>
    public string SourceId { get; init; }

    /// <summary>Version of the source; may be empty.</summary>
    public string SourceVersion { get; init; }

    /// <summary>Identifier of the plugin that provides the executor; empty for built-in executors.</summary>
    public string PluginId { get; init; }

    /// <summary>Identifier of the plugin package; empty when the executor does not come from a package.</summary>
    public string PackageId { get; init; }

    /// <summary>
    /// Trust level of the source, as a JSON integer: 0 Application, 1 BundledPlugin, 2 LocalPackage, 3 RemotePackage,
    /// 4 Untrusted.
    /// </summary>
    public WorkflowExecutorTrustLevel TrustLevel { get; init; }

    /// <summary>Display name of the source, for example <c>Built-in</c>; may be empty.</summary>
    public string DisplayName { get; init; }

    /// <summary>Icon of the source.</summary>
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

/// <summary>
/// Whether a workflow executor can run in this host now, evaluated when the executor catalog is read.
/// </summary>
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

    /// <summary>
    /// Availability, as a JSON integer: 0 Available, 1 Planned, 2 Disabled, 3 Unavailable, 4 Incompatible.
    /// </summary>
    public WorkflowExecutorAvailabilityKind Kind { get; init; }

    /// <summary>True when the executor can run now.</summary>
    public bool IsRunnable { get; init; }

    /// <summary>
    /// Short machine-readable reason when the executor cannot run, for example <c>planned</c> or <c>disabled</c>;
    /// empty when it is available.
    /// </summary>
    public string ReasonCode { get; init; }

    /// <summary>Human-readable explanation of the availability.</summary>
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

/// <summary>
/// Versioned JSON Schema of an executor's settings.
/// </summary>
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

    /// <summary>Kind of schema, as a JSON integer: 0 None, 1 JsonSchema.</summary>
    public WorkflowExecutorSettingsSchemaKind Kind { get; init; }

    /// <summary>Version of the schema, for example <c>1.0</c>; may be empty.</summary>
    public string Version { get; init; }

    /// <summary>The JSON Schema as JSON text in a string; empty when there is none.</summary>
    public string SchemaJson { get; init; }

    /// <summary>True when a schema is present. Computed.</summary>
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
    DiffText,
    ListDirectory
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
    RangeToMarkdown,
    Preview
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

/// <summary>
/// Timeout and retry policy for running a workflow executor: an executor's default policy in the executor catalog and
/// plugin manifests, or a node's own policy. Values outside the limits make a definition fail validation.
/// </summary>
/// <param name="TimeoutSeconds">Maximum duration of one attempt, in seconds, from 1 through 3,600.</param>
/// <param name="MaxRetryAttempts">
/// Number of retries after a failed attempt, from 0 through 10. More than 0 is rejected for executors that write
/// external state unless their side effects allow idempotent retries.
/// </param>
/// <param name="RetryDelayMilliseconds">Delay before a retry, in milliseconds, from 0 through 600,000.</param>
/// <param name="CaptureOutputArtifact">
/// Whether the executor's output should be captured as an artifact; recorded in the execution audit. Node outputs are
/// stored according to the workflow artifact policy.
/// </param>
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

/// <summary>
/// Whether and how a preview (test) run can simulate a workflow executor instead of running it.
/// </summary>
/// <param name="SupportsPreviewSimulation">True when a preview run can replace the executor with a template.</param>
/// <param name="OutputTemplateJson">
/// Template of the simulated output as JSON text in a string, which may contain placeholders such as
/// <c>{{utcNow}}</c>, <c>{{node.id}}</c> or <c>{{inputPayload}}</c> that are filled in when a run simulates the node;
/// use it as <c>outputTemplateJson</c> of a preview simulation step. Empty when simulation is not supported.
/// </param>
/// <param name="Description">What the simulation produces; empty when simulation is not supported.</param>
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

/// <summary>
/// Strongest side effect of a workflow executor, as a JSON integer: 0 None, 1 WorkspaceRead, 2 WorkspaceWrite,
/// 3 ExternalRead, 4 ExternalWrite (changes state outside the host, such as a remote system).
/// </summary>
public enum WorkflowExecutorSideEffectKind
{
    None,
    WorkspaceRead,
    WorkspaceWrite,
    ExternalRead,
    ExternalWrite
}

/// <summary>
/// Kind of external change a workflow executor makes, as a JSON integer: 0 None, 1 ProcessedMarker (marks an external
/// item, such as a message, as processed).
/// </summary>
public enum WorkflowExecutorExternalMutationKind
{
    None,
    ProcessedMarker
}

/// <summary>
/// Side-effect contract of a workflow executor: what it changes and how it can be previewed, committed and retried.
/// </summary>
/// <param name="Kind">
/// Strongest side effect, as a JSON integer: 0 None, 1 WorkspaceRead, 2 WorkspaceWrite, 3 ExternalRead,
/// 4 ExternalWrite.
/// </param>
/// <param name="ExternalMutationKind">
/// Kind of external change, as a JSON integer: 0 None, 1 ProcessedMarker.
/// </param>
/// <param name="SupportsPreview">True when the executor can report what it would do without doing it.</param>
/// <param name="SupportsDryRun">True when the executor can run without committing its effect.</param>
/// <param name="SupportsCommit">True when the executor commits its effect in a separate, explicit step.</param>
/// <param name="RequiresCommitIdempotencyKey">
/// True when committing requires an idempotency key, found at <c>idempotencyKeyJsonPath</c>.
/// </param>
/// <param name="AllowsIdempotentRetry">True when repeating the executor cannot repeat its external effect.</param>
/// <param name="IdempotencyKeyJsonPath">JSON path of the idempotency key in the executor input; empty for none.</param>
/// <param name="ReceiptSchema">
/// Identifier of the schema of the receipt the executor records, for example <c>workflow-email-external-read/v1</c>;
/// empty for none.
/// </param>
public sealed record WorkflowExecutorSideEffectDescriptor(
    WorkflowExecutorSideEffectKind Kind,
    WorkflowExecutorExternalMutationKind ExternalMutationKind,
    bool SupportsPreview,
    bool SupportsDryRun,
    bool SupportsCommit,
    bool RequiresCommitIdempotencyKey,
    bool AllowsIdempotentRetry,
    string IdempotencyKeyJsonPath,
    string ReceiptSchema)
{
    /// <summary>True when <c>kind</c> is ExternalWrite. Computed.</summary>
    public bool WritesExternalState => Kind == WorkflowExecutorSideEffectKind.ExternalWrite;

    public static WorkflowExecutorSideEffectDescriptor None { get; } = new(
        WorkflowExecutorSideEffectKind.None,
        WorkflowExecutorExternalMutationKind.None,
        SupportsPreview: false,
        SupportsDryRun: false,
        SupportsCommit: false,
        RequiresCommitIdempotencyKey: false,
        AllowsIdempotentRetry: false,
        IdempotencyKeyJsonPath: string.Empty,
        ReceiptSchema: string.Empty);

    public static WorkflowExecutorSideEffectDescriptor ExternalRead(
        string receiptSchema = "")
        => new(
            WorkflowExecutorSideEffectKind.ExternalRead,
            WorkflowExecutorExternalMutationKind.None,
            SupportsPreview: true,
            SupportsDryRun: true,
            SupportsCommit: true,
            RequiresCommitIdempotencyKey: false,
            AllowsIdempotentRetry: true,
            IdempotencyKeyJsonPath: string.Empty,
            ReceiptSchema: string.IsNullOrWhiteSpace(receiptSchema) ? string.Empty : receiptSchema.Trim());

    public static WorkflowExecutorSideEffectDescriptor ExternalWrite(
        WorkflowExecutorExternalMutationKind mutationKind,
        bool requiresCommitIdempotencyKey,
        bool allowsIdempotentRetry,
        string idempotencyKeyJsonPath,
        string receiptSchema)
        => new(
            WorkflowExecutorSideEffectKind.ExternalWrite,
            mutationKind,
            SupportsPreview: true,
            SupportsDryRun: true,
            SupportsCommit: true,
            requiresCommitIdempotencyKey,
            allowsIdempotentRetry,
            string.IsNullOrWhiteSpace(idempotencyKeyJsonPath) ? string.Empty : idempotencyKeyJsonPath.Trim(),
            string.IsNullOrWhiteSpace(receiptSchema) ? string.Empty : receiptSchema.Trim());

    public static WorkflowExecutorSideEffectDescriptor IdempotentProcessedMarker(
        string idempotencyKeyJsonPath,
        string receiptSchema)
        => ExternalWrite(
            WorkflowExecutorExternalMutationKind.ProcessedMarker,
            requiresCommitIdempotencyKey: true,
            allowsIdempotentRetry: true,
            idempotencyKeyJsonPath,
            receiptSchema);
}

/// <summary>
/// Capabilities a workflow executor needs, as a JSON integer bit mask that adds these values: 0 None,
/// 1 ReadsWorkspace, 2 WritesWorkspace, 4 ReadsExternalData, 8 WritesExternalData, 16 UsesNetwork, 32 UsesSecrets,
/// 64 RunsHostCommand, 128 EmitsArtifacts, 256 SupportsDeterministicTestMode, 512 IdempotentExternalMarker. For
/// example 17 means ReadsWorkspace and UsesNetwork.
/// </summary>
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

/// <summary>
/// Whether running a workflow executor needs a person's approval, as a JSON integer: 0 NotRequired,
/// 1 RequiredForExternalEffect, 2 AlwaysRequired. With either of the last two, a run raises an approval request that
/// must be approved before the executor runs.
/// </summary>
public enum WorkflowExecutorApprovalRequirement
{
    NotRequired,
    RequiredForExternalEffect,
    AlwaysRequired
}

/// <summary>
/// Permissions a workflow executor needs and whether running it needs approval.
/// </summary>
/// <param name="RequiredCapabilities">
/// Capabilities the executor needs, as a JSON integer bit mask: 0 None, 1 ReadsWorkspace, 2 WritesWorkspace,
/// 4 ReadsExternalData, 8 WritesExternalData, 16 UsesNetwork, 32 UsesSecrets, 64 RunsHostCommand, 128 EmitsArtifacts,
/// 256 SupportsDeterministicTestMode, 512 IdempotentExternalMarker.
/// </param>
/// <param name="ApprovalRequirement">
/// Whether a run needs approval before the executor runs, as a JSON integer: 0 NotRequired,
/// 1 RequiredForExternalEffect, 2 AlwaysRequired.
/// </param>
public sealed record WorkflowExecutorPermissionPolicy(
    WorkflowExecutorCapabilityFlags RequiredCapabilities,
    WorkflowExecutorApprovalRequirement ApprovalRequirement)
{
    /// <summary>True when <c>approvalRequirement</c> is not NotRequired. Computed.</summary>
    public bool RequiresApproval => ApprovalRequirement != WorkflowExecutorApprovalRequirement.NotRequired;

    public static WorkflowExecutorPermissionPolicy None { get; } = new(
        WorkflowExecutorCapabilityFlags.None,
        WorkflowExecutorApprovalRequirement.NotRequired);
}

/// <summary>
/// Whether a workflow executor can run deterministically with fake or preview inputs for tests.
/// </summary>
/// <param name="IsSupported">True when the executor supports a deterministic test mode.</param>
/// <param name="Description">How the test mode behaves; empty when it is not supported.</param>
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

/// <summary>
/// A workflow executor that Executor nodes can run, as listed by <c>GET /api/workflows/executor-catalog</c>: what it
/// does, how it is configured, what it may change, whether it needs approval and whether it can run now.
/// </summary>
/// <param name="Id">Identifier to put in <c>settings.executorId</c> of a node, for example <c>storage.file</c>.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">What the executor does.</param>
/// <param name="Category">
/// Category, as a JSON integer: 0 Storage, 1 ProjectStructure, 2 Http, 3 Image, 4 Spreadsheet, 5 Data, 6 Markdown,
/// 7 Human, 8 Utility, 9 Command.
/// </param>
/// <param name="IconName">Material icon name shown for the executor.</param>
/// <param name="SetupRendererKey">Key of the settings editor the workflow workspace uses; empty for none.</param>
/// <param name="InputShape">Shape of the value the executor receives.</param>
/// <param name="ResultShape">Shape of the value the executor produces.</param>
/// <param name="SettingsSchemaJson">JSON Schema of the settings as JSON text in a string; may be empty.</param>
/// <param name="DefaultSettingsJson">
/// Default settings as JSON text in a string; used when a node's <c>executorSettingsJson</c> is empty.
/// </param>
/// <param name="DefaultPolicy">Timeout and retry policy used when a node has none.</param>
/// <param name="IsImplemented">False for executors that are planned but not implemented; they cannot run.</param>
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
    [JsonIgnore]
    public WorkflowDisclosureOwnerId? ProviderReadOwner { get; init; }

    /// <summary>Where the executor comes from and how far it is trusted.</summary>
    public WorkflowExecutorSourceDescriptor Source { get; init; } = WorkflowExecutorSourceDescriptor.BuiltIn();

    /// <summary>Whether the executor can run now, evaluated when the catalog was read.</summary>
    public WorkflowExecutorAvailabilityDescriptor Availability { get; init; } = IsImplemented
        ? WorkflowExecutorAvailabilityDescriptor.Available()
        : WorkflowExecutorAvailabilityDescriptor.Planned("Executor is planned but not implemented.");

    /// <summary>Versioned JSON Schema of the settings.</summary>
    public WorkflowExecutorSettingsSchemaDescriptor SettingsSchema { get; init; } =
        WorkflowExecutorSettingsSchemaDescriptor.JsonSchema("1.0", SettingsSchemaJson);

    /// <summary>
    /// Settings fields as a form description. When it lists fields, a node's settings must be a JSON object whose
    /// values satisfy them, otherwise the definition fails validation.
    /// </summary>
    public ConfigurationSchema ConfigurationSchema { get; init; } = ConfigurationSchema.Empty();

    /// <summary>
    /// How the workflow editor presents the settings, as a JSON integer: 0 Schema, 1 CustomRenderer.
    /// </summary>
    public WorkflowExecutorSettingsPresentationMode SettingsPresentationMode { get; init; } =
        WorkflowExecutorSettingsPresentationMode.Schema;

    /// <summary>Whether a preview run can simulate the executor, and with which output template.</summary>
    public WorkflowExecutorSimulationDescriptor Simulation { get; init; } = WorkflowExecutorSimulationDescriptor.None;

    /// <summary>Capabilities the executor needs and whether running it needs approval.</summary>
    public WorkflowExecutorPermissionPolicy PermissionPolicy { get; init; } = WorkflowExecutorPermissionPolicy.None;

    /// <summary>What the executor changes and how it can be previewed, committed and retried.</summary>
    public WorkflowExecutorSideEffectDescriptor SideEffects { get; init; } = WorkflowExecutorSideEffectDescriptor.None;

    /// <summary>Whether the executor supports a deterministic test mode.</summary>
    public WorkflowExecutorDeterministicTestModeDescriptor DeterministicTestMode { get; init; } = WorkflowExecutorDeterministicTestModeDescriptor.None;

    /// <summary>
    /// True when the executor is implemented and currently runnable. Computed. Definitions that use an executor that
    /// cannot run fail validation.
    /// </summary>
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

    public IReadOnlyDictionary<string, string> QueryParameters { get; init; } = new Dictionary<string, string>();

    public string QueryParametersJsonPath { get; init; } = string.Empty;

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

    public int MaxWorksheets { get; init; } = 2;
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

public sealed record WorkflowDocumentToMarkdownExecutorSettings
{
    public string SourcePath { get; init; } = string.Empty;

    public string SourcePathJsonPath { get; init; } = string.Empty;

    public string OutputPath { get; init; } = string.Empty;

    public int PreviewCharacters { get; init; } = 4000;
}

public sealed record WorkflowImageInspectExecutorSettings
{
    public string Path { get; init; } = string.Empty;

    public string PathJsonPath { get; init; } = string.Empty;
}

public sealed record WorkflowImageAnalyzeExecutorSettings
{
    public string Path { get; init; } = string.Empty;

    public string PathJsonPath { get; init; } = string.Empty;

    public string Prompt { get; init; } = "Analyze the image using only directly visible evidence.";

    public Guid? ProviderProfileId { get; init; }

    public string Model { get; init; } = string.Empty;

    public long MaxBytes { get; init; } = 10 * 1024 * 1024;

    public string ModelParameterConfigurationJson { get; init; } =
        """{"modelParameters":{"numPredict":512}}""";
}

public sealed record WorkflowImageAnalyzeExecutorResult(
    bool Succeeded,
    Guid ProviderProfileId,
    string ProviderName,
    string Model,
    string Path,
    string Prompt,
    string Analysis,
    int InputTokens,
    int OutputTokens,
    string Format,
    string ContentType,
    long SizeBytes,
    int? Width,
    int? Height,
    WorkspaceToolReceipt Receipt);
