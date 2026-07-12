using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Plugins;

internal static class Office365WorkflowExecutorDescriptors
{
    private const string SettingsSchemaJson = "{\"type\":\"object\"}";
    private static readonly WorkflowExecutorSourceDescriptor Source = WorkflowExecutorSourceDescriptor.BundledPlugin(
        Office365PluginConstants.PluginId.Value,
        "1.0.0",
        "Office365 Mail",
        Office365PluginConstants.Icon);
    private static readonly WorkflowValueShape EmailBatchShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "Office365 email message batch JSON.");
    private static readonly WorkflowValueShape AddressMessageShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "Office365 address-matched email message JSON.");
    private static readonly WorkflowValueShape CategoryMutationShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "Office365 category mutation JSON.");

    public static WorkflowExecutorDescriptor DownloadByCategory { get; } = new(
        Office365PluginConstants.DownloadByCategoryExecutorId,
        "Office365 messages by category",
        "Downloads a bounded batch of Microsoft Graph mail messages that have the configured Outlook category.",
        WorkflowExecutorCategoryKind.Data,
        "mail",
        Office365PluginConstants.SettingsRendererKey.Value,
        WorkflowValueShape.Text,
        EmailBatchShape,
        SettingsSchemaJson,
        JsonSerializer.Serialize(new Office365WorkflowExecutorSettings(), Office365WorkflowJson.Options),
        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 },
        IsImplemented: true)
    {
        Source = Source,
        ConfigurationSchema = CreateDownloadByCategorySettingsSchema(),
        Simulation = Office365WorkflowSimulationTemplates.DownloadByCategory,
        PermissionPolicy = ReadPermissionPolicy(),
        SideEffects = WorkflowExecutorSideEffectDescriptor.ExternalRead(
            EmailWorkflowSideEffectConstants.ExternalReadReceiptSchema),
        DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported(
            "Run Preview uses simulated Microsoft Graph messages without calling Office365.")
    };

    public static WorkflowExecutorDescriptor DownloadByAddress { get; } = new(
        Office365PluginConstants.DownloadByAddressExecutorId,
        "Office365 unprocessed message by address",
        "Downloads at most one newest Microsoft 365 message from or sent by a configured email address, excluding messages already carrying the processed category.",
        WorkflowExecutorCategoryKind.Data,
        "mail",
        Office365PluginConstants.SettingsRendererKey.Value,
        WorkflowValueShape.Text,
        AddressMessageShape,
        SettingsSchemaJson,
        JsonSerializer.Serialize(new Office365MessageAddressWorkflowExecutorSettings(), Office365WorkflowJson.Options),
        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 },
        IsImplemented: true)
    {
        Source = Source,
        ConfigurationSchema = CreateDownloadByAddressSettingsSchema(),
        Simulation = Office365WorkflowSimulationTemplates.DownloadByAddress,
        PermissionPolicy = ReadPermissionPolicy(),
        SideEffects = WorkflowExecutorSideEffectDescriptor.ExternalRead(
            EmailWorkflowSideEffectConstants.ExternalReadReceiptSchema),
        DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported(
            "Run Preview uses simulated Microsoft Graph messages without calling Office365.")
    };

    public static WorkflowExecutorDescriptor MarkProcessed { get; } = new(
        Office365PluginConstants.MarkProcessedExecutorId,
        "Office365 mark processed",
        "Adds the processed Outlook category to a Microsoft 365 message and optionally removes the source category.",
        WorkflowExecutorCategoryKind.Data,
        "mail",
        Office365PluginConstants.SettingsRendererKey.Value,
        WorkflowValueShape.Text,
        CategoryMutationShape,
        SettingsSchemaJson,
        JsonSerializer.Serialize(new Office365MarkProcessedWorkflowExecutorSettings(), Office365WorkflowJson.Options),
        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 },
        IsImplemented: true)
    {
        Source = Source,
        ConfigurationSchema = CreateMarkProcessedSettingsSchema(),
        Simulation = Office365WorkflowSimulationTemplates.MarkProcessed,
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
            "Run Preview simulates the Office365 category mutation without changing Microsoft Graph.")
    };

    public static IReadOnlyList<WorkflowExecutorDescriptor> All { get; } =
    [
        DownloadByCategory,
        DownloadByAddress,
        MarkProcessed
    ];

    private static WorkflowExecutorPermissionPolicy ReadPermissionPolicy()
        => new(
            WorkflowExecutorCapabilityFlags.ReadsExternalData |
            WorkflowExecutorCapabilityFlags.UsesNetwork |
            WorkflowExecutorCapabilityFlags.UsesSecrets |
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired);

    private static ConfigurationSchema CreateDownloadByCategorySettingsSchema()
        => new(
            "1.0",
            [
                new ConfigurationFieldDescriptor("connectionId", "Connection", ConfigurationFieldType.Text, IsRequired: false, "Optional plugin connection id. Leave empty to use the latest connected Office365 OAuth connection."),
                new ConfigurationFieldDescriptor("category", "Category", ConfigurationFieldType.Text, IsRequired: true, "Outlook category name."),
                new ConfigurationFieldDescriptor("processedCategory", "Processed category", ConfigurationFieldType.Text, IsRequired: true, "Outlook category added after successful processing."),
                new ConfigurationFieldDescriptor("maxMessages", "Max messages", ConfigurationFieldType.Number, IsRequired: false, "Maximum messages to download.")
            ]);

    private static ConfigurationSchema CreateDownloadByAddressSettingsSchema()
        => new(
            "1.0",
            [
                new ConfigurationFieldDescriptor("connectionId", "Connection", ConfigurationFieldType.Text, IsRequired: false, "Optional plugin connection id. Leave empty to use the latest connected Office365 OAuth connection."),
                new ConfigurationFieldDescriptor("connectionIdJsonPath", "Connection JSON path", ConfigurationFieldType.Text, IsRequired: false, "Optional workflow JSON path used when the connection setting is empty."),
                new ConfigurationFieldDescriptor("emailAddress", "Email address", ConfigurationFieldType.Text, IsRequired: false, "Concrete sender email address. Leave empty to resolve from workflow input JSON."),
                new ConfigurationFieldDescriptor("emailAddressJsonPath", "Email address JSON path", ConfigurationFieldType.Text, IsRequired: false, "Workflow JSON path used when the email address setting is empty."),
                new ConfigurationFieldDescriptor("processedCategory", "Processed category", ConfigurationFieldType.Text, IsRequired: false, "Static Outlook category used to exclude and mark processed messages. Leave empty to resolve from workflow input JSON."),
                new ConfigurationFieldDescriptor("processedCategoryJsonPath", "Processed category JSON path", ConfigurationFieldType.Text, IsRequired: false, "Optional workflow JSON path used when the processed category setting is empty."),
                new ConfigurationFieldDescriptor("mailFolderId", "Mail folder id", ConfigurationFieldType.Text, IsRequired: false, "Optional Microsoft Graph mail folder id."),
                new ConfigurationFieldDescriptor("matchMode", "Match mode", ConfigurationFieldType.Text, IsRequired: false, "Address matching mode: FromOrSenderEquals, FromEquals, or SenderEquals."),
                new ConfigurationFieldDescriptor("maxCandidateMessages", "Max candidate messages", ConfigurationFieldType.Number, IsRequired: false, "Bounded candidate count used by fallback filtering."),
                new ConfigurationFieldDescriptor("lookbackHours", "Lookback hours", ConfigurationFieldType.Number, IsRequired: false, "Only consider messages received within this lookback window."),
                new ConfigurationFieldDescriptor("lookbackHoursJsonPath", "Lookback hours JSON path", ConfigurationFieldType.Text, IsRequired: false, "Optional workflow JSON path used when the lookback value should come from Scheduler input."),
                new ConfigurationFieldDescriptor("maxBodyCharacters", "Max body characters", ConfigurationFieldType.Number, IsRequired: false, "Maximum body text characters returned to the workflow."),
                new ConfigurationFieldDescriptor("includeBody", "Include body", ConfigurationFieldType.Boolean, IsRequired: false, "Whether to include message body text."),
                new ConfigurationFieldDescriptor("noMessageBehavior", "No-message behavior", ConfigurationFieldType.Text, IsRequired: false, "SuccessNoMessages or Fail.")
            ]);

    private static ConfigurationSchema CreateMarkProcessedSettingsSchema()
        => new(
            "1.0",
            [
                new ConfigurationFieldDescriptor("connectionId", "Connection", ConfigurationFieldType.Text, IsRequired: false, "Optional plugin connection id. Leave empty to use the latest connected Office365 OAuth connection."),
                new ConfigurationFieldDescriptor("connectionIdJsonPath", "Connection JSON path", ConfigurationFieldType.Text, IsRequired: false, "Optional workflow JSON path used when the connection setting is empty."),
                new ConfigurationFieldDescriptor("sourceCategory", "Source category", ConfigurationFieldType.Text, IsRequired: false, "Optional Outlook category removed after successful processing. Leave empty for add-only processed-category marking."),
                new ConfigurationFieldDescriptor("processedCategory", "Processed category", ConfigurationFieldType.Text, IsRequired: false, "Static Outlook category added after successful processing. Leave empty to resolve from workflow input JSON."),
                new ConfigurationFieldDescriptor("processedCategoryJsonPath", "Processed category JSON path", ConfigurationFieldType.Text, IsRequired: false, "Optional workflow JSON path used when the processed category setting is empty."),
                new ConfigurationFieldDescriptor("messageIdJsonPath", "Message id JSON path", ConfigurationFieldType.Text, IsRequired: true, "Workflow JSON path resolving to the Microsoft Graph message id.")
            ]);
}
