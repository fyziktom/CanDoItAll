using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Manager;

internal sealed record MacProcessCommandResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    WorkspaceProcessTerminationReason TerminationReason = WorkspaceProcessTerminationReason.Completed);

internal sealed record MacProcessNativeIdentity(
    int ProcessId,
    int ParentProcessId,
    uint UserId,
    long StartSeconds,
    int StartMicroseconds)
{
    public string StartIdentity => $"macos-kernel-start:{StartSeconds}:{StartMicroseconds}";
}

internal sealed record MacProcessIdentityReadResult(
    ManagerProcessDiscoveryStatus Status,
    MacProcessNativeIdentity? Identity,
    string DiagnosticCode)
{
    public static MacProcessIdentityReadResult Available(MacProcessNativeIdentity identity)
        => new(ManagerProcessDiscoveryStatus.Available, identity, "available");

    public static MacProcessIdentityReadResult Unavailable(
        ManagerProcessDiscoveryStatus status,
        string diagnosticCode)
        => new(status, null, diagnosticCode);
}

internal interface IMacProcessIdentityReader
{
    MacProcessIdentityReadResult Read(int processId);
}

internal sealed class LibProcMacProcessIdentityReader : IMacProcessIdentityReader
{
    private const int ProcPidTbsdInfo = 3;
    private const int ProcBsdInfoSize = 136;
    private const int ProcessIdOffset = 12;
    private const int ParentProcessIdOffset = 16;
    private const int UserIdOffset = 20;
    private const int StartSecondsOffset = 120;
    private const int StartMicrosecondsOffset = 128;

    public MacProcessIdentityReadResult Read(int processId)
    {
        var buffer = new byte[ProcBsdInfoSize];
        var bytesRead = NativeMethods.ProcPidInfo(
            processId,
            ProcPidTbsdInfo,
            0,
            buffer,
            buffer.Length);
        if (bytesRead <= 0)
        {
            return Marshal.GetLastPInvokeError() switch
            {
                3 => MacProcessIdentityReadResult.Unavailable(
                    ManagerProcessDiscoveryStatus.Exited,
                    "macos-kernel-process-exited"),
                1 or 13 => MacProcessIdentityReadResult.Unavailable(
                    ManagerProcessDiscoveryStatus.PermissionDenied,
                    "macos-kernel-process-permission-denied"),
                _ => MacProcessIdentityReadResult.Unavailable(
                    ManagerProcessDiscoveryStatus.Incomplete,
                    "macos-kernel-process-query-failed")
            };
        }

        return bytesRead >= ProcBsdInfoSize && TryParseBuffer(processId, buffer, out var identity)
            ? MacProcessIdentityReadResult.Available(identity!)
            : MacProcessIdentityReadResult.Unavailable(
                ManagerProcessDiscoveryStatus.Incomplete,
                "macos-kernel-process-evidence-incomplete");
    }

    internal static bool TryParseBuffer(
        int expectedProcessId,
        ReadOnlySpan<byte> buffer,
        out MacProcessNativeIdentity? identity)
    {
        identity = null;
        if (buffer.Length < ProcBsdInfoSize)
        {
            return false;
        }

        var processId = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(buffer[ProcessIdOffset..]));
        var parentProcessId = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(buffer[ParentProcessIdOffset..]));
        var userId = BinaryPrimitives.ReadUInt32LittleEndian(buffer[UserIdOffset..]);
        var startSeconds = checked((long)BinaryPrimitives.ReadUInt64LittleEndian(buffer[StartSecondsOffset..]));
        var startMicroseconds = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(buffer[StartMicrosecondsOffset..]));
        if (processId != expectedProcessId || startSeconds <= 0 || startMicroseconds is < 0 or >= 1_000_000)
        {
            return false;
        }

        identity = new MacProcessNativeIdentity(
            processId,
            parentProcessId,
            userId,
            startSeconds,
            startMicroseconds);
        return true;
    }

    private static class NativeMethods
    {
        [DllImport("/usr/lib/libproc.dylib", EntryPoint = "proc_pidinfo", SetLastError = true)]
        internal static extern int ProcPidInfo(
            int processId,
            int flavor,
            ulong argument,
            byte[] buffer,
            int bufferSize);
    }
}

internal interface IMacProcessCommandRunner
{
    Task<MacProcessCommandResult> RunAsync(int processId, CancellationToken cancellationToken);
}

internal sealed class B01MacProcessCommandRunner(IWorkspaceProcessHost processHost) : IMacProcessCommandRunner
{
    private readonly WorkspaceCommandEnvironmentPolicy environmentPolicy = new();

    public async Task<MacProcessCommandResult> RunAsync(int processId, CancellationToken cancellationToken)
    {
        var environment = environmentPolicy.MergeEnvironmentVariables(
            new Dictionary<string, string?> { ["LC_ALL"] = "C" });
        var result = await processHost.ExecuteAsync(
            new WorkspaceProcessExecutionRequest(
                "manager_macos_process_discovery",
                "manager.process-discovery.macos.v1",
                "/bin/ps",
                [
                    "-p",
                    processId.ToString(CultureInfo.InvariantCulture),
                    "-o",
                    "pid=,ppid=,uid=,lstart=,comm=,args="
                ],
                "/",
                environment,
                5,
                64 * 1024,
                16 * 1024),
            cancellationToken).ConfigureAwait(false);
        return new MacProcessCommandResult(
            result.ExitCode,
            result.Stdout,
            result.Stderr,
            result.TerminationReason);
    }
}

