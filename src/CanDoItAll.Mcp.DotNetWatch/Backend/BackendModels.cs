using CanDoItAll.Mcp.DotNetWatch.Configuration;
using System.Security.Cryptography;

namespace CanDoItAll.Mcp.DotNetWatch.Backend;

internal enum DotNetWatchHostMode
{
    StdioProxy,
    Backend,
    BackendLauncher
}

internal sealed record LaunchContext(
    string SettingsPath,
    DotNetWatchHostMode HostMode,
    string? BackendToken);

internal static class BackendAuth
{
    public const string HeaderName = "X-CanDoItAll-Backend-Token";
    public const string QueryKey = "token";

    public static bool IsAuthorized(HttpContext httpContext, string? expectedToken)
    {
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            return false;
        }

        if (httpContext.Request.Headers.TryGetValue(HeaderName, out var headerValues) &&
            headerValues.Any(value => string.Equals(value, expectedToken, StringComparison.Ordinal)))
        {
            return true;
        }

        return string.Equals(httpContext.Request.Query[QueryKey], expectedToken, StringComparison.Ordinal);
    }
}

public sealed record BackendIdentitySnapshot(
    string ServerName,
    string WorkspaceRoot,
    string SettingsPath,
    string SettingsHash,
    string BinaryVersionMarker);

public sealed record BackendRegistrationRecord(
    string BackendId,
    int ProcessId,
    DateTimeOffset ProcessStartedUtc,
    DateTimeOffset RegisteredUtc,
    string BaseUrl,
    string ManagerUrl,
    string AuthToken,
    BackendIdentitySnapshot Identity);

internal sealed record BackendConnectionInfo(
    BackendRegistrationRecord Registration)
{
    public string BaseUrl => Registration.BaseUrl;

    public string ManagerUrl => Registration.ManagerUrl;

    public string AuthToken => Registration.AuthToken;

    public int ProcessId => Registration.ProcessId;
}

public sealed record BackendPingResponse(
    string BackendId,
    int ProcessId,
    DateTimeOffset StartedUtc,
    BackendIdentitySnapshot Identity);

public sealed record BackendManagerStatusResponse(
    BackendIdentitySnapshot Identity,
    string BackendId,
    int ProcessId,
    DateTimeOffset StartedUtc,
    string BaseUrl,
    string ManagerUrl,
    IReadOnlyList<AppStatusData> ActiveSessions,
    IReadOnlyList<OperationStatusData> ActiveOperations,
    IReadOnlyList<OperationStatusData> RecentOperations,
    DateTimeOffset TimestampUtc);

internal sealed class BackendIdentityProvider(RuntimeConfiguration configuration, LaunchContext launchContext)
{
    private readonly Lazy<BackendIdentitySnapshot> _snapshot = new(
        () => CreateSnapshot(configuration, launchContext),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public BackendIdentitySnapshot Current => _snapshot.Value;

    public bool Matches(BackendIdentitySnapshot? candidate)
    {
        if (candidate is null)
        {
            return false;
        }

        var current = Current;
        return string.Equals(candidate.ServerName, current.ServerName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(candidate.WorkspaceRoot, current.WorkspaceRoot, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(candidate.SettingsPath, current.SettingsPath, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(candidate.SettingsHash, current.SettingsHash, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(candidate.BinaryVersionMarker, current.BinaryVersionMarker, StringComparison.OrdinalIgnoreCase);
    }

    private static BackendIdentitySnapshot CreateSnapshot(RuntimeConfiguration configuration, LaunchContext launchContext)
    {
        var settingsPath = Path.GetFullPath(launchContext.SettingsPath);
        return new BackendIdentitySnapshot(
            configuration.ServerName,
            configuration.WorkspaceRoot,
            settingsPath,
            ComputeFileHash(settingsPath),
            configuration.BinaryVersionMarker);
    }

    private static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}
