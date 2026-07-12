using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Plugins;

internal static class DockerWorkflowExecutorDescriptors
{
    private const string SettingsSchemaJson = "{\"type\":\"object\"}";
    private static readonly WorkflowExecutorSourceDescriptor Source = WorkflowExecutorSourceDescriptor.BundledPlugin(
        DockerPluginConstants.PluginId.Value,
        "1.0.0",
        "Docker",
        DockerPluginConstants.Icon);
    private static readonly WorkflowValueShape ResultShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "Docker command JSON result");

    public static WorkflowExecutorDescriptor ListContainers { get; } = Create(
        DockerPluginConstants.ListContainersExecutorId,
        "Docker containers",
        "Lists running Docker containers through the guarded Docker plugin.",
        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 20 });

    public static WorkflowExecutorDescriptor PullImage { get; } = Create(
        DockerPluginConstants.PullImageExecutorId,
        "Docker pull image",
        "Pulls a validated Docker image through the guarded Docker plugin.",
        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 900, CaptureOutputArtifact = true });

    public static WorkflowExecutorDescriptor StartContainer { get; } = Create(
        DockerPluginConstants.StartContainerExecutorId,
        "Docker start container",
        "Starts an existing container or creates one from a validated image through the guarded Docker plugin.",
        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 120, CaptureOutputArtifact = true });

    public static WorkflowExecutorDescriptor ReadLogs { get; } = Create(
        DockerPluginConstants.ReadLogsExecutorId,
        "Docker logs",
        "Reads bounded logs from a Docker container through the guarded Docker plugin.",
        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 30, CaptureOutputArtifact = true });

    public static IReadOnlyList<WorkflowExecutorDescriptor> All { get; } =
    [
        ListContainers,
        PullImage,
        StartContainer,
        ReadLogs
    ];

    private static WorkflowExecutorDescriptor Create(
        WorkflowExecutorId id,
        string name,
        string description,
        WorkflowExecutorExecutionPolicy policy)
        => new(
            id,
            name,
            description,
            WorkflowExecutorCategoryKind.Command,
            "terminal",
            DockerPluginConstants.SettingsRendererKey.Value,
            WorkflowValueShape.Text,
            ResultShape,
            SettingsSchemaJson,
            JsonSerializer.Serialize(new DockerWorkflowExecutorSettings(), DockerWorkflowJson.Options),
            policy,
            IsImplemented: true)
        {
            Source = Source,
            SettingsSchema = WorkflowExecutorSettingsSchemaDescriptor.JsonSchema("1.0", SettingsSchemaJson),
            ConfigurationSchema = CreateConfigurationSchema(),
            Simulation = DockerWorkflowSimulationTemplates.CommandResult,
            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                WorkflowExecutorCapabilityFlags.RunsHostCommand |
                WorkflowExecutorCapabilityFlags.EmitsArtifacts |
                WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
                WorkflowExecutorApprovalRequirement.AlwaysRequired),
            DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported(
                "Run Preview simulates Docker host-tool output without invoking Docker.")
        };

    private static ConfigurationSchema CreateConfigurationSchema()
        => new(
            "1.0",
            [
                new ConfigurationFieldDescriptor("image", "Image", ConfigurationFieldType.Text, IsRequired: false, "Docker image reference."),
                new ConfigurationFieldDescriptor("containerName", "Container name", ConfigurationFieldType.Text, IsRequired: false, "Docker container name."),
                new ConfigurationFieldDescriptor("pullIfMissing", "Pull if missing", ConfigurationFieldType.Boolean, IsRequired: false, "Pull the image before creating the container when it is not available locally."),
                new ConfigurationFieldDescriptor("portMappings", "Port mappings", ConfigurationFieldType.Json, IsRequired: false, "JSON array of host:container port mappings."),
                new ConfigurationFieldDescriptor("tail", "Log tail", ConfigurationFieldType.Number, IsRequired: false, "Maximum number of log lines to read."),
                new ConfigurationFieldDescriptor("since", "Logs since", ConfigurationFieldType.Text, IsRequired: false, "Optional docker logs --since value."),
                new ConfigurationFieldDescriptor("maxOutputCharacters", "Output cap", ConfigurationFieldType.Number, IsRequired: false, "Maximum stdout/stderr characters captured.")
            ]);
}

internal static class DockerWorkflowJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
