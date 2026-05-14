using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

public static class Office365PluginConstants
{
    public static PluginId PluginId { get; } = new("office365.mail");

    public static PluginPackageId PackageId { get; } = new("office365.mail.bundled");

    public static PluginConnectionKey ConnectionKey { get; } = new("office365");

    public static WorkflowExecutorId DownloadByCategoryExecutorId { get; } = new("office365.messages-by-category");

    public static PluginRendererKey SettingsRendererKey { get; } = new("office365.settings");

    public const string MailReadScope = "https://graph.microsoft.com/Mail.Read";

    public const string OpenIdScope = "openid";

    public const string OfflineAccessScope = "offline_access";
}
