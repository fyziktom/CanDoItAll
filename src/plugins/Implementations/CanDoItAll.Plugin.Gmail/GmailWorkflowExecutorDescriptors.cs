using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Plugins;

internal static class GmailWorkflowExecutorDescriptors
{
    private const string SettingsSchemaJson = "{\"type\":\"object\"}";
    private static readonly WorkflowExecutorSourceDescriptor Source = WorkflowExecutorSourceDescriptor.BundledPlugin(
        GmailPluginConstants.PluginId.Value,
        "1.0.0",
        "Gmail",
        GmailPluginConstants.Icon);
    private static readonly WorkflowValueShape EmailBatchShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "Gmail email message batch JSON.");
    private static readonly WorkflowValueShape LabelMutationShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "Gmail processed label mutation JSON.");

    public static WorkflowExecutorDescriptor DownloadByLabel { get; } = new(
        GmailPluginConstants.DownloadByLabelExecutorId,
        "Gmail messages by label",
        "Downloads the first unprocessed Gmail messages that have the configured label.",
        WorkflowExecutorCategoryKind.Data,
        "mail",
        GmailPluginConstants.SettingsRendererKey.Value,
        WorkflowValueShape.Text,
        EmailBatchShape,
        SettingsSchemaJson,
        JsonSerializer.Serialize(new GmailWorkflowExecutorSettings(), GmailWorkflowJson.Options),
        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 },
        IsImplemented: true)
    {
        Source = Source,
        ConfigurationSchema = CreateDownloadSettingsSchema(),
        Simulation = GmailWorkflowSimulationTemplates.DownloadByLabel,
        PermissionPolicy = new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.ReadsExternalData |
            WorkflowExecutorCapabilityFlags.UsesNetwork |
            WorkflowExecutorCapabilityFlags.UsesSecrets |
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        SideEffects = WorkflowExecutorSideEffectDescriptor.ExternalRead(
            EmailWorkflowSideEffectConstants.ExternalReadReceiptSchema),
        DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported(
            "Run Preview uses simulated Gmail messages without calling Gmail.")
    };

    public static WorkflowExecutorDescriptor MarkProcessed { get; } = new(
        GmailPluginConstants.MarkProcessedExecutorId,
        "Gmail mark processed",
        "Adds the processed label to a Gmail message and removes the source label.",
        WorkflowExecutorCategoryKind.Data,
        "mail",
        GmailPluginConstants.SettingsRendererKey.Value,
        WorkflowValueShape.Text,
        LabelMutationShape,
        SettingsSchemaJson,
        JsonSerializer.Serialize(new GmailMarkProcessedWorkflowExecutorSettings(), GmailWorkflowJson.Options),
        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 },
        IsImplemented: true)
    {
        Source = Source,
        ConfigurationSchema = CreateMarkProcessedSettingsSchema(),
        Simulation = GmailWorkflowSimulationTemplates.MarkProcessed,
        PermissionPolicy = new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.WritesExternalData |
            WorkflowExecutorCapabilityFlags.UsesNetwork |
            WorkflowExecutorCapabilityFlags.UsesSecrets |
            WorkflowExecutorCapabilityFlags.IdempotentExternalMarker |
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        SideEffects = WorkflowExecutorSideEffectDescriptor.IdempotentProcessedMarker(
            "$.externalSideEffectReceipt.idempotencyKey",
            EmailWorkflowSideEffectConstants.ProcessedMarkerReceiptSchema),
        DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported(
            "Run Preview simulates the Gmail label mutation without changing Gmail.")
    };

    public static IReadOnlyList<WorkflowExecutorDescriptor> All { get; } =
    [
        DownloadByLabel,
        MarkProcessed
    ];

    private static ConfigurationSchema CreateDownloadSettingsSchema()
        => new(
            "1.0",
            [
                new ConfigurationFieldDescriptor("connectionId", "Connection", ConfigurationFieldType.Text, IsRequired: false, "Optional plugin connection id. Leave empty to use the latest connected Gmail OAuth connection."),
                new ConfigurationFieldDescriptor("label", "Label", ConfigurationFieldType.Text, IsRequired: true, "Gmail label name or id."),
                new ConfigurationFieldDescriptor("processedLabel", "Processed label", ConfigurationFieldType.Text, IsRequired: true, "Label applied after the workflow stores the summary."),
                new ConfigurationFieldDescriptor("maxMessages", "Max messages", ConfigurationFieldType.Number, IsRequired: false, "Maximum messages to download. Use 1 for one-message processing.")
            ]);

    private static ConfigurationSchema CreateMarkProcessedSettingsSchema()
        => new(
            "1.0",
            [
                new ConfigurationFieldDescriptor("connectionId", "Connection", ConfigurationFieldType.Text, IsRequired: false, "Optional plugin connection id. Leave empty to use the latest connected Gmail OAuth connection."),
                new ConfigurationFieldDescriptor("sourceLabel", "Source label", ConfigurationFieldType.Text, IsRequired: true, "Gmail label removed after successful processing."),
                new ConfigurationFieldDescriptor("processedLabel", "Processed label", ConfigurationFieldType.Text, IsRequired: true, "Gmail label added after successful processing."),
                new ConfigurationFieldDescriptor("messageIdJsonPath", "Message id JSON path", ConfigurationFieldType.Text, IsRequired: true, "Workflow JSON path resolving to the Gmail message id.")
            ]);
}
