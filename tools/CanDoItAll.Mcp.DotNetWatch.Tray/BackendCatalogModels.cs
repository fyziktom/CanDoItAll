using System.Text.Json.Serialization;

namespace CanDoItAll.Mcp.DotNetWatch.Tray;

internal sealed record BackendIdentitySnapshot(
    string ServerName,
    string WorkspaceRoot,
    string SettingsPath,
    string SettingsHash,
    string BinaryVersionMarker);

internal sealed record BackendRegistrationRecord(
    string BackendId,
    int ProcessId,
    DateTimeOffset ProcessStartedUtc,
    DateTimeOffset RegisteredUtc,
    string BaseUrl,
    string ManagerUrl,
    string AuthToken,
    BackendIdentitySnapshot Identity);

internal sealed record CatalogRecord(
    string FilePath,
    BackendRegistrationRecord Registration);

internal sealed record ShadowManifest(
    [property: JsonPropertyName("shadowDllPath")] string? ShadowDllPath);

internal sealed record BackendCandidate(
    CatalogRecord Record,
    bool IsLive,
    bool IsReachable,
    string? UnavailableReason);

internal enum TrayStatusKind
{
    Healthy,
    Missing,
    Duplicate,
    Unreachable,
    Error
}

internal sealed record BackendTraySnapshot(
    TrayStatusKind StatusKind,
    string MenuText,
    string TooltipText,
    string NotificationKey,
    string? NotificationText,
    ToolTipIcon NotificationIcon,
    IReadOnlyList<BackendCandidate> MatchingBackends,
    BackendCandidate? PrimaryBackend,
    bool CanStartOrRecover,
    bool CanRestart)
{
    public static BackendTraySnapshot Initial(TrayOptions options)
    {
        return new BackendTraySnapshot(
            TrayStatusKind.Missing,
            $"Waiting for {Path.GetFileName(options.SettingsPath)}",
            "CanDoItAll MCP: starting tray",
            "initial",
            null,
            ToolTipIcon.None,
            [],
            null,
            CanStartOrRecover: false,
            CanRestart: false);
    }

    public static BackendTraySnapshot Error(string message)
    {
        return new BackendTraySnapshot(
            TrayStatusKind.Error,
            $"Error: {message}",
            TrimNotifyText($"CanDoItAll MCP: error | {message}"),
            $"error:{message}",
            message,
            ToolTipIcon.Error,
            [],
            null,
            CanStartOrRecover: true,
            CanRestart: false);
    }

    public static string TrimNotifyText(string value)
    {
        return value.Length <= 63 ? value : value[..63];
    }
}
