using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

public static class GmailPluginConstants
{
    public static PluginId PluginId { get; } = new("gmail.mail");

    public static PluginPackageId PackageId { get; } = new("gmail.mail.bundled");

    public static PluginConnectionKey ConnectionKey { get; } = new("gmail");

    public static WorkflowExecutorId DownloadByLabelExecutorId { get; } = new("gmail.messages-by-label");

    public static WorkflowExecutorId MarkProcessedExecutorId { get; } = new("gmail.mark-message-processed");

    public static PluginRendererKey SettingsRendererKey { get; } = new("gmail.settings");

    public const string DefaultSourceLabel = "CanDoItAllSummaryTest";

    public const string DefaultProcessedLabel = "CanDoItAllSummaryTestProcessed";

    public const string ClientId = "977924573657-li0lctr50h2mq7p7rue9rfr53cgc1ev5.apps.googleusercontent.com";

    public const string ClientSecretEnvironmentVariable = "CANDOITALL_GMAIL_SECRET";

    public const string GmailModifyScope = "https://www.googleapis.com/auth/gmail.modify";
}
