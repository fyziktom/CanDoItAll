using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.SshOps.Configuration;

public sealed class RuntimeConfiguration
{
    public RuntimeConfiguration(IOptions<McpServerOptions> optionsAccessor)
    {
        Options = optionsAccessor.Value;
        StateDirectory = ResolvePath(Environment.CurrentDirectory, Options.Server.StateDirectory);
        LogDirectory = ResolvePath(Environment.CurrentDirectory, Options.Server.LogDirectory);
        Directory.CreateDirectory(StateDirectory);
        Directory.CreateDirectory(LogDirectory);

        ConnectTimeout = TimeSpan.FromSeconds(Options.Transport.ConnectTimeoutSeconds);
        CommandTimeout = TimeSpan.FromSeconds(Options.Transport.CommandTimeoutSeconds);
        UploadTimeout = TimeSpan.FromSeconds(Options.Transport.UploadTimeoutSeconds);
        DefaultOperationPollInterval = TimeSpan.FromSeconds(Options.Defaults.OperationPollIntervalSeconds);
        DefaultHttpWaitTimeout = TimeSpan.FromSeconds(Options.Defaults.HttpWaitTimeoutSeconds);
        DefaultComposeApplyTimeout = TimeSpan.FromSeconds(Options.Defaults.ComposeApplyTimeoutSeconds);
    }

    public McpServerOptions Options { get; }

    public string StateDirectory { get; }

    public string LogDirectory { get; }

    public TimeSpan ConnectTimeout { get; }

    public TimeSpan CommandTimeout { get; }

    public TimeSpan UploadTimeout { get; }

    public TimeSpan DefaultOperationPollInterval { get; }

    public TimeSpan DefaultHttpWaitTimeout { get; }

    public TimeSpan DefaultComposeApplyTimeout { get; }

    public FileLogStoreOptions CreateFileLogStoreOptions()
    {
        return new FileLogStoreOptions
        {
            Enabled = true,
            RootDirectory = LogDirectory,
            MaxFileSizeBytes = 50L * 1024L * 1024L
        };
    }

    public SecretRedactionOptions CreateSecretRedactionOptions()
    {
        return new SecretRedactionOptions
        {
            Enabled = Options.Security.RedactSecretsInLogs,
            Replacement = Options.Redaction.ReplaceWith,
            LiteralPatterns = Options.Redaction.Patterns
        };
    }

    private static string ResolvePath(string basePath, string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(basePath, path));
    }
}

public sealed record ResolvedTargetConfiguration(
    string Name,
    string Host,
    int Port,
    string User,
    SudoOptions Sudo,
    AuthOptions Auth,
    HostKeyVerificationOptions HostKeyVerification,
    string RemoteStateRoot,
    string StacksRoot,
    string SecretsRoot,
    IReadOnlyList<string> AllowedRoots,
    DockerOptions Docker,
    TraefikOptions Traefik,
    ValidationOptions Validation,
    GuardsOptions Guards);
