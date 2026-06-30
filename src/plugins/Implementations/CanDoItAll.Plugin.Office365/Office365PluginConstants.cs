using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

public static class Office365PluginConstants
{
    public static PluginId PluginId { get; } = new("office365.mail");

    public static PluginPackageId PackageId { get; } = new("office365.mail.bundled");

    public static UiIconDescriptor Icon { get; } = UiIconDescriptor.MaterialIcon("business_center", "Office365 Mail");

    public static PluginConnectionKey ConnectionKey { get; } = new("office365");

    public static WorkflowExecutorId DownloadByCategoryExecutorId { get; } = new("office365.messages-by-category");

    public static WorkflowExecutorId DownloadByAddressExecutorId { get; } = new("office365.message-by-address-unprocessed");

    public static WorkflowExecutorId MarkProcessedExecutorId { get; } = new("office365.mark-message-processed");

    public static PluginRendererKey SettingsRendererKey { get; } = new("office365.settings");

    public const string DefaultSourceCategory = "CanDoItAllSummaryTest";

    public const string DefaultProcessedCategory = "CanDoItAllSummaryTestProcessed";

    public const string DefaultEmailWatchProcessedCategory = "CanDoItAllProcessed";

    public const string MailReadScope = "https://graph.microsoft.com/Mail.Read";

    public const string MailReadWriteScope = "https://graph.microsoft.com/Mail.ReadWrite";

    public const string MailboxSettingsReadWriteScope = "https://graph.microsoft.com/MailboxSettings.ReadWrite";

    public const string OpenIdScope = "openid";

    public const string OfflineAccessScope = "offline_access";
}