internal sealed partial class MacOsManagerProcessDiscovery(
    IMacProcessCommandRunner commandRunner,
    IMacProcessIdentityReader identityReader) : IManagerProcessDiscovery
{
    private const int MaximumOutputLength = 64 * 1024;

    [GeneratedRegex(
        @"^\s*(?<pid>\d+)\s+(?<ppid>\d+)\s+(?<uid>\d+)\s+(?<start>[A-Z][a-z]{2}\s+[A-Z][a-z]{2}\s+\d{1,2}\s+\d{2}:\d{2}:\d{2}\s+\d{4})\s+(?<exe>\S+)\s+(?<args>.+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ProcessLineRegex();

    public async Task<ManagerProcessDiscoveryResult> ProbeAsync(
        int processId,
        CancellationToken cancellationToken = default)
    {
        if (processId <= 0)
        {
            return ManagerProcessDiscoveryResult.Unavailable(
                ManagerProcessDiscoveryStatus.Incomplete,
                "invalid-pid");
        }

        var identityResult = identityReader.Read(processId);
        if (identityResult is not { Status: ManagerProcessDiscoveryStatus.Available, Identity: not null })
        {
            return ManagerProcessDiscoveryResult.Unavailable(
                identityResult.Status,
                identityResult.DiagnosticCode);
        }

        var result = await commandRunner.RunAsync(processId, cancellationToken).ConfigureAwait(false);
        if (result.TerminationReason != WorkspaceProcessTerminationReason.Completed)
        {
            return ManagerProcessDiscoveryResult.Unavailable(
                ManagerProcessDiscoveryStatus.Incomplete,
                result.TerminationReason switch
                {
                    WorkspaceProcessTerminationReason.TimedOut => "macos-process-query-timeout",
                    WorkspaceProcessTerminationReason.CallerCanceled => "macos-process-query-cancelled",
                    WorkspaceProcessTerminationReason.StartFailed => "macos-process-query-start-failed",
                    WorkspaceProcessTerminationReason.TerminationFailed => "macos-process-query-termination-failed",
                    _ => "macos-process-query-incomplete"
                });
        }

        if (result.ExitCode != 0)
        {
            return ManagerProcessDiscoveryResult.Unavailable(
                IsPermissionDenied(result.Stderr)
                    ? ManagerProcessDiscoveryStatus.PermissionDenied
                    : ManagerProcessDiscoveryStatus.Incomplete,
                IsPermissionDenied(result.Stderr)
                    ? "macos-process-query-permission-denied"
                    : "macos-process-query-failed");
        }

        if (result.Stdout.Length > MaximumOutputLength)
        {
            return ManagerProcessDiscoveryResult.Unavailable(
                ManagerProcessDiscoveryStatus.Incomplete,
                "macos-process-output-too-large");
        }

        if (!TryParse(processId, result.Stdout, out var evidence) ||
            evidence!.ParentProcessId != identityResult.Identity.ParentProcessId ||
            !string.Equals(
                evidence.OwnerIdentity,
                $"uid:{identityResult.Identity.UserId}",
                StringComparison.Ordinal))
        {
            return ManagerProcessDiscoveryResult.Unavailable(
                ManagerProcessDiscoveryStatus.Incomplete,
                "macos-process-evidence-incomplete");
        }

        return ManagerProcessDiscoveryResult.Available(
            evidence with { StartIdentity = identityResult.Identity.StartIdentity });
    }

    internal static bool TryParse(
        int expectedProcessId,
        string output,
        out ManagerProcessEvidence? evidence)
    {
        evidence = null;
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length != 1)
        {
            return false;
        }

        var match = ProcessLineRegex().Match(lines[0]);
        if (!match.Success ||
            !int.TryParse(match.Groups["pid"].Value, CultureInfo.InvariantCulture, out var processId) ||
            processId != expectedProcessId ||
            !int.TryParse(match.Groups["ppid"].Value, CultureInfo.InvariantCulture, out var parentProcessId) ||
            !DateTime.TryParseExact(
                match.Groups["start"].Value,
                "ddd MMM d HH:mm:ss yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var startedAt))
        {
            return false;
        }

        var executablePath = match.Groups["exe"].Value;
        var arguments = match.Groups["args"].Value;
        var uid = match.Groups["uid"].Value;
        if (!Path.IsPathRooted(executablePath) || string.IsNullOrWhiteSpace(arguments))
        {
            return false;
        }

        evidence = new ManagerProcessEvidence(
            processId,
            $"macos-start:{startedAt.ToUniversalTime().Ticks}",
            Path.GetFullPath(executablePath),
            ManagerProcessFingerprint.ComputeObservedCommand(arguments),
            $"uid:{uid}",
            parentProcessId);
        return true;
    }

    private static bool IsPermissionDenied(string error)
        => error.Contains("not permitted", StringComparison.OrdinalIgnoreCase) ||
           error.Contains("permission denied", StringComparison.OrdinalIgnoreCase) ||
           error.Contains("not authorized", StringComparison.OrdinalIgnoreCase);
}
