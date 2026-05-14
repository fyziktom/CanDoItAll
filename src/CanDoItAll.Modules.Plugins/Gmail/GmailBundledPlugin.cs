using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Plugins;

internal sealed class GmailBundledPlugin : IBundledPlugin
{
    private static readonly WorkflowValueShape EmailBatchShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "Gmail email message batch JSON.");
    private static readonly WorkflowValueShape LabelMutationShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "Gmail label mutation result JSON.");

    public PluginDescriptor Descriptor { get; } = new(
        GmailPluginConstants.PluginId,
        "Gmail",
        "Downloads Gmail messages by label for workflow summarization.",
        "1.0.0",
        "CanDoItAll",
        PluginSourceKind.Bundled,
        PluginTrustLevel.Bundled,
        "1.0.0",
        PluginCapabilityKind.WorkflowExecutor | PluginCapabilityKind.OAuth2 | PluginCapabilityKind.HttpClient,
        [
            new PluginWorkflowExecutorDescriptor(
                GmailPluginConstants.DownloadByLabelExecutorId,
                "Gmail messages by label",
                "Downloads the first unprocessed Gmail messages that have the selected label.",
                WorkflowExecutorCategoryKind.Data,
                GmailPluginConstants.SettingsRendererKey,
                CreateExecutorSettingsSchema(),
                WorkflowValueShape.Text,
                EmailBatchShape,
                WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 }),
            new PluginWorkflowExecutorDescriptor(
                GmailPluginConstants.MarkProcessedExecutorId,
                "Gmail mark processed",
                "Adds the processed label to a Gmail message and removes the source label.",
                WorkflowExecutorCategoryKind.Data,
                GmailPluginConstants.SettingsRendererKey,
                CreateMarkProcessedSettingsSchema(),
                WorkflowValueShape.Text,
                LabelMutationShape,
                WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 })
        ],
        PluginSettingsDescriptor.Empty,
        [
            new PluginConnectionDescriptor(
                GmailPluginConstants.ConnectionKey,
                "Gmail account",
                "OAuth connection used to read Gmail messages and move processed labels.",
                PluginConnectionAuthKind.OAuth2,
                CreateConnectionSettingsSchema(),
                IsRequired: true)
        ],
        new PluginPackageDescriptor(
            GmailPluginConstants.PackageId,
            "1.0.0",
            "1.0.0",
            Sha256: string.Empty,
            Signature: string.Empty),
        new PluginOAuth2Descriptor(
            GmailPluginConstants.ConnectionKey,
            new Uri("https://accounts.google.com/o/oauth2/v2/auth"),
            new Uri("https://oauth2.googleapis.com/token"),
            [GmailPluginConstants.GmailModifyScope])
        {
            ClientId = GmailPluginConstants.ClientId,
            ClientSecretEnvironmentVariable = GmailPluginConstants.ClientSecretEnvironmentVariable,
            AuthorizationParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["access_type"] = "offline",
                ["prompt"] = "consent",
                ["include_granted_scopes"] = "true"
            }
        });

    public void ConfigurePluginServices(IPluginServiceRegistry services)
    {
    }

    private static ConfigurationSchema CreateConnectionSettingsSchema()
        => new(
            "1.0",
            [
                new ConfigurationFieldDescriptor(PluginOAuthConnectionSettingKeys.ClientId, "Client id", ConfigurationFieldType.Text, IsRequired: false, "Optional override for the bundled Gmail OAuth client id."),
                new ConfigurationFieldDescriptor(PluginOAuthConnectionSettingKeys.RedirectUri, "Redirect URI", ConfigurationFieldType.Url, IsRequired: false, "Optional exact redirect URI registered in Google Cloud.")
            ]);

    private static ConfigurationSchema CreateExecutorSettingsSchema()
        => new(
            "1.0",
            [
                new ConfigurationFieldDescriptor("connectionId", "Connection", ConfigurationFieldType.Text, IsRequired: true, "Plugin connection id."),
                new ConfigurationFieldDescriptor("label", "Label", ConfigurationFieldType.Text, IsRequired: true, "Gmail label name or id."),
                new ConfigurationFieldDescriptor("processedLabel", "Processed label", ConfigurationFieldType.Text, IsRequired: true, "Label applied after the workflow stores the summary."),
                new ConfigurationFieldDescriptor("maxMessages", "Max messages", ConfigurationFieldType.Number, IsRequired: false, "Maximum messages to download. Use 1 for one-message processing.")
            ]);

    private static ConfigurationSchema CreateMarkProcessedSettingsSchema()
        => new(
            "1.0",
            [
                new ConfigurationFieldDescriptor("connectionId", "Connection", ConfigurationFieldType.Text, IsRequired: true, "Plugin connection id."),
                new ConfigurationFieldDescriptor("sourceLabel", "Source label", ConfigurationFieldType.Text, IsRequired: true, "Gmail label removed after successful processing."),
                new ConfigurationFieldDescriptor("processedLabel", "Processed label", ConfigurationFieldType.Text, IsRequired: true, "Gmail label added after successful processing."),
                new ConfigurationFieldDescriptor("messageIdJsonPath", "Message id JSON path", ConfigurationFieldType.Text, IsRequired: true, "Workflow JSON path resolving to the Gmail message id.")
            ]);
}
