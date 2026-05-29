using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.AgentFramework.Maf;

public static class BuiltInWorkflowExecutorDescriptors
{
    private const string SettingsSchemaVersion = "1.0";

    private static readonly WorkflowValueShape JsonShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "JSON payload");
    private static readonly WorkflowExecutorSourceDescriptor BuiltInSource = WorkflowExecutorSourceDescriptor.BuiltIn(
        typeof(BuiltInWorkflowExecutorDescriptors).Assembly.GetName().Version?.ToString() ?? string.Empty);

    public static WorkflowExecutorDescriptor StorageFile { get; } = Create(
        WorkflowExecutorIds.StorageFile,
        "Workspace files",
        "Lists, reads, writes, appends, searches, stats, and diffs files through the workspace storage boundary.",
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
        "HTTP fetch",
        "Fetches bounded HTTP/HTTPS content with explicit method, headers, body, and size settings.",
        WorkflowExecutorCategoryKind.Http,
        "public",
        "builtin.http-fetch",
        new WorkflowHttpExecutorSettings(),
        defaultPolicy: WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 20 },
        permissionPolicy: new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.ReadsExternalData |
            WorkflowExecutorCapabilityFlags.UsesNetwork |
            WorkflowExecutorCapabilityFlags.UsesSecrets,
            WorkflowExecutorApprovalRequirement.NotRequired));

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
            WorkflowExecutorApprovalRequirement.NotRequired));

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
        WorkflowExecutorExecutionPolicy? defaultPolicy = null,
        WorkflowExecutorPermissionPolicy? permissionPolicy = null,
        WorkflowExecutorDeterministicTestModeDescriptor? deterministicTestMode = null)
    {
        const string schemaJson = "{\"type\":\"object\"}";
        var configurationSchema = CreateSettingsConfigurationSchema<TSettings>();
        return new WorkflowExecutorDescriptor(
            id,
            name,
            description,
            category,
            iconName,
            setupRendererKey,
            WorkflowValueShape.Text,
            JsonShape,
            schemaJson,
            WorkflowExecutorJson.Serialize(defaultSettings),
            defaultPolicy ?? WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true)
        {
            Source = BuiltInSource,
            Availability = WorkflowExecutorAvailabilityDescriptor.Available(),
            SettingsSchema = WorkflowExecutorSettingsSchemaDescriptor.JsonSchema(SettingsSchemaVersion, schemaJson),
            ConfigurationSchema = configurationSchema,
            PermissionPolicy = permissionPolicy ?? WorkflowExecutorPermissionPolicy.None,
            DeterministicTestMode = deterministicTestMode ?? WorkflowExecutorDeterministicTestModeDescriptor.None
        };
    }

    private static WorkflowExecutorDescriptor CreatePlanned(
        WorkflowExecutorId id,
        string name,
        string description,
        WorkflowExecutorCategoryKind category,
        string iconName,
        string setupRendererKey)
    {
        const string schemaJson = "{\"type\":\"object\"}";
        return new WorkflowExecutorDescriptor(
            id,
            name,
            description,
            category,
            iconName,
            setupRendererKey,
            WorkflowValueShape.Text,
            JsonShape,
            schemaJson,
            "{}",
            WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: false)
        {
            Source = BuiltInSource,
            Availability = WorkflowExecutorAvailabilityDescriptor.Planned("Executor is listed for roadmap visibility but is not implemented in this host."),
            SettingsSchema = WorkflowExecutorSettingsSchemaDescriptor.JsonSchema(SettingsSchemaVersion, schemaJson),
            ConfigurationSchema = ConfigurationSchema.Empty(SettingsSchemaVersion)
        };
    }

    private static ConfigurationSchema CreateSettingsConfigurationSchema<TSettings>()
    {
        var fields = typeof(TSettings)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetMethod is not null)
            .Select(property => new ConfigurationFieldDescriptor(
                JsonNamingPolicy.CamelCase.ConvertName(property.Name),
                property.Name,
                ResolveFieldType(property.PropertyType),
                IsRequired: false,
                HelpText: string.Empty)
            {
                Options = ResolveOptions(property.PropertyType)
            })
            .ToArray();

        return new ConfigurationSchema(SettingsSchemaVersion, fields);
    }

    private static ConfigurationFieldType ResolveFieldType(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (type == typeof(bool))
        {
            return ConfigurationFieldType.Boolean;
        }

        if (type == typeof(int) ||
            type == typeof(long) ||
            type == typeof(decimal) ||
            type == typeof(double) ||
            type == typeof(float))
        {
            return ConfigurationFieldType.Number;
        }

        if (type.IsEnum)
        {
            return ConfigurationFieldType.Select;
        }

        if (type == typeof(string) || type == typeof(Guid))
        {
            return ConfigurationFieldType.Text;
        }

        return ConfigurationFieldType.Json;
    }

    private static IReadOnlyList<ConfigurationFieldOption> ResolveOptions(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (!type.IsEnum)
        {
            return [];
        }

        return Enum.GetValues(type)
            .Cast<object>()
            .Select(value =>
            {
                var name = Enum.GetName(type, value) ?? value.ToString() ?? string.Empty;
                return new ConfigurationFieldOption(name, name)
                {
                    AcceptedValues = [Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)]
                };
            })
            .ToArray();
    }
}

