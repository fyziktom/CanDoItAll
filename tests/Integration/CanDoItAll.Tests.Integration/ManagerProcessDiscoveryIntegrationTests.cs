using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure;
using CanDoItAll.Manager;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "ManagerPortability")]
[Trait("Category", "UnixRuntimePortability")]
public sealed class ManagerProcessDiscoveryIntegrationTests
{
    [Fact]
    public async Task Current_host_adapter_reads_complete_identity_for_the_current_process()
    {
        var discovery = ManagerProcessDiscoveryFactory.Create(new LocalWorkspaceProcessHost());

        var result = await discovery.ProbeAsync(Environment.ProcessId);

        if (OperatingSystem.IsWindows() && result.Status == ManagerProcessDiscoveryStatus.PermissionDenied)
        {
            Assert.Equal("windows-process-permission-denied", result.DiagnosticCode);
            Assert.Null(result.Evidence);
            return;
        }

        Assert.True(result.Status == ManagerProcessDiscoveryStatus.Available, $"Process discovery failed with '{result.DiagnosticCode}'.");
        Assert.NotNull(result.Evidence);
        Assert.Equal(Environment.ProcessId, result.Evidence.ProcessId);
        Assert.NotEmpty(result.Evidence.StartIdentity);
        Assert.True(Path.IsPathRooted(result.Evidence.ExecutablePath));
        Assert.NotEmpty(result.Evidence.ObservedCommandFingerprint);
        Assert.NotEmpty(result.Evidence.OwnerIdentity);
        Assert.True(result.Evidence.ParentProcessId > 0);
    }

