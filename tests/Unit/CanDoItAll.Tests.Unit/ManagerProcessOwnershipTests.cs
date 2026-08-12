using System.Buffers.Binary;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Manager;

namespace CanDoItAll.Tests.Unit;

public sealed class ManagerProcessOwnershipTests
{
    [Fact]
    public void Verifier_rejects_pid_reuse_and_incomplete_identity_evidence()
    {
        var record = CreateRecord();
        var verifier = CreateVerifier(ManagerHostKind.Linux, StringComparer.Ordinal);

        var reused = verifier.Verify(
            record,
            ManagerProcessDiscoveryResult.Available(CreateEvidence() with { StartIdentity = "linux-proc-start:other" }));
        var missing = verifier.Verify(
            record,
            ManagerProcessDiscoveryResult.Unavailable(
                ManagerProcessDiscoveryStatus.PermissionDenied,
                "linux-proc-permission-denied"));

        Assert.Equal(ManagerProcessOwnershipStatus.Unverified, reused.Status);
        Assert.Equal("start-identity-mismatch", reused.DiagnosticCode);
        Assert.Equal(ManagerProcessOwnershipStatus.Unverified, missing.Status);
        Assert.Equal("linux-proc-permission-denied", missing.DiagnosticCode);
    }

    [Fact]
    public void Verifier_requires_owner_executable_and_observed_command_evidence()
    {
        var record = CreateRecord();
        var verifier = CreateVerifier(ManagerHostKind.Linux, StringComparer.Ordinal);

        Assert.Equal(
            "owner-mismatch",
            verifier.Verify(record, ManagerProcessDiscoveryResult.Available(CreateEvidence() with { OwnerIdentity = "uid:2000" })).DiagnosticCode);
        Assert.Equal(
            "executable-mismatch",
            verifier.Verify(record, ManagerProcessDiscoveryResult.Available(CreateEvidence() with { ExecutablePath = AlternateExecutablePath() })).DiagnosticCode);
        Assert.Equal(
            "command-mismatch",
            verifier.Verify(record, ManagerProcessDiscoveryResult.Available(CreateEvidence() with { ObservedCommandFingerprint = new string('a', 64) })).DiagnosticCode);
    }

