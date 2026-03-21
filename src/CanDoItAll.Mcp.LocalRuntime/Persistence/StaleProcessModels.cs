namespace CanDoItAll.Mcp.LocalRuntime.Persistence;

public record ManagedProcessRecord(
    int Pid,
    DateTimeOffset StartedUtc,
    string Command,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    string WorkspaceRoot,
    string OwnerKind,
    string OwnerId,
    string RegisteredByServerInstanceId);

public record CleanupKilledProcessData(int Pid, string OwnerKind, string OwnerId);

public record CleanupSkippedProcessData(int Pid, string Reason);

public record CleanupStaleProcessesData(
    int Checked,
    IReadOnlyList<CleanupKilledProcessData> Killed,
    IReadOnlyList<CleanupSkippedProcessData> Skipped,
    bool DryRun);
