using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.Plugins.UI;

namespace CanDoItAll.Modules.Plugins.Presentation;

public enum PluginLogScope { Selected, All }

public enum PluginsDialog { Packages }

public enum PluginOAuthOutcome { Connected, Failed }

public sealed record PluginOAuthReturn(PluginOAuthOutcome Outcome, PluginConnectionId? ConnectionId = null, string? Reason = null);

public sealed record PluginsUrlState(
    PluginId? PluginId = null,
    PluginSection Section = PluginSection.Main,
    PluginLogScope Logs = PluginLogScope.Selected,
    PluginsDialog? Dialog = null,
    PluginOAuthReturn? OAuthReturn = null);

public interface IPluginsWorkspaceRoute {
    void Navigate(PluginsUrlState state, UrlHistoryMode mode);
    void OpenOverlay(PluginsUrlState state);
    void CloseOverlay(PluginsUrlState parent);
}

public sealed class PluginsUrlCodec : IUrlStateCodec<PluginsUrlState> {
    public const string Path = "/plugins";
    public const string PluginKey = "pluginId";
    public const string SectionKey = "section";
    public const string LogsKey = "logs";
    public const string DialogKey = "dialog";
    public const string OAuthKey = "oauth";
    public const string ConnectionKey = "connectionId";
    public const string ReasonKey = "reason";
    public const string LegacySectionKey = "tab";
    public const int PluginIdMaximumLength = 200;
    public const int ReasonMaximumLength = 120;

    public static PluginsUrlCodec Instance { get; } = new();

    public static UrlTokenMap<PluginSection> Sections { get; } = new(
        (PluginSection.Main, "main"),
        (PluginSection.Executors, "executors"),
        (PluginSection.Settings, "settings"),
        (PluginSection.Connections, "connections"),
        (PluginSection.Logs, "logs"),
        (PluginSection.Grants, "grants"));

    public static UrlTokenMap<PluginLogScope> LogScopes { get; } = new(
        (PluginLogScope.Selected, "selected"),
        (PluginLogScope.All, "all"));

    public static UrlTokenMap<PluginsDialog> Dialogs { get; } = new((PluginsDialog.Packages, "packages"));

    public static UrlTokenMap<PluginOAuthOutcome> OAuthOutcomes { get; } = new(
        (PluginOAuthOutcome.Connected, "connected"),
        (PluginOAuthOutcome.Failed, "failed"));

    public IReadOnlyList<string> Keys { get; } = [PluginKey, SectionKey, LogsKey, DialogKey, OAuthKey, ConnectionKey, ReasonKey, LegacySectionKey];

    public static string ConnectionsHref(PluginId pluginId) => Instance.Href(Path, new(pluginId, PluginSection.Connections));

    public PluginsUrlState Read(UrlQuery query) {
        var section = query.GetToken(SectionKey, Sections) ?? query.GetToken(LegacySectionKey, Sections) ?? PluginSection.Main;
        var logs = section == PluginSection.Logs ? query.GetToken(LogsKey, LogScopes, PluginLogScope.Selected) : PluginLogScope.Selected;
        return new(ReadPlugin(query), section, logs, query.GetToken(DialogKey, Dialogs), ReadOAuthReturn(query));
    }

    public void Write(PluginsUrlState state, UrlQueryWriter query) {
        query.Text(PluginKey, state.PluginId?.Value);
        query.Token(SectionKey, state.Section, Sections, PluginSection.Main);
        if (state.Section == PluginSection.Logs) {
            query.Token(LogsKey, state.Logs, LogScopes, PluginLogScope.Selected);
        }
        query.Token(DialogKey, state.Dialog, Dialogs);
    }

    private static PluginId? ReadPlugin(UrlQuery query) {
        var text = query.GetText(PluginKey, PluginIdMaximumLength);
        return text is not null && text.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_')
            ? new PluginId(text)
            : null;
    }

    private static PluginOAuthReturn? ReadOAuthReturn(UrlQuery query) {
        if (query.GetToken(OAuthKey, OAuthOutcomes) is not { } outcome) {
            return null;
        }
        var connection = query.GetId(ConnectionKey);
        var reason = query.GetText(ReasonKey, ReasonMaximumLength);
        return new(outcome, connection is { } id ? new PluginConnectionId(id) : null, reason is not null && IsSafeReason(reason) ? reason : null);
    }

    private static bool IsSafeReason(string reason)
        => reason.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
}
