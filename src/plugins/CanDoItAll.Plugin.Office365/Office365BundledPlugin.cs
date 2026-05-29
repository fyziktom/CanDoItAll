using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Plugins;

internal sealed class Office365BundledPlugin : IBundledPlugin
{
    private const string PromptParameter = "prompt";
    private const string ConsentPrompt = "consent";

    private static readonly WorkflowValueShape EmailBatchShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "Office365 email message batch JSON.");

    private static readonly WorkflowValueShape CategoryMutationShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "Office365 category mutation result JSON.");

    public PluginDescriptor Descriptor { get; } = new(
        Office365PluginConstants.PluginId,
        "Office365 Mail",
        "Downloads Microsoft 365 messages by Outlook category for workflow summarization and marks processed messages.",
        "1.0.0",
        "CanDoItAll",
        PluginSourceKind.Bundled,
        PluginTrustLevel.Bundled,
        "1.0.0",
        PluginCapabilityKind.WorkflowExecutor | PluginCapabilityKind.OAuth2 | PluginCapabilityKind.HttpClient,
        [
            new PluginWorkflowExecutorDescriptor(
                Office365PluginConstants.DownloadByCategoryExecutorId,
                "Office365 messages by category",
                "Downloads a bounded batch of Microsoft Graph mail messages that have the selected Outlook category.",
                WorkflowExecutorCategoryKind.Data,
                Office365PluginConstants.SettingsRendererKey,
                CreateExecutorSettingsSchema(),
                WorkflowValueShape.Text,
                EmailBatchShape,
                WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 })
            {
                PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                    WorkflowExecutorCapabilityFlags.ReadsExternalData |
                    WorkflowExecutorCapabilityFlags.UsesNetwork |
                    WorkflowExecutorCapabilityFlags.UsesSecrets |
                    WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
                    WorkflowExecutorApprovalRequirement.NotRequired),
                DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Run Preview uses simulated Microsoft Graph messages without calling Office365.")
            },
            new PluginWorkflowExecutorDescriptor(
                Office365PluginConstants.MarkProcessedExecutorId,
                "Office365 mark processed",
                "Adds the processed Outlook category to a Microsoft 365 message and removes the source category.",
                WorkflowExecutorCategoryKind.Data,
                Office365PluginConstants.SettingsRendererKey,
                CreateMarkProcessedSettingsSchema(),
                WorkflowValueShape.Text,
                CategoryMutationShape,
                WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 })
            {
                PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                    WorkflowExecutorCapabilityFlags.WritesExternalData |
                    WorkflowExecutorCapabilityFlags.UsesNetwork |
                    WorkflowExecutorCapabilityFlags.UsesSecrets |
                    WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
                    WorkflowExecutorApprovalRequirement.RequiredForExternalEffect),
                DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Run Preview simulates the Office365 category mutation without changing Microsoft Graph.")
            }
        ],
        PluginSettingsDescriptor.Empty,
        [
            new PluginConnectionDescriptor(
                Office365PluginConstants.ConnectionKey,
                "Office365 account",
                "OAuth connection used to read Microsoft 365 mail and move processed Outlook categories through Graph.",
                PluginConnectionAuthKind.OAuth2,
                CreateConnectionSettingsSchema(),
                IsRequired: true)
        ],
        new PluginPackageDescriptor(
            Office365PluginConstants.PackageId,
            "1.0.0",
            "1.0.0",
            Sha256: string.Empty,
            Signature: string.Empty),
        new PluginOAuth2Descriptor(
            Office365PluginConstants.ConnectionKey,
            new Uri("https://login.microsoftonline.com/common/oauth2/v2.0/authorize"),
            new Uri("https://login.microsoftonline.com/common/oauth2/v2.0/token"),
            [
                Office365PluginConstants.OpenIdScope,
                Office365PluginConstants.OfflineAccessScope,
                Office365PluginConstants.MailReadScope,
                Office365PluginConstants.MailReadWriteScope,
                Office365PluginConstants.MailboxSettingsReadWriteScope
            ])
        {
            AuthorizationParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PromptParameter] = ConsentPrompt
            }
        },
        Office365PluginConstants.Icon);

    public void ConfigurePluginServices(IPluginServiceRegistry services)
    {
    }

    private static ConfigurationSchema CreateConnectionSettingsSchema()
        => new(
            "1.0",
            [
                new ConfigurationFieldDescriptor(PluginOAuthConnectionSettingKeys.ClientId, "Client id", ConfigurationFieldType.Text, IsRequired: true, "Microsoft Entra application client id."),
                new ConfigurationFieldDescriptor(PluginOAuthConnectionSettingKeys.RedirectUri, "Redirect URI", ConfigurationFieldType.Url, IsRequired: false, "Optional exact redirect URI registered in Microsoft Entra.")
            ]);

    private static ConfigurationSchema CreateExecutorSettingsSchema()
        => new(
            "1.0",
            [
                new ConfigurationFieldDescriptor("connectionId", "Connection", ConfigurationFieldType.Text, IsRequired: false, "Optional plugin connection id. Leave empty to use the latest connected Office365 OAuth connection."),
                new ConfigurationFieldDescriptor("category", "Category", ConfigurationFieldType.Text, IsRequired: true, "Outlook category name."),
                new ConfigurationFieldDescriptor("processedCategory", "Processed category", ConfigurationFieldType.Text, IsRequired: true, "Outlook category added after successful processing."),
                new ConfigurationFieldDescriptor("maxMessages", "Max messages", ConfigurationFieldType.Number, IsRequired: false, "Maximum messages to download.")
            ]);

    private static ConfigurationSchema CreateMarkProcessedSettingsSchema()
        => new(
            "1.0",
            [
                new ConfigurationFieldDescriptor("connectionId", "Connection", ConfigurationFieldType.Text, IsRequired: false, "Optional plugin connection id. Leave empty to use the latest connected Office365 OAuth connection."),
                new ConfigurationFieldDescriptor("sourceCategory", "Source category", ConfigurationFieldType.Text, IsRequired: true, "Outlook category removed after successful processing."),
                new ConfigurationFieldDescriptor("processedCategory", "Processed category", ConfigurationFieldType.Text, IsRequired: true, "Outlook category added after successful processing."),
                new ConfigurationFieldDescriptor("messageIdJsonPath", "Message id JSON path", ConfigurationFieldType.Text, IsRequired: true, "Workflow JSON path resolving to the Microsoft Graph message id.")
            ]);
}
