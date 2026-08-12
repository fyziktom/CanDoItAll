using System.Management;
using System.Runtime.Versioning;

namespace CanDoItAll.Manager;

[SupportedOSPlatform("windows")]
internal sealed class WindowsManagerProcessDiscovery : IManagerProcessDiscovery
{
    public Task<ManagerProcessDiscoveryResult> ProbeAsync(
        int processId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (processId <= 0)
        {
            return Task.FromResult(ManagerProcessDiscoveryResult.Unavailable(
                ManagerProcessDiscoveryStatus.Incomplete,
                "invalid-pid"));
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT ProcessId, ParentProcessId, CreationDate, CommandLine, ExecutablePath FROM Win32_Process WHERE ProcessId = {processId}");
            using var results = searcher.Get();
            var process = results.OfType<ManagementObject>().SingleOrDefault();
            if (process is null)
            {
                return Task.FromResult(ManagerProcessDiscoveryResult.Unavailable(
                    ManagerProcessDiscoveryStatus.Exited,
                    "process-exited-during-probe"));
            }

            using (process)
            {
                return Task.FromResult(TryMap(processId, process, out var evidence)
                    ? ManagerProcessDiscoveryResult.Available(evidence!)
                    : ManagerProcessDiscoveryResult.Unavailable(
                        ManagerProcessDiscoveryStatus.Incomplete,
                        "windows-process-evidence-incomplete"));
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(ManagerProcessDiscoveryResult.Unavailable(
                ManagerProcessDiscoveryStatus.PermissionDenied,
                "windows-process-permission-denied"));
        }
        catch (ManagementException exception) when (exception.ErrorCode == ManagementStatus.AccessDenied)
        {
            return Task.FromResult(ManagerProcessDiscoveryResult.Unavailable(
                ManagerProcessDiscoveryStatus.PermissionDenied,
                "windows-process-permission-denied"));
        }
        catch (ManagementException exception)
        {
            return Task.FromResult(ManagerProcessDiscoveryResult.Unavailable(
                ManagerProcessDiscoveryStatus.Failed,
                $"windows-process-query-failed:{exception.ErrorCode}"));
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult(ManagerProcessDiscoveryResult.Unavailable(
                ManagerProcessDiscoveryStatus.Failed,
                "windows-process-query-failed:invalid-state"));
        }
    }

    private static bool TryMap(
        int processId,
        ManagementObject process,
        out ManagerProcessEvidence? evidence)
    {
        evidence = null;
        var executablePath = Convert.ToString(process["ExecutablePath"]);
        var commandLine = Convert.ToString(process["CommandLine"]);
        var creationValue = Convert.ToString(process["CreationDate"]);
        if (string.IsNullOrWhiteSpace(executablePath) ||
            string.IsNullOrWhiteSpace(commandLine) ||
            string.IsNullOrWhiteSpace(creationValue) ||
            !Path.IsPathRooted(executablePath))
        {
            return false;
        }

        DateTime startedAt;
        try
        {
            startedAt = ManagementDateTimeConverter.ToDateTime(creationValue).ToUniversalTime();
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        using var ownerProcess = new ManagementObject(
            process.Scope,
            new ManagementPath($"Win32_Process.Handle='{processId}'"),
            options: null);
        ownerProcess.Get();
        object?[] ownerArguments = [null, null];
        var ownerResult = ownerProcess.InvokeMethod("GetOwner", ownerArguments);
        var ownerStatus = ownerResult is null
            ? uint.MaxValue
            : Convert.ToUInt32(ownerResult);
        var ownerName = Convert.ToString(ownerArguments[0]);
        var ownerDomain = Convert.ToString(ownerArguments[1]);
        if (ownerStatus != 0 || string.IsNullOrWhiteSpace(ownerName))
        {
            return false;
        }

        var ownerIdentity = string.IsNullOrWhiteSpace(ownerDomain)
            ? ownerName
            : $"{ownerDomain}\\{ownerName}";
        return WindowsProcessEvidenceMapper.TryCreate(
            processId,
            Convert.ToInt32(process["ParentProcessId"]),
            startedAt,
            executablePath,
            commandLine,
            ownerIdentity,
            out evidence);
    }

}

internal static class WindowsProcessEvidenceMapper
{
    public static bool TryCreate(
        int processId,
        int parentProcessId,
        DateTime startedAtUtc,
        string executablePath,
        string commandLine,
        string ownerIdentity,
        out ManagerProcessEvidence? evidence)
    {
        evidence = null;
        if (processId <= 0 ||
            parentProcessId <= 0 ||
            startedAtUtc.Kind != DateTimeKind.Utc ||
            string.IsNullOrWhiteSpace(executablePath) ||
            !Path.IsPathRooted(executablePath) ||
            string.IsNullOrWhiteSpace(commandLine) ||
            string.IsNullOrWhiteSpace(ownerIdentity))
        {
            return false;
        }

        evidence = new ManagerProcessEvidence(
            processId,
            $"windows-start:{startedAtUtc.Ticks}",
            Path.GetFullPath(executablePath),
            ManagerProcessFingerprint.ComputeObservedCommand(commandLine),
            ownerIdentity,
            parentProcessId);
        return true;
    }
}
