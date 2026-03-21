using CanDoItAll.Mcp.Core.Net;

namespace CanDoItAll.Mcp.SshOps.Coordination;

public sealed record SshOpsToolResult<T>(
    T Data,
    string Status,
    string Summary,
    string? Target = null,
    string? OperationId = null,
    IReadOnlyList<string>? Diagnostics = null,
    IReadOnlyList<string>? NextSuggestedTools = null,
    IReadOnlyList<string>? Warnings = null);

public sealed record TargetCapabilitySummary(
    bool Bootstrap,
    bool Compose,
    bool Rollback,
    bool RawExec);

public sealed record TargetDescriptor(
    string Name,
    string Host,
    bool UseSudo,
    IReadOnlyList<string> AllowedRoots,
    TargetCapabilitySummary Capabilities);

public sealed record TargetsListData(IReadOnlyList<TargetDescriptor> Targets);

public sealed record TargetTestData(
    bool Verified,
    string RemoteUser,
    string FingerprintSha256,
    string AuthenticationMethod,
    string? Banner);

public sealed record AuditOsData(
    string Distribution,
    string Version,
    string Kernel);

public sealed record AuditSudoData(
    bool Available,
    string Mode);

public sealed record AuditDockerData(
    bool Installed,
    string? Version,
    string? ComposeVersion);

public sealed record AuditPortData(
    int Port,
    bool Occupied);

public sealed record AuditDiskData(
    long AvailableMb,
    long TotalMb,
    string MountPoint);

public sealed record AuditDirectoryData(
    string Path,
    bool Exists);

public sealed record AuditToolData(
    string Name,
    bool Available);

public sealed record TargetAuditData(
    AuditOsData Os,
    AuditSudoData Sudo,
    AuditDockerData Docker,
    IReadOnlyList<AuditPortData> Ports,
    AuditDiskData? Disk,
    IReadOnlyList<AuditDirectoryData> Directories,
    IReadOnlyList<AuditToolData> Tools,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Blockers);

public sealed record HostBootstrapData(
    string Mode,
    bool DirectoriesPrepared,
    bool ProxyNetworkEnsured);

public sealed record RemoteFileBundleEntry(
    string Path,
    string Encoding,
    string Content,
    string Mode = "overwrite",
    bool BackupBeforeWrite = true,
    string? Permissions = null);

public sealed record FsApplyBundleData(
    int Written,
    int BackupsCreated,
    string? RevisionId);

public sealed record FsReadTextData(
    string Path,
    string Content,
    bool Truncated);

public sealed record FsBackupPathData(
    string BackupId,
    string StoredAt);

public sealed record FsRestoreBackupData(
    string BackupId,
    string RestoredTo);

public sealed record DockerNetworkEnsureData(
    string Name,
    bool Created,
    bool Exists);

public sealed record DockerVolumeEnsureData(
    string Name,
    bool Created);

public sealed record ComposeValidateData(
    bool Valid,
    string? NormalizedConfigPreview,
    IReadOnlyList<string> Warnings);

public sealed record ComposeWaitPolicy(
    string[] WaitForHealthyServices,
    int TimeoutSeconds);

public sealed record ComposeApplyData(
    string StackName,
    string ExecutionModeResolved,
    string? BackupRevisionId);

public sealed record ComposeServiceState(
    string Name,
    string State,
    string? Health);

public sealed record ComposePsData(IReadOnlyList<ComposeServiceState> Services);

public sealed record ComposeLogsData(
    string? Service,
    IReadOnlyList<string> Lines,
    bool Redacted);

public sealed record ComposeExecData(
    int ExitCode,
    string StandardOutput,
    string StandardError);

public sealed record ComposeDownData(
    string StackName,
    bool Stopped);

public sealed record StackRollbackData(
    string RestoringRevisionId);

public sealed record HttpProbeData(
    string Origin,
    string Url,
    int? StatusCode,
    long DurationMs,
    bool Success,
    string? Summary,
    TlsCertificateSummary? Tls,
    string? Body);

public sealed record HttpWaitData(
    string Origin,
    string Url,
    bool Ready,
    bool TimedOut,
    HttpProbeData LastProbe,
    long ElapsedMs);

public sealed record CertCheckData(
    string Domain,
    bool CertificateReady,
    string? Issuer,
    string? Subject,
    DateTimeOffset? NotAfter,
    IReadOnlyList<string> Warnings);

public sealed record PostgresReadyData(
    bool Ready,
    string Service,
    string Summary);

public sealed record IpfsStatusData(
    bool DaemonReady,
    string? PeerId,
    bool ApiReachable,
    bool GatewayReachable,
    int SwarmPeerCount);

public sealed record IpfsPrivateValidateData(
    bool PrivateMode,
    bool SwarmKeyPresent,
    bool PublicBootstrapDetected,
    IReadOnlyList<string> BootstrapPeers,
    IReadOnlyList<string> Warnings);

public sealed record OperationStatusData(
    string OperationId,
    string State,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int? ExitCode,
    string Summary);

public sealed record OperationWaitData(
    string OperationId,
    string State,
    bool Completed,
    bool TimedOut,
    long ElapsedMs,
    int? ExitCode,
    string Summary);

public sealed record OperationLogsData(
    long CursorStart,
    long CursorEnd,
    string Content,
    bool Redacted);

public sealed record OperationCancelData(
    string OperationId,
    string State,
    string Summary);

public sealed record DangerousRawExecData(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string Warning);
