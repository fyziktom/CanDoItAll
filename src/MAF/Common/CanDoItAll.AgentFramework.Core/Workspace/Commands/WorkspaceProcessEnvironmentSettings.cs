namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// Operator options for the environment of processes CanDoItAll starts, bound from
/// <c>AgentFramework:ProcessEnvironment</c>. Everything is off by default.
/// </summary>
public sealed class ProcessEnvironmentOptions
{
    public const string SectionName = "AgentFramework:ProcessEnvironment";

    /// <summary>
    /// Pass the host's proxy and certificate settings (HTTP(S)_PROXY, NO_PROXY, SSL_CERT_FILE, …) to
    /// processes. Proxy URLs can contain credentials, so this is an explicit operator decision.
    /// </summary>
    public bool NetworkTrust { get; set; }

    /// <summary>
    /// Further host variable names to pass to processes (names only). Names that look like secrets or
    /// that inject code into a runtime are rejected and logged.
    /// </summary>
    public string[] AdditionalInheritedNames { get; set; } = [];

    /// <summary>
    /// Folder used as HOME and DOTNET_CLI_HOME on Linux and macOS when the host itself has no HOME.
    /// Empty means the application state root's <c>toolchain-home</c> folder.
    /// </summary>
    public string? ToolchainHomePath { get; set; }

    /// <summary>
    /// Keep .NET build output of read-only external targets in the workspace's <c>.build</c> folder
    /// instead of the target's <c>bin</c>/<c>obj</c>. On by default; switch it off only for a project
    /// whose build cannot use a separate output folder.
    /// </summary>
    public bool RedirectReadOnlyDotnetOutput { get; set; } = true;
}

/// <summary>
/// The validated, process-wide environment settings. The host applies them once at startup; tests
/// pass explicit settings to <see cref="WorkspaceCommandEnvironmentPolicy"/> instead of changing these.
/// </summary>
public sealed record WorkspaceProcessEnvironmentSettings(
    bool NetworkTrust,
    IReadOnlyList<string> AdditionalInheritedNames,
    string? ToolchainHomePath)
{
    /// <summary>See <see cref="ProcessEnvironmentOptions.RedirectReadOnlyDotnetOutput"/>.</summary>
    public bool RedirectReadOnlyDotnetOutput { get; init; } = true;

    public static readonly IReadOnlyList<string> NetworkTrustNames =
    [
        "HTTP_PROXY",
        "HTTPS_PROXY",
        "NO_PROXY",
        "ALL_PROXY",
        "http_proxy",
        "https_proxy",
        "no_proxy",
        "all_proxy",
        "SSL_CERT_FILE",
        "SSL_CERT_DIR",
        "NODE_EXTRA_CA_CERTS",
        "REQUESTS_CA_BUNDLE",
        "CURL_CA_BUNDLE",
        "PIP_CERT",
        "GIT_SSL_CAINFO"
    ];

    private static readonly string[] DeniedNamePrefixes =
    [
        "MSBUILD",
        "DOTNET_STARTUP_HOOKS",
        "DOTNET_WATCH",
        "COR_",
        "CORECLR_",
        "DYLD_",
        "LD_PRELOAD",
        "LD_AUDIT",
        "NODE_OPTIONS",
        "PYTHONSTARTUP",
        "PYTHONINSPECT",
        "PERL5OPT",
        "BASH_ENV"
    ];

    private static readonly string[] DeniedNameFragments =
    [
        "KEY",
        "TOKEN",
        "SECRET",
        "PASSWORD",
        "PASSWD",
        "CREDENTIAL"
    ];

    // Static initializers run in declaration order: Default must exist before current copies it.
    public static WorkspaceProcessEnvironmentSettings Default { get; } = new(false, [], null);

    private static WorkspaceProcessEnvironmentSettings current = Default;

    public static WorkspaceProcessEnvironmentSettings Current => Volatile.Read(ref current);

    /// <summary>Applies the host's validated settings. Called once by the host at startup.</summary>
    public static void Configure(WorkspaceProcessEnvironmentSettings settings)
        => Volatile.Write(ref current, settings ?? throw new ArgumentNullException(nameof(settings)));

    /// <summary>
    /// Validates operator options. Rejected names are returned so the caller can log them; the
    /// settings never contain them. Validation never throws, so a bad option cannot stop the host.
    /// </summary>
    public static WorkspaceProcessEnvironmentSettings FromOptions(
        ProcessEnvironmentOptions? options,
        string? defaultToolchainHomePath,
        out IReadOnlyList<string> rejectedNames)
    {
        var rejected = new List<string>();
        var accepted = new List<string>();
        foreach (var raw in options?.AdditionalInheritedNames ?? [])
        {
            var name = raw?.Trim() ?? string.Empty;
            if (name.Length == 0)
            {
                continue;
            }

            if (IsDeniedName(name) || !IsValidName(name))
            {
                rejected.Add(name);
                continue;
            }

            accepted.Add(name);
        }

        rejectedNames = rejected;
        var toolchainHome = string.IsNullOrWhiteSpace(options?.ToolchainHomePath)
            ? defaultToolchainHomePath
            : options!.ToolchainHomePath!.Trim();
        return new WorkspaceProcessEnvironmentSettings(
            options?.NetworkTrust == true,
            accepted.Distinct(StringComparer.Ordinal).ToArray(),
            string.IsNullOrWhiteSpace(toolchainHome) ? null : toolchainHome)
        {
            RedirectReadOnlyDotnetOutput = options?.RedirectReadOnlyDotnetOutput ?? true
        };
    }

    /// <summary>True for names that look like secrets or that inject code into a runtime.</summary>
    public static bool IsDeniedName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return DeniedNamePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ||
               DeniedNameFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValidName(string name)
        => name.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '.' or '-') &&
           !char.IsAsciiDigit(name[0]);
}
