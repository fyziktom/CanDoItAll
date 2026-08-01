using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Plugins;

internal sealed class GmailBundledPlugin : IBundledPlugin
{
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
        GmailWorkflowExecutorDescriptors.All
            .Select(PluginWorkflowExecutorDescriptor.FromWorkflowExecutorDescriptor)
            .ToArray(),
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
        },
        GmailPluginConstants.Icon)
    {
        Tags = [PluginDescriptorTags.Email, PluginDescriptorTags.OAuth, PluginDescriptorTags.Workflow]
    };

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

}
