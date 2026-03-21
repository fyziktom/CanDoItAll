using System.ComponentModel.DataAnnotations;

namespace CanDoItAll.Mcp.SshOps.Configuration;

public sealed class McpServerOptions
{
    [Required]
    public ServerOptions Server { get; set; } = new();

    [Required]
    public TransportOptions Transport { get; set; } = new();

    [Required]
    public SecurityOptions Security { get; set; } = new();

    [Required]
    public DefaultsOptions Defaults { get; set; } = new();

    [Required]
    public RemoteJobsOptions RemoteJobs { get; set; } = new();

    [Required]
    public RevisionOptions Revisions { get; set; } = new();

    public RedactionOptions Redaction { get; set; } = new();

    [MinLength(1)]
    public TargetOptions[] Targets { get; set; } = [];
}

public sealed class ServerOptions
{
    [Required]
    public string Name { get; set; } = "CanDoItAll.Mcp.SshOps";

    public string StateDirectory { get; set; } = ".mcp-state/sshops";

    public string LogDirectory { get; set; } = ".mcp-state/sshops/logs";

    [Range(1024, int.MaxValue)]
    public int MaxBundleBytes { get; set; } = 64 * 1024 * 1024;

    public bool AllowDangerousRawExec { get; set; }
}

public sealed class TransportOptions
{
    public string Backend { get; set; } = "sshnet";

    [Range(1, 600)]
    public int ConnectTimeoutSeconds { get; set; } = 20;

    [Range(1, 3600)]
    public int CommandTimeoutSeconds { get; set; } = 120;

    [Range(1, 3600)]
    public int UploadTimeoutSeconds { get; set; } = 120;
}

public sealed class SecurityOptions
{
    public bool RequireHostKeyPinningInProduction { get; set; } = true;

    public bool RedactSecretsInLogs { get; set; } = true;

    public bool DenyAgentForwarding { get; set; } = true;

    public bool DenyPasswordAuthentication { get; set; } = false;
}

public sealed class DefaultsOptions
{
    [Required]
    public string RemoteStateRoot { get; set; } = "/opt/candoitall/.mcp-state";

    [Required]
    public string StacksRoot { get; set; } = "/opt/candoitall/stacks";

    [Required]
    public string SecretsRoot { get; set; } = "/opt/candoitall/secrets";

    [MinLength(1)]
    public string[] AllowedRoots { get; set; } = ["/opt/candoitall", "/etc/traefik"];

    [Range(1, 3600)]
    public int ComposeApplyTimeoutSeconds { get; set; } = 900;

    [Range(1, 3600)]
    public int HttpWaitTimeoutSeconds { get; set; } = 180;

    [Range(1, 3600)]
    public int CertWaitTimeoutSeconds { get; set; } = 600;

    [Range(1, 3600)]
    public int PostgresWaitTimeoutSeconds { get; set; } = 120;

    [Range(1, 300)]
    public int OperationPollIntervalSeconds { get; set; } = 5;
}

public sealed class RemoteJobsOptions
{
    [Required]
    public string Root { get; set; } = "/opt/candoitall/.mcp-state/jobs";

    [Range(1, 3600)]
    public int DefaultDetachedThresholdSeconds { get; set; } = 20;

    [Range(1, 365)]
    public int RetentionDays { get; set; } = 14;

    [Range(1, 120)]
    public int GracefulCancelSeconds { get; set; } = 10;
}

public sealed class RevisionOptions
{
    public bool Enabled { get; set; } = true;

    [Range(1, 200)]
    public int KeepLast { get; set; } = 20;

    public bool BackupBeforeOverwrite { get; set; } = true;
}

public sealed class RedactionOptions
{
    public string[] Patterns { get; set; } = [];

    public string ReplaceWith { get; set; } = "***REDACTED***";
}

public sealed class TargetOptions
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 22;

    [Required]
    public string User { get; set; } = string.Empty;

    public SudoOptions Sudo { get; set; } = new();

    [Required]
    public AuthOptions Auth { get; set; } = new();

    [Required]
    public HostKeyVerificationOptions HostKeyVerification { get; set; } = new();

    public TargetPathsOptions Paths { get; set; } = new();

    public DockerOptions Docker { get; set; } = new();

    public TraefikOptions Traefik { get; set; } = new();

    public ValidationOptions Validation { get; set; } = new();

    public GuardsOptions Guards { get; set; } = new();
}

public sealed class SudoOptions
{
    public string Mode { get; set; } = "none";

    public string Command { get; set; } = "sudo -n";
}

public sealed class AuthOptions
{
    public string? PrivateKeyEnv { get; set; }

    public string? PrivateKeyPassphraseEnv { get; set; }

    public string? PasswordEnv { get; set; }
}

public sealed class HostKeyVerificationOptions
{
    [Required]
    public string Mode { get; set; } = "fingerprintSha256";

    public string? Value { get; set; }

    public string[] Values { get; set; } = [];
}

public sealed class TargetPathsOptions
{
    public string? RemoteStateRoot { get; set; }

    public string? StacksRoot { get; set; }

    public string? SecretsRoot { get; set; }

    public string[]? AllowedRoots { get; set; }
}

public sealed class DockerOptions
{
    public string ComposeCommand { get; set; } = "docker compose";

    public string[] RequiredNetworks { get; set; } = ["proxy"];

    public string DefaultLoggingDriver { get; set; } = "local";
}

public sealed class TraefikOptions
{
    public string StackName { get; set; } = "infra-traefik";

    public string? ComposeFile { get; set; }

    public string? AcmeStoragePath { get; set; }

    public string? DashboardHost { get; set; }

    public string ResolverName { get; set; } = "default";
}

public sealed class ValidationOptions
{
    public string? PublicAppHost { get; set; }

    public string DefaultHealthPath { get; set; } = "/health";

    public string[] CertificateDomains { get; set; } = [];
}

public sealed class GuardsOptions
{
    public bool AllowBootstrap { get; set; } = true;

    public bool AllowComposeExec { get; set; } = true;

    public bool AllowRawExec { get; set; }
}
