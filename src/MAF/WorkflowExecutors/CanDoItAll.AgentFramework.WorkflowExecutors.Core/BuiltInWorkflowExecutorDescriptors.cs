using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.AgentFramework.Core;

public sealed class BuiltInWorkflowExecutorDescriptorSource : IWorkflowExecutorDescriptorSource
{
    public IEnumerable<WorkflowExecutorDescriptor> ListExecutorDescriptors()
        => BuiltInWorkflowExecutorDescriptors.All;
}

public static class BuiltInWorkflowExecutorDescriptors
{
    private static readonly WorkflowExecutorSourceDescriptor BuiltInSource = WorkflowExecutorSourceDescriptor.BuiltIn(
        typeof(BuiltInWorkflowExecutorDescriptors).Assembly.GetName().Version?.ToString() ?? string.Empty);
    private static readonly WorkflowExecutorSideEffectDescriptor DirectExternalWriteSideEffects = new(
        WorkflowExecutorSideEffectKind.ExternalWrite,
        WorkflowExecutorExternalMutationKind.None,
        SupportsPreview: false,
        SupportsDryRun: false,
        SupportsCommit: false,
        RequiresCommitIdempotencyKey: false,
        AllowsIdempotentRetry: false,
        IdempotencyKeyJsonPath: string.Empty,
        ReceiptSchema: string.Empty);

    public static WorkflowExecutorDescriptor StorageFile { get; } = Create(
        WorkflowExecutorIds.StorageFile,
        "Workspace files",
        "Lists, reads, writes, moves, deletes, hashes, zips, unzips, searches, stats, and diffs files through the workspace storage boundary.",
        WorkflowExecutorCategoryKind.Storage,
        "folder_open",
        "builtin.storage-file",
        new WorkflowStorageFileExecutorSettings(),
        permissionPolicy: new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.ReadsWorkspace |
            WorkflowExecutorCapabilityFlags.WritesWorkspace |
            WorkflowExecutorCapabilityFlags.EmitsArtifacts |
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        deterministicTestMode: WorkflowExecutorDeterministicTestModeDescriptor.Supported("Uses the configured workspace file boundary and can be tested with sandbox files."));

    public static WorkflowExecutorDescriptor JsonTransform { get; } = Create(
        WorkflowExecutorIds.JsonTransform,
        "JSON transform",
        "Transforms JSON with typed deterministic select, set, remove, merge, array, count, template, and validation operations.",
        WorkflowExecutorCategoryKind.Data,
        "data_object",
        "builtin.json-transform",
        new WorkflowJsonTransformExecutorSettings(),
        permissionPolicy: new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        deterministicTestMode: WorkflowExecutorDeterministicTestModeDescriptor.Supported("Runs deterministic JSON transformations without external calls or arbitrary code."));

    public static WorkflowExecutorDescriptor MarkdownRender { get; } = Create(
        WorkflowExecutorIds.MarkdownRender,
        "Markdown render",
        "Renders Markdown from JSON bindings and tables, with optional workspace file output.",
        WorkflowExecutorCategoryKind.Markdown,
        "article",
        "builtin.markdown-render",
        new WorkflowMarkdownRenderExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { CaptureOutputArtifact = true },
        permissionPolicy: new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.ReadsWorkspace |
            WorkflowExecutorCapabilityFlags.WritesWorkspace |
            WorkflowExecutorCapabilityFlags.EmitsArtifacts |
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        deterministicTestMode: WorkflowExecutorDeterministicTestModeDescriptor.Supported("Renders deterministic Markdown from local JSON payloads and workspace templates."));