    [Fact]
    public async Task Linux_coordinator_registers_and_gracefully_terminates_a_real_owned_process()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Manager.Integration.{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var registry = new InMemoryRegistry();
            var coordinator = new ManagerProcessCoordinator(
                new LocalWorkspaceProcessHost(),
                new LinuxManagerProcessDiscovery(),
                registry,
                new PhysicalFileSystemPathPolicyFactory(),
                ManagerHostKind.Linux);
            await using var lease = await coordinator.StartAsync(
                new ManagerProcessLaunchRequest(
                    ManagerProcessPurpose.DotnetWatch,
                    "workspace_dotnet_manager_watch",
                    "manager.integration-process.v1",
                    "/bin/sh",
                    ["-c", "trap 'exit 0' TERM; while :; do :; done"],
                    root,
                    new Dictionary<string, string?>(),
                    root,
                    "integration"));

            var running = Assert.Single(await registry.ReadAllAsync());
            var termination = await lease.TerminateAsync("integration-complete");
            var completed = Assert.Single(await registry.ReadAllAsync());

            Assert.Equal(ManagerProcessLifecycleState.Running, running.State);
            Assert.Equal(WorkspaceProcessTerminationStatus.Terminated, termination.Status);
            Assert.False(termination.ResidualProcessPossible);
            Assert.Equal(ManagerProcessLifecycleState.Terminated, completed.State);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Linux_recovery_reclaims_an_exact_child_after_its_original_parent_exits()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Manager.Reparent.{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var childProcessId = 0;
        try
        {
            var processHost = new LocalWorkspaceProcessHost();
            var parentStartInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/setsid",
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            parentStartInfo.ArgumentList.Add("/bin/sh");
            parentStartInfo.ArgumentList.Add("-c");
            parentStartInfo.ArgumentList.Add("nohup sleep 30 >/dev/null 2>&1 & echo $!");
            using var parent = Process.Start(parentStartInfo)
                ?? throw new InvalidOperationException("The unmanaged recovery fixture could not start.");
            int originalParentProcessId = parent.Id;
            string parentOutput = await parent.StandardOutput.ReadToEndAsync();
            await parent.WaitForExitAsync();
            childProcessId = int.Parse(parentOutput.Trim(), System.Globalization.CultureInfo.InvariantCulture);

            var discovery = new LinuxManagerProcessDiscovery();
            ManagerProcessDiscoveryResult discovered = ManagerProcessDiscoveryResult.Unavailable(
                ManagerProcessDiscoveryStatus.Incomplete,
                "not-observed");
            var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while (DateTimeOffset.UtcNow < deadline)
            {
                discovered = await discovery.ProbeAsync(childProcessId);
                if (discovered is { Status: ManagerProcessDiscoveryStatus.Available, Evidence: not null } &&
                    discovered.Evidence.ParentProcessId != originalParentProcessId)
                {
                    break;
                }

                await Task.Delay(25);
            }

            Assert.Equal(ManagerProcessDiscoveryStatus.Available, discovered.Status);
            Assert.NotNull(discovered.Evidence);
            Assert.NotEqual(originalParentProcessId, discovered.Evidence.ParentProcessId);

            using var child = Process.GetProcessById(childProcessId);
            var executablePath = child.MainModule!.FileName;
            var hostIdentity = new WorkspaceOwnedProcessIdentity(
                childProcessId,
                new DateTimeOffset(child.StartTime.ToUniversalTime()),
                ComputeExecutablePathFingerprint(executablePath),
                new WorkspaceOwnedProcessBoundary(
                    OperatingSystem.IsWindows()
                        ? WorkspaceOwnedProcessBoundaryKind.WindowsJobObject
                        : WorkspaceOwnedProcessBoundaryKind.UnixProcessGroup,
                    OperatingSystem.IsWindows() ? 0 : originalParentProcessId,
                    OperatingSystem.IsWindows() ? Guid.NewGuid() : Guid.Empty));
            var now = DateTimeOffset.UtcNow;
            var record = new ManagerOwnedProcessRecord(
                Guid.NewGuid(),
                ManagerProcessPurpose.DotnetWatch,
                hostIdentity,
                discovered.Evidence.StartIdentity,
                discovered.Evidence.ExecutablePath,
                ManagerProcessFingerprint.ComputeArguments(executablePath, ["30"]),
                discovered.Evidence.ObservedCommandFingerprint,
                root,
                discovered.Evidence.OwnerIdentity,
                originalParentProcessId,
                "reparent-integration",
                ManagerProcessLifecycleState.Running,
                now,
                now);
            var registry = new InMemoryRegistry();
            await registry.UpsertAsync(record);
            var coordinator = new ManagerProcessCoordinator(
                processHost,
                discovery,
                registry,
                new PhysicalFileSystemPathPolicyFactory(),
                ManagerHostKind.Linux);

            var results = await coordinator.ReclaimRegisteredAsync(
                ManagerProcessPurpose.DotnetWatch,
                "reparent-integration-cleanup");

            var termination = Assert.Single(results);
            Assert.Equal(WorkspaceProcessTerminationStatus.Terminated, termination.Status);
            Assert.False(termination.ResidualProcessPossible);
            Assert.Equal(
                ManagerProcessLifecycleState.Terminated,
                Assert.Single(await registry.ReadAllAsync()).State);
            childProcessId = 0;
        }
        finally
        {
            if (childProcessId > 0)
            {
                try
                {
                    using var child = Process.GetProcessById(childProcessId);
                    child.Kill(entireProcessTree: true);
                }
                catch (Exception exception) when (
                    exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                }
            }

            Directory.Delete(root, recursive: true);
        }
    }

    private static string ComputeExecutablePathFingerprint(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var finalTarget = File.ResolveLinkTarget(fullPath, returnFinalTarget: true);
        var identityPath = finalTarget?.FullName ?? fullPath;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identityPath))).ToLowerInvariant();
    }

    private sealed class InMemoryRegistry : IManagerOwnedProcessRegistry
    {
        private readonly Dictionary<Guid, ManagerOwnedProcessRecord> records = [];

        public Task<IReadOnlyList<ManagerOwnedProcessRecord>> ReadAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ManagerOwnedProcessRecord>>(records.Values.ToArray());

        public Task UpsertAsync(ManagerOwnedProcessRecord record, CancellationToken cancellationToken = default)
        {
            records[record.LeaseId] = record;
            return Task.CompletedTask;
        }
    }
}
