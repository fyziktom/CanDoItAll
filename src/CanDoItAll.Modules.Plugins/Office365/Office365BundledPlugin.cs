using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Plugins;

internal sealed class Office365BundledPlugin : IBundledPlugin
{
    private static readonly WorkflowValueShape EmailBatchShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "Office365 email message batch JSON.");

    public PluginDescriptor Descriptor { get; } = new(
        Office365PluginConstants.PluginId,
        "Office365 Mail",
        "Downloads Microsoft 365 messages by Outlook category for workflow summarization.",
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
        ],
        PluginSettingsDescriptor.Empty,
        [
            new PluginConnectionDescriptor(
                Office365PluginConstants.ConnectionKey,
                "Office365 account",
                "OAuth connection used to read Microsoft 365 mail through Graph.",
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
            [Office365PluginConstants.OfflineAccessScope, Office365PluginConstants.MailReadScope]));

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
                new ConfigurationFieldDescriptor("connectionId", "Connection", ConfigurationFieldType.Text, IsRequired: true, "Plugin connection id."),
                new ConfigurationFieldDescriptor("category", "Category", ConfigurationFieldType.Text, IsRequired: true, "Outlook category name."),
                new ConfigurationFieldDescriptor("maxMessages", "Max messages", ConfigurationFieldType.Number, IsRequired: false, "Maximum messages to download.")
            ]);
}