    [Fact]
    public void Verifier_preserves_case_distinct_executable_identity_on_linux()
    {
        var record = CreateRecord() with { ExecutablePath = Path.Combine(Path.GetTempPath(), "Manager", "Dotnet") };
        var evidence = CreateEvidence() with { ExecutablePath = Path.Combine(Path.GetTempPath(), "Manager", "dotnet") };

        var result = CreateVerifier(ManagerHostKind.Linux, StringComparer.Ordinal)
            .Verify(record, ManagerProcessDiscoveryResult.Available(evidence));

        Assert.Equal(ManagerProcessOwnershipStatus.Unverified, result.Status);
        Assert.Equal("executable-mismatch", result.DiagnosticCode);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Verifier_uses_detected_executable_filesystem_case_semantics(
        bool caseInsensitive,
        bool expectedVerified)
    {
        var record = CreateRecord() with { ExecutablePath = Path.Combine(Path.GetTempPath(), "Manager", "Dotnet") };
        var evidence = CreateEvidence() with { ExecutablePath = Path.Combine(Path.GetTempPath(), "Manager", "dotnet") };
        var comparer = caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        var result = CreateVerifier(ManagerHostKind.MacOs, comparer)
            .Verify(record, ManagerProcessDiscoveryResult.Available(evidence));

        Assert.Equal(expectedVerified, result.Status == ManagerProcessOwnershipStatus.Verified);
    }

    [Fact]
    public void Verifier_allows_verified_unix_reparenting_after_manager_restart()
    {
        var record = CreateRecord() with { ParentProcessId = 4_242 };
        var reparentedEvidence = CreateEvidence() with { ParentProcessId = 1 };

        var result = CreateVerifier(ManagerHostKind.Linux, StringComparer.Ordinal)
            .Verify(record, ManagerProcessDiscoveryResult.Available(reparentedEvidence));

        Assert.Equal(ManagerProcessOwnershipStatus.Verified, result.Status);
    }

    [Fact]
    public async Task Durable_registry_round_trips_without_raw_arguments_or_environment_values()
    {
        var root = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Manager.Registry.{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
            var first = new FileManagerOwnedProcessRegistry(root, writer);
            var record = CreateRecord() with
            {
                PlannedArgumentsFingerprint = ManagerProcessFingerprint.ComputeArguments(
                    ExecutablePath(),
                    ["--token", "sentinel-secret-value"])
            };

            await first.UpsertAsync(record);
            var second = new FileManagerOwnedProcessRegistry(root, writer);
            var restored = Assert.Single(await second.ReadAllAsync());
            var json = await File.ReadAllTextAsync(Path.Combine(root, "manager-process-registry.json"));

            Assert.Equal(record, restored);
            Assert.DoesNotContain("sentinel-secret-value", json, StringComparison.Ordinal);
            Assert.DoesNotContain("--token", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Reclaim_does_not_terminate_when_recovery_evidence_is_ambiguous()
    {
        var record = CreateRecord();
        var registry = new InMemoryRegistry(record);
        var host = new FakeProcessHost();
        var coordinator = new ManagerProcessCoordinator(
            host,
            new FixedDiscovery(ManagerProcessDiscoveryResult.Available(CreateEvidence() with { OwnerIdentity = "uid:other" })),
            registry,
            new PhysicalFileSystemPathPolicyFactory(),
            ManagerHostKind.Linux);

        var results = await coordinator.ReclaimRegisteredAsync(
            ManagerProcessPurpose.DotnetWatch,
            "test-reclaim");

        var result = Assert.Single(results);
        Assert.Equal(WorkspaceProcessTerminationStatus.IdentityMismatch, result.Status);
        Assert.Equal(0, host.TerminationCount);
        Assert.Equal(ManagerProcessLifecycleState.OwnershipUnverified, Assert.Single(await registry.ReadAllAsync()).State);
    }

    [Fact]
    public async Task Coordinator_registers_complete_launch_evidence_before_returning_lease()
    {
        var root = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Manager.Launch.{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var recordStore = new InMemoryRegistry();
            var host = new FakeProcessHost();
            var coordinator = new ManagerProcessCoordinator(
                host,
                new FixedDiscovery(ManagerProcessDiscoveryResult.Available(CreateEvidence())),
                recordStore,
                new PhysicalFileSystemPathPolicyFactory(),
                ManagerHostKind.Linux);

            await using var lease = await coordinator.StartAsync(
                new ManagerProcessLaunchRequest(
                    ManagerProcessPurpose.DotnetWatch,
                    "workspace_dotnet_manager_watch",
                    "manager.dotnet-watch.v1",
                    "dotnet",
                    ["watch", "--project", "sample.csproj"],
                    root,
                    new Dictionary<string, string?> { ["ASPNETCORE_ENVIRONMENT"] = "Development" },
                    root,
                    "test"));

            var registered = Assert.Single(await recordStore.ReadAllAsync());
            Assert.Equal(ManagerProcessLifecycleState.Running, registered.State);
            Assert.Equal(host.Identity, registered.HostIdentity);
            Assert.Equal("linux-proc-start:123456", registered.RecoveryStartIdentity);
            Assert.NotEmpty(registered.PlannedArgumentsFingerprint);
            Assert.Equal("uid:1000", registered.OwnerIdentity);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Coordinator_refuses_duplicate_or_unverified_active_lease()
    {
        var root = Path.Combine(Path.GetTempPath(), $"CanDoItAll.Manager.Duplicate.{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var registry = new InMemoryRegistry();
            var coordinator = new ManagerProcessCoordinator(
                new FakeProcessHost(),
                new FixedDiscovery(ManagerProcessDiscoveryResult.Available(CreateEvidence())),
                registry,
                new PhysicalFileSystemPathPolicyFactory(),
                ManagerHostKind.Linux);
            var request = new ManagerProcessLaunchRequest(
                ManagerProcessPurpose.DotnetWatch,
                "workspace_dotnet_manager_watch",
                "manager.dotnet-watch.v1",
                "dotnet",
                ["watch"],
                root,
                new Dictionary<string, string?>(),
                root,
                "WatchSupervisorService");
            await using var first = await coordinator.StartAsync(request);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartAsync(request));

            Assert.Contains("duplicate process launch", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Lease_completion_retries_registry_persistence_after_a_transient_failure()
    {
        var registry = new FailOnceRegistry();
        var session = new FakeSession(CreateRecord().HostIdentity);
        await using var lease = new ManagerProcessLease(session, CreateRecord(), registry);

        await Assert.ThrowsAsync<IOException>(() => lease.WaitForExitAsync());
        await lease.DisposeAsync();

        Assert.Equal(2, registry.Attempts);
        Assert.Equal(ManagerProcessLifecycleState.Terminated, registry.Record!.State);
        Assert.Equal("lease-disposed", registry.Record.DiagnosticCode);
    }

    [Fact]
    public void Linux_proc_parser_handles_names_with_spaces_and_collects_bounded_identity()
    {
        var fields = new List<string> { "S", "42" };
        fields.AddRange(Enumerable.Repeat("0", 17));
        fields.Add("123456");
        var stat = $"321 (worker with spaces) {string.Join(' ', fields)}";
        var command = Encoding.UTF8.GetBytes("/usr/bin/dotnet\0watch\0--project\0sample.csproj\0");

        var parsed = LinuxManagerProcessDiscovery.TryParse(
            321,
            stat,
            "Name:\tworker\nUid:\t1000\t1000\t1000\t1000\n",
            command,
            "/usr/bin/dotnet",
            out var evidence);

        Assert.True(parsed);
        Assert.NotNull(evidence);
        Assert.Equal("linux-proc-start:123456", evidence.StartIdentity);
        Assert.Equal("uid:1000", evidence.OwnerIdentity);
        Assert.Equal(42, evidence.ParentProcessId);
        Assert.Equal(ManagerProcessFingerprint.ComputeObservedCommand(command), evidence.ObservedCommandFingerprint);
    }

    [Fact]
    public void Mac_parser_is_invariant_and_rejects_non_rooted_executable_evidence()
    {
        const string valid = "321 42 501 Tue Aug 11 12:34:56 2026 /usr/local/bin/dotnet /usr/local/bin/dotnet watch --project sample.csproj";
        const string ambiguous = "321 42 501 Tue Aug 11 12:34:56 2026 dotnet dotnet watch --project sample.csproj";

        Assert.True(MacOsManagerProcessDiscovery.TryParse(321, valid, out var evidence));
        Assert.Equal("uid:501", evidence!.OwnerIdentity);
        Assert.False(MacOsManagerProcessDiscovery.TryParse(321, ambiguous, out _));
        Assert.False(MacOsManagerProcessDiscovery.TryParse(321, valid.Replace("Aug", "ago", StringComparison.Ordinal), out _));
    }

    [Fact]
    public void Mac_native_identity_parser_preserves_kernel_microseconds()
    {
        var buffer = new byte[136];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(12), 321);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(20), 501);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(120), 1_786_450_496);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(128), 123_456);

        var parsed = LibProcMacProcessIdentityReader.TryParseBuffer(321, buffer, out var identity);

        Assert.True(parsed);
        Assert.Equal("macos-kernel-start:1786450496:123456", identity!.StartIdentity);
        Assert.Equal(42, identity.ParentProcessId);
        Assert.Equal((uint)501, identity.UserId);
    }

    [Fact]
    public async Task Mac_probe_combines_kernel_start_and_parent_with_strict_command_evidence()
    {
        const string output = "321 42 501 Tue Aug 11 12:34:56 2026 /usr/local/bin/dotnet /usr/local/bin/dotnet watch --project sample.csproj";
        var discovery = new MacOsManagerProcessDiscovery(
            new FixedMacCommandRunner(new MacProcessCommandResult(0, output, "")),
            new FixedMacIdentityReader(AvailableMacIdentity()));

        var result = await discovery.ProbeAsync(321);

        Assert.Equal(ManagerProcessDiscoveryStatus.Available, result.Status);
        Assert.Equal("macos-kernel-start:1786450496:123456", result.Evidence!.StartIdentity);
        Assert.Equal(42, result.Evidence.ParentProcessId);
    }

    [Theory]
    [InlineData(WorkspaceProcessTerminationReason.TimedOut, "macos-process-query-timeout")]
    [InlineData(WorkspaceProcessTerminationReason.CallerCanceled, "macos-process-query-cancelled")]
    [InlineData(WorkspaceProcessTerminationReason.StartFailed, "macos-process-query-start-failed")]
    [InlineData(WorkspaceProcessTerminationReason.TerminationFailed, "macos-process-query-termination-failed")]
    public async Task Mac_probe_does_not_treat_failed_or_interrupted_query_as_exited(
        WorkspaceProcessTerminationReason terminationReason,
        string expectedDiagnostic)
    {
        var discovery = new MacOsManagerProcessDiscovery(
            new FixedMacCommandRunner(new MacProcessCommandResult(-1, "", "", terminationReason)),
            new FixedMacIdentityReader(AvailableMacIdentity()));

        var result = await discovery.ProbeAsync(321);

        Assert.Equal(ManagerProcessDiscoveryStatus.Incomplete, result.Status);
        Assert.Equal(expectedDiagnostic, result.DiagnosticCode);
    }

    [Theory]
    [InlineData("Operation not permitted")]
    [InlineData("permission denied")]
    [InlineData("not authorized")]
    public async Task Mac_probe_maps_permission_failures_without_authorizing_exit(string error)
    {
        var discovery = new MacOsManagerProcessDiscovery(
            new FixedMacCommandRunner(new MacProcessCommandResult(1, "", error)),
            new FixedMacIdentityReader(AvailableMacIdentity()));

        var result = await discovery.ProbeAsync(321);

        Assert.Equal(ManagerProcessDiscoveryStatus.PermissionDenied, result.Status);
        Assert.Equal("macos-process-query-permission-denied", result.DiagnosticCode);
    }

    [Fact]
    public async Task Mac_probe_keeps_unknown_nonzero_query_failure_incomplete()
    {
        var discovery = new MacOsManagerProcessDiscovery(
            new FixedMacCommandRunner(new MacProcessCommandResult(1, "", "ps: query failed")),
            new FixedMacIdentityReader(AvailableMacIdentity()));

        var result = await discovery.ProbeAsync(321);

        Assert.Equal(ManagerProcessDiscoveryStatus.Incomplete, result.Status);
        Assert.Equal("macos-process-query-failed", result.DiagnosticCode);
    }

    [Fact]
    public void Windows_mapper_requires_complete_typed_identity_evidence()
    {
        var startedAtUtc = new DateTime(2026, 8, 11, 12, 34, 56, DateTimeKind.Utc);

        var mapped = WindowsProcessEvidenceMapper.TryCreate(
            321,
            Environment.ProcessId,
            startedAtUtc,
            OperatingSystem.IsWindows() ? @"C:\Program Files\dotnet\dotnet.exe" : "/opt/dotnet/dotnet",
            "dotnet watch --project sample.csproj",
            @"WORKSTATION\developer",
            out var evidence);

        Assert.True(mapped);
        Assert.Equal($"windows-start:{startedAtUtc.Ticks}", evidence!.StartIdentity);
        Assert.False(WindowsProcessEvidenceMapper.TryCreate(
            321,
            Environment.ProcessId,
            DateTime.SpecifyKind(startedAtUtc, DateTimeKind.Unspecified),
            evidence.ExecutablePath,
            "dotnet watch --project sample.csproj",
            evidence.OwnerIdentity,
            out _));
    }

    [Fact]
    public void Tuning_argument_tokenizer_preserves_windows_paths_and_rejects_unterminated_quotes()
    {
        var path = @"C:\repositories\CanDoItAll\request file.json";

        var arguments = ManagerCommandLineTokenizer.Tokenize($"--input \"{path}\" --mode safe");

        Assert.Equal(["--input", path, "--mode", "safe"], arguments);
        Assert.Throws<InvalidOperationException>(() => ManagerCommandLineTokenizer.Tokenize("--input \"unfinished"));
    }

    [Fact]
    public void Tuning_argument_builder_does_not_reparse_substituted_unix_filename_characters()
    {
        var root = Path.GetFullPath(Path.GetTempPath());
        var requestPath = Path.Combine(root, "request \"quoted\" file.json");
        var context = new TuningExecutionContext(
            Guid.NewGuid(),
            root,
            root,
            requestPath,
            Path.Combine(root, "stdout.log"),
            Path.Combine(root, "stderr.log"),
            Path.Combine(root, "events.jsonl"));

        var arguments = ManagerTuningArgumentBuilder.Build("--input \"{requestPath}\"", context);

        Assert.Equal(["--input", requestPath], arguments);
    }

    private static ManagerOwnedProcessRecord CreateRecord()
    {
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        return new ManagerOwnedProcessRecord(
            Guid.Parse("bca18f44-f3eb-482c-b027-65947ad25a6c"),
            ManagerProcessPurpose.DotnetWatch,
            new WorkspaceOwnedProcessIdentity(321, now, new string('b', 64)),
            "linux-proc-start:123456",
            ExecutablePath(),
            new string('c', 64),
            new string('d', 64),
            Path.GetFullPath(Path.GetTempPath()),
            "uid:1000",
            Environment.ProcessId,
            "test",
            ManagerProcessLifecycleState.Running,
            now,
            now);
    }

    private static ManagerProcessOwnershipVerifier CreateVerifier(
        ManagerHostKind hostKind,
        StringComparer executablePathComparer)
        => new(hostKind, new FixedPhysicalPathPolicyFactory(executablePathComparer));

    private static MacProcessIdentityReadResult AvailableMacIdentity()
        => MacProcessIdentityReadResult.Available(
            new MacProcessNativeIdentity(321, 42, 501, 1_786_450_496, 123_456));

    private static ManagerProcessEvidence CreateEvidence()
        => new(
            321,
            "linux-proc-start:123456",
            ExecutablePath(),
            new string('d', 64),
            "uid:1000",
            Environment.ProcessId);

    private static string ExecutablePath()
        => Path.Combine(Path.GetTempPath(), "Manager", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");

    private static string AlternateExecutablePath()
        => Path.Combine(Path.GetTempPath(), "Manager", OperatingSystem.IsWindows() ? "other.exe" : "other");

    private sealed class FixedDiscovery(ManagerProcessDiscoveryResult result) : IManagerProcessDiscovery
    {
        public Task<ManagerProcessDiscoveryResult> ProbeAsync(int processId, CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private sealed class FixedMacCommandRunner(MacProcessCommandResult result) : IMacProcessCommandRunner
    {
        public Task<MacProcessCommandResult> RunAsync(int processId, CancellationToken cancellationToken)
            => Task.FromResult(result);
    }

    private sealed class FixedMacIdentityReader(MacProcessIdentityReadResult result) : IMacProcessIdentityReader
    {
        public MacProcessIdentityReadResult Read(int processId) => result;
    }

    private sealed class FixedPhysicalPathPolicyFactory(StringComparer comparer) : IPhysicalFileSystemPathPolicyFactory
    {
        public IPhysicalFileSystemPathPolicy Create(string managedRoot)
            => new FixedPhysicalPathPolicy(managedRoot, comparer);
    }

    private sealed class FixedPhysicalPathPolicy(string managedRoot, StringComparer comparer) : IPhysicalFileSystemPathPolicy
    {
        public string RootPath { get; } = Path.GetFullPath(managedRoot);

        public PhysicalFileSystemCaseSensitivity CaseSensitivity { get; } = comparer.Equals("a", "A")
            ? PhysicalFileSystemCaseSensitivity.Insensitive
            : PhysicalFileSystemCaseSensitivity.Sensitive;

        public StringComparer PathComparer { get; } = comparer;

        public StringComparison PathComparison { get; } = comparer.Equals("a", "A")
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        public bool IsWithinRoot(string fullPath) => true;

        public string ResolveContainedPath(string path) => Path.GetFullPath(path);

        public void EnsureSafePath(string fullPath, bool allowMissingLeaf = false)
        {
        }

        public void RevalidateMutationTarget(string fullPath)
        {
        }
    }

    private sealed class InMemoryRegistry(params ManagerOwnedProcessRecord[] initial) : IManagerOwnedProcessRegistry
    {
        private readonly Dictionary<Guid, ManagerOwnedProcessRecord> records = initial.ToDictionary(record => record.LeaseId);

        public Task<IReadOnlyList<ManagerOwnedProcessRecord>> ReadAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ManagerOwnedProcessRecord>>(records.Values.ToArray());

        public Task UpsertAsync(ManagerOwnedProcessRecord record, CancellationToken cancellationToken = default)
        {
            records[record.LeaseId] = record;
            return Task.CompletedTask;
        }
    }

    private sealed class FailOnceRegistry : IManagerOwnedProcessRegistry
    {
        public int Attempts { get; private set; }

        public ManagerOwnedProcessRecord? Record { get; private set; }

        public Task<IReadOnlyList<ManagerOwnedProcessRecord>> ReadAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ManagerOwnedProcessRecord>>([]);

        public Task UpsertAsync(ManagerOwnedProcessRecord record, CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Attempts == 1)
            {
                throw new IOException("Injected transient registry failure.");
            }

            Record = record;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProcessHost : IWorkspaceLongRunningProcessHost
    {
        public WorkspaceOwnedProcessIdentity Identity { get; } = new(
            321,
            new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero),
            new string('b', 64));

        public int TerminationCount { get; private set; }

        public ExecutionBoundaryDescriptor DescribeBoundary()
            => new("test", "test", "test", "test", "test", true, "test");

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IWorkspaceProcessSession> StartSessionAsync(
            WorkspaceProcessSessionRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IWorkspaceProcessSession>(new FakeSession(Identity));

        public Task<WorkspaceProcessTerminationResult> TerminateOwnedProcessAsync(
            WorkspaceOwnedProcessIdentity identity,
            CancellationToken cancellationToken = default)
        {
            TerminationCount++;
            return Task.FromResult(new WorkspaceProcessTerminationResult(
                WorkspaceProcessTerminationStatus.Terminated,
                false,
                "terminated"));
        }
    }

    private sealed class FakeSession(WorkspaceOwnedProcessIdentity identity) : IWorkspaceProcessSession
    {
        public WorkspaceOwnedProcessIdentity Identity { get; } = identity;

        public bool HasExited { get; private set; }

        public WorkspaceProcessOutputSnapshot CaptureOutput() => new(string.Empty, string.Empty, false, false);

        public Task<WorkspaceProcessExecutionResult> WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            HasExited = true;
            return Task.FromResult(CreateExecutionResult());
        }

        public Task<WorkspaceProcessExecutionResult> TerminateAsync(
            WorkspaceProcessTerminationReason reason,
            string failureMessage,
            CancellationToken cancellationToken = default)
        {
            HasExited = true;
            return Task.FromResult(CreateExecutionResult() with { TerminationReason = reason });
        }

        public WorkspaceOwnedProcessIdentity Detach() => Identity;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static WorkspaceProcessExecutionResult CreateExecutionResult()
        {
            var now = DateTimeOffset.UtcNow;
            return new WorkspaceProcessExecutionResult(
                true,
                0,
                string.Empty,
                string.Empty,
                false,
                false,
                now,
                now,
                false,
                new ExecutionBoundaryDescriptor("test", "test", "test", "test", "test", true, "test"),
                string.Empty);
        }
    }
}