    public static WorkflowExecutorDescriptor SourceIngestion { get; } = Create(
        WorkflowExecutorIds.SourceIngestion,
        "Source ingestion",
        "Loads explicit project-structure workflow file and folder sources into bounded text for downstream LLM nodes.",
        WorkflowExecutorCategoryKind.Data,
        "drive_folder_upload",
        "builtin.source-ingest",
        new WorkflowSourceIngestionExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 90, CaptureOutputArtifact = true },
        permissionPolicy: new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.ReadsWorkspace |
            WorkflowExecutorCapabilityFlags.EmitsArtifacts |
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        deterministicTestMode: WorkflowExecutorDeterministicTestModeDescriptor.Supported("Reads bounded local sources and can be tested against fixture files."));

    public static WorkflowExecutorDescriptor HttpFetch { get; } = Create(
        WorkflowExecutorIds.HttpFetch,
        "HTTP request",
        "Sends a bounded HTTP/HTTPS request with SSRF guardrails and returns the response for the next workflow step.",
        WorkflowExecutorCategoryKind.Http,
        "public",
        "builtin.http-fetch",
        new WorkflowHttpExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 20 },
        permissionPolicy: new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.ReadsExternalData |
            WorkflowExecutorCapabilityFlags.WritesExternalData |
            WorkflowExecutorCapabilityFlags.WritesWorkspace |
            WorkflowExecutorCapabilityFlags.EmitsArtifacts |
            WorkflowExecutorCapabilityFlags.UsesNetwork |
            WorkflowExecutorCapabilityFlags.UsesSecrets,
            WorkflowExecutorApprovalRequirement.RequiredForExternalEffect)) with
    {
        ConfigurationSchema = CreateHttpFetchConfigurationSchema(),
        SideEffects = DirectExternalWriteSideEffects
    };

    public static WorkflowExecutorDescriptor Delay { get; } = Create(
        WorkflowExecutorIds.Delay,
        "Delay",
        "Waits for a short bounded in-process delay. This is not durable scheduling.",
        WorkflowExecutorCategoryKind.Utility,
        "timer",
        "builtin.delay",
        new WorkflowDelayExecutorSettings(),
        permissionPolicy: new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        deterministicTestMode: WorkflowExecutorDeterministicTestModeDescriptor.Supported("Supports short bounded in-process delay tests."));

    public static WorkflowExecutorDescriptor ApprovalRequest { get; } = Create(
        WorkflowExecutorIds.ApprovalRequest,
        "Approval request",
        "Creates a workflow approval request through the existing external request runtime.",
        WorkflowExecutorCategoryKind.Human,
        "approval",
        "builtin.approval-request",
        new WorkflowApprovalExecutorSettings(),
        permissionPolicy: new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        deterministicTestMode: WorkflowExecutorDeterministicTestModeDescriptor.Supported("Creates deterministic external approval request records when reached."));

    public static WorkflowExecutorDescriptor Spreadsheet { get; } = Create(
        WorkflowExecutorIds.Spreadsheet,
        "Spreadsheet",
        "Inspects, reads, writes, and Markdown-renders XLSX workbooks through the document wrapper.",
        WorkflowExecutorCategoryKind.Spreadsheet,
        "table_chart",
        "builtin.spreadsheet",
        new WorkflowSpreadsheetExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 },
        permissionPolicy: new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.ReadsWorkspace |
            WorkflowExecutorCapabilityFlags.WritesWorkspace |
            WorkflowExecutorCapabilityFlags.EmitsArtifacts |
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        deterministicTestMode: WorkflowExecutorDeterministicTestModeDescriptor.Supported("Uses local workbook fixtures through the document wrapper."));

    public static WorkflowExecutorDescriptor ProjectStructure { get; } = Create(
        WorkflowExecutorIds.ProjectStructure,
        "Project structure",
        "Reads project structures and creates typed asset and task nodes through the project-structure service.",
        WorkflowExecutorCategoryKind.ProjectStructure,
        "account_tree",
        "builtin.project-structure",
        new WorkflowProjectStructureExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 45 },
        permissionPolicy: new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.ReadsWorkspace |
            WorkflowExecutorCapabilityFlags.WritesWorkspace |
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        deterministicTestMode: WorkflowExecutorDeterministicTestModeDescriptor.Supported("Supports preview simulation for write operations without mutating project data."));

    public static WorkflowExecutorDescriptor ImageGeneration { get; } = Create(
        WorkflowExecutorIds.ImageGeneration,
        "Image generation",
        "Prepares image generation through configured image providers and managed workspace output.",
        WorkflowExecutorCategoryKind.Image,
        "image",
        "builtin.image-generation",
        new WorkflowImageGenerationExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 120, CaptureOutputArtifact = true },
        permissionPolicy: new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.WritesWorkspace |
            WorkflowExecutorCapabilityFlags.ReadsExternalData |
            WorkflowExecutorCapabilityFlags.UsesNetwork |
            WorkflowExecutorCapabilityFlags.EmitsArtifacts,
            WorkflowExecutorApprovalRequirement.RequiredForExternalEffect)) with
    {
        SettingsPresentationMode = WorkflowExecutorSettingsPresentationMode.CustomRenderer
    };

    public static WorkflowExecutorDescriptor DocumentToMarkdown { get; } = Create(
        WorkflowExecutorIds.DocumentToMarkdown,
        "Document to Markdown",
        "Converts a workspace document to Markdown through the shared artifact operation and writes the managed output artifact.",
        WorkflowExecutorCategoryKind.Markdown,
        "text_snippet",
        "builtin.document-to-markdown",
        new WorkflowDocumentToMarkdownExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 90, CaptureOutputArtifact = true },
        permissionPolicy: new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.ReadsWorkspace |
            WorkflowExecutorCapabilityFlags.WritesWorkspace |
            WorkflowExecutorCapabilityFlags.EmitsArtifacts |
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        deterministicTestMode: WorkflowExecutorDeterministicTestModeDescriptor.Supported(
            "Delegates to the deterministic workspace document conversion operation and supports fixture documents."),
        simulation: WorkflowExecutorSimulationDescriptor.JsonTemplate(
            """
            {
              "succeeded": true,
              "message": "Simulated document conversion.",
              "sourcePath": "{{settingsPath:$.sourcePath}}",
              "outputPath": "{{settingsPath:$.outputPath}}",
              "markdownPreview": "",
              "previewTruncated": false,
              "diagnostics": ""
            }
            """,
            "Simulates the document conversion result without reading or writing workspace files."));

    public static WorkflowExecutorDescriptor ImageInspect { get; } = Create(
        WorkflowExecutorIds.ImageInspect,
        "Image inspection",
        "Reads deterministic metadata for a workspace image through the shared image operation.",
        WorkflowExecutorCategoryKind.Image,
        "image_search",
        "builtin.image-inspect",
        new WorkflowImageInspectExecutorSettings(),
        permissionPolicy: new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.ReadsWorkspace |
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        deterministicTestMode: WorkflowExecutorDeterministicTestModeDescriptor.Supported(
            "Reads deterministic image headers and metadata from local fixture files."),
        simulation: WorkflowExecutorSimulationDescriptor.JsonTemplate(
            """
            {
              "succeeded": true,
              "message": "Simulated image inspection.",
              "path": "{{settingsPath:$.path}}",
              "format": "",
              "contentType": "",
              "sizeBytes": 0,
              "width": null,
              "height": null,
              "diagnostics": ""
            }
            """,
            "Simulates image metadata without reading workspace files."));

    public static WorkflowExecutorDescriptor ImageAnalyze { get; } = Create(
        WorkflowExecutorIds.ImageAnalyze,
        "Image analysis",
        "Loads a bounded workspace image and analyzes visible evidence through an enabled vision-capable Chat provider.",
        WorkflowExecutorCategoryKind.Image,
        "visibility",
        "builtin.image-analyze",
        new WorkflowImageAnalyzeExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 120 },
        permissionPolicy: new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.ReadsWorkspace |
            WorkflowExecutorCapabilityFlags.ReadsExternalData |
            WorkflowExecutorCapabilityFlags.UsesNetwork |
            WorkflowExecutorCapabilityFlags.UsesSecrets,
            WorkflowExecutorApprovalRequirement.RequiredForExternalEffect));

    public static IReadOnlyList<WorkflowExecutorDescriptor> Planned { get; } =
    [
        CreatePlanned(
            WorkflowExecutorIds.CommandProcess,
            "Command process",
            "Runs a bounded local process through the existing workspace command service.",
            WorkflowExecutorCategoryKind.Command,
            "terminal",
            "planned.command-process",
            "Command execution remains planned until typed allow-listed recipes propagate cancellation, require approval, remove credentials from child environments, and return masked output and failures.")
    ];

    public static IReadOnlyList<WorkflowExecutorDescriptor> Implemented { get; } =
    [
        StorageFile,
        JsonTransform,
        MarkdownRender,
        SourceIngestion,
        HttpFetch,
        Delay,
        ApprovalRequest,
        Spreadsheet,
        ProjectStructure,
        ImageGeneration,
        DocumentToMarkdown,
        ImageInspect,
        ImageAnalyze
    ];

    public static IReadOnlyList<WorkflowExecutorDescriptor> All { get; } =
    [
        .. Implemented,
        .. Planned
    ];

    private static ConfigurationSchema CreateHttpFetchConfigurationSchema()
    {
        var generated = WorkflowExecutorDescriptorFactory.CreateSettingsConfigurationSchema<WorkflowHttpExecutorSettings>();
        return generated with
        {
            Fields = generated.Fields
                .Select(field => field.Key switch
                {
                    "method" => field with
                    {
                        Label = "HTTP method",
                        HelpText = "HTTP method used for the request."
                    },
                    "url" => field with
                    {
                        Label = "Endpoint URL",
                        FieldType = ConfigurationFieldType.Url,
                        HelpText = "Fixed absolute HTTP or HTTPS endpoint. When set, it takes precedence over the URL-from-input setting."
                    },
                    "urlJsonPath" => field with
                    {
                        Label = "URL from input JSON path",
                        HelpText = "Optional JSON path resolving to an absolute HTTP or HTTPS URL when Endpoint URL is empty."
                    },
                    "queryParameters" => field with
                    {
                        Label = "Static query parameters",
                        FieldType = ConfigurationFieldType.Json,
                        HelpText = "Optional JSON object of query parameter names and string values. Names and values are URL-encoded."
                    },
                    "queryParametersJsonPath" => field with
                    {
                        Label = "Query parameters from input JSON path",
                        HelpText = "Optional JSON path resolving to an object of string, number, or boolean query parameter values."
                    },
                    "headers" => field with
                    {
                        Label = "Headers",
                        HelpText = "Optional JSON object of non-secret request headers. Use Secret header for credentials."
                    },
                    "secretHeader" => field with
                    {
                        Label = "Secret header",
                        HelpText = "Optional stored-secret binding applied to one request header at execution time."
                    },
                    "body" => field with
                    {
                        Label = "JSON body",
                        FieldType = ConfigurationFieldType.MultilineText,
                        HelpText = "Optional JSON request body. GET requests do not send a body."
                    },
                    "maxResponseBytes" => field with
                    {
                        Label = "Maximum response bytes",
                        HelpText = "Bounded response size requested from the executor, from 1 KiB through 5 MiB."
                    },
                    "includeInputPayload" => field with
                    {
                        Label = "Include input payload",
                        HelpText = "Include the incoming workflow payload in the executor result for the next node."
                    },
                    "allowPrivateNetworkTargets" => field with
                    {
                        Label = "Allow private network targets",
                        HelpText = "Allow loopback, private, and link-local targets. Enable only for explicitly trusted local services."
                    },
                    "downloadToWorkspace" => field with
                    {
                        Label = "Download to workspace",
                        HelpText = "Write the bounded response body to the managed workflow workspace."
                    },
                    "outputPath" => field with
                    {
                        Label = "Workspace output path",
                        HelpText = "Optional managed workspace path used when Download to workspace is enabled."
                    },
                    "overwrite" => field with
                    {
                        Label = "Overwrite workspace file",
                        HelpText = "Allow replacement of an existing workspace output file."
                    },
                    _ => field
                })
                .ToArray()
        };
    }

    private static WorkflowExecutorDescriptor Create<TSettings>(
        WorkflowExecutorId id,
        string name,
        string description,
        WorkflowExecutorCategoryKind category,
        string iconName,
        string setupRendererKey,
        TSettings defaultSettings,
        WorkflowExecutorExecutionPolicy? defaultPolicy = null,
        WorkflowExecutorPermissionPolicy? permissionPolicy = null,
        WorkflowExecutorDeterministicTestModeDescriptor? deterministicTestMode = null,
        WorkflowExecutorSimulationDescriptor? simulation = null)
    {
        var descriptor = WorkflowExecutorDescriptorFactory.CreateImplemented(
            id,
            name,
            description,
            category,
            iconName,
            setupRendererKey,
            defaultSettings,
            BuiltInSource,
            defaultPolicy: defaultPolicy,
            permissionPolicy: permissionPolicy,
            deterministicTestMode: deterministicTestMode);
        return descriptor with
        {
            Simulation = simulation ?? WorkflowExecutorSimulationDescriptor.None
        };
    }

    private static WorkflowExecutorDescriptor CreatePlanned(
        WorkflowExecutorId id,
        string name,
        string description,
        WorkflowExecutorCategoryKind category,
        string iconName,
        string setupRendererKey,
        string availabilityMessage)
        => WorkflowExecutorDescriptorFactory.CreatePlanned(
            id,
            name,
            description,
            category,
            iconName,
            setupRendererKey,
            BuiltInSource,
            availabilityMessage);
}
