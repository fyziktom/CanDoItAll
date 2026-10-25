using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "LiveProcess")]
[Trait("Category", "HostPlatform")]
public sealed class DatasetActivationCommandProcessTests {
    [Fact]
    public async Task Offline_command_processes_bootstrap_resume_and_reactivate_only_the_owned_profile_without_starting_the_web_host() {
        await using var fixture = await Fixture.CreateAsync();
        Assert.Null(await fixture.ReadFileAsync());
        var invalidOptions = await fixture.RunAsync("bootstrap", succeeds: false, taskPlanAdmissionWindow: TimeSpan.Zero);
        Assert.Contains("Microsoft.Extensions.Options.OptionsValidationException", invalidOptions, StringComparison.Ordinal);
        Assert.Null(await fixture.ReadFileAsync());
        await using (var untouched = fixture.Projects()) {
            Assert.False(await untouched.Database.SqlQueryRaw<bool>(
                "SELECT to_regclass('\"__EFMigrationsHistory\"') IS NOT NULL AS \"Value\"").SingleAsync());
        }
        await fixture.RunAsync("bootstrap");
        var originalFile = (await fixture.ReadFileAsync())!;
        var original = await fixture.ReadSqlAsync();
        Assert.Equal(DatasetActivationFileState.Active, originalFile.State);
        Assert.Equal(originalFile.Plan.Binding.Namespace.Value, original.Namespace);
        Assert.Equal(originalFile.Plan.Binding.MaintenanceId, original.Maintenance);
        Assert.Equal(DatasetLifecycleState.Active, original.State);
        AssertCurrentPools(original);

        await fixture.RunAsync("resume");
        Assert.Equal(originalFile, await fixture.ReadFileAsync());
        AssertSqlEqual(original, await fixture.ReadSqlAsync());
        var maintenance = Guid.NewGuid();
        await fixture.RunAsync("reactivate", extra: ["--expected-namespace", original.Namespace.ToString("D"), "--maintenance-id", maintenance.ToString("D")]);
        var activatedFile = (await fixture.ReadFileAsync())!;
        var activated = await fixture.ReadSqlAsync();
        Assert.NotEqual(original.Namespace, activated.Namespace);
        Assert.Equal(maintenance, activated.Maintenance);
        Assert.Equal(activated.Namespace, activatedFile.Plan.Binding.Namespace.Value);
        Assert.Equal(originalFile.Plan.Binding, activatedFile.Plan.ExpectedPreviousSqlBinding);
        AssertCurrentPools(activated);
        Assert.Equal(4, activated.Epochs.Length);
        Assert.Equal(4, activated.OwnerStates.Length);
        foreach (var firstEpoch in original.Epochs) {
            var closed = Assert.Single(activated.Epochs, epoch => epoch.Namespace == original.Namespace && epoch.Pool == firstEpoch.Pool);
            Assert.Equal(FiniteOperationEpochState.Closed, closed.State);
            Assert.NotNull(closed.ClosedAt);
            Assert.Equal(firstEpoch with { State = FiniteOperationEpochState.Closed, ClosedAt = closed.ClosedAt }, closed);
            Assert.Equal(Assert.Single(original.OwnerStates, state => state.Pool == firstEpoch.Pool),
                Assert.Single(activated.OwnerStates, state => state.Namespace == original.Namespace && state.Pool == firstEpoch.Pool));
        }
        await fixture.RunAsync("resume");
        AssertSqlEqual(activated, await fixture.ReadSqlAsync());
        await fixture.RunAsync("reactivate", succeeds: false,
            extra: ["--expected-namespace", original.Namespace.ToString("D"), "--maintenance-id", Guid.NewGuid().ToString("D")]);
        Assert.Equal(activatedFile, await fixture.ReadFileAsync());
        AssertSqlEqual(activated, await fixture.ReadSqlAsync());
    }

    [Fact]
    public async Task Offline_resume_process_completes_the_exact_prepared_bootstrap_after_SQL_rollback() {
        await using var fixture = await Fixture.CreateAsync();
        await using (var schema = await fixture.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            await schema.Database.MigrateAsync();
            await schema.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION activation_cli_reject_epoch() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Synthetic CLI epoch initialization interruption'; END $$;
                CREATE TRIGGER activation_cli_reject_epoch BEFORE INSERT ON "Infrastructure_FiniteOperationEpochs"
                FOR EACH ROW EXECUTE FUNCTION activation_cli_reject_epoch();
                """);
        }
        var bootstrapFailure = await fixture.RunAsync("bootstrap", succeeds: false);
        var preparedFile = await fixture.ReadFileAsync();
        Assert.True(preparedFile is not null, bootstrapFailure);
        var prepared = Assert.IsType<DatasetActivationFileRecord>(preparedFile);
        Assert.Equal(DatasetActivationFileState.Prepared, prepared.State);
        await using (var owner = fixture.Projects()) {
            Assert.Empty(await owner.Set<FiniteOperationStateRecord>().ToArrayAsync());
            Assert.Empty(await owner.Set<FiniteOperationEpochRecord>().ToArrayAsync());
            await owner.Database.ExecuteSqlRawAsync("DROP TRIGGER activation_cli_reject_epoch ON \"Infrastructure_FiniteOperationEpochs\"");
        }
        await fixture.RunAsync("resume");
        var completed = (await fixture.ReadFileAsync())!;
        Assert.Equal(prepared.Plan, completed.Plan);
        Assert.Equal(DatasetActivationFileState.Active, completed.State);
        var sql = await fixture.ReadSqlAsync();
        Assert.Equal(prepared.Plan.Binding.Namespace.Value, sql.Namespace);
        Assert.Equal(prepared.Plan.Binding.MaintenanceId, sql.Maintenance);
        AssertCurrentPools(sql);
        await fixture.RunAsync("resume");
        AssertSqlEqual(sql, await fixture.ReadSqlAsync());
    }

    private static void AssertSqlEqual(SqlSnapshot expected, SqlSnapshot actual) {
        Assert.Equal(expected.Namespace, actual.Namespace);
        Assert.Equal(expected.Maintenance, actual.Maintenance);
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.Epochs, actual.Epochs);
        Assert.Equal(expected.OwnerStates, actual.OwnerStates);
    }

    private static void AssertCurrentPools(SqlSnapshot snapshot) {
        Guid[] expected = [ProjectFiniteOperationPool.Pool.Value, WorkbenchTaskPlanFiniteOperations.Pool.Value];
        var currentEpochs = snapshot.Epochs.Where(epoch => epoch.Namespace == snapshot.Namespace).ToArray();
        var currentStates = snapshot.OwnerStates.Where(state => state.Namespace == snapshot.Namespace).ToArray();
        Assert.Equal(expected.Order(), currentEpochs.Select(epoch => epoch.Pool).Order());
        Assert.Equal(expected.Order(), currentStates.Select(state => state.Pool).Order());
        Assert.All(currentEpochs, epoch => {
            Assert.Equal(1, epoch.Epoch);
            Assert.Equal(FiniteOperationEpochState.Open, epoch.State);
            Assert.Null(epoch.ClosedAt);
            Assert.Equal(TimeSpan.FromDays(1), epoch.AdmissionClosesAt - epoch.OpenedAt);
            Assert.Equal(30000, epoch.MutationDeadlineMilliseconds);
            Assert.Equal(604800, epoch.ReplayRetentionSeconds);
        });
        Assert.All(currentStates, state => {
            Assert.Equal(1, state.ProtocolVersion);
            Assert.Equal(1, state.CurrentEpoch);
            Assert.Equal(0, state.RetiredThrough);
        });
    }

    private sealed record EpochObservation(Guid Namespace, Guid Pool, long Epoch, FiniteOperationEpochState State, DateTimeOffset OpenedAt,
        DateTimeOffset AdmissionClosesAt, DateTimeOffset? ClosedAt, int MutationDeadlineMilliseconds, long ReplayRetentionSeconds);
    private sealed record OwnerState(Guid Namespace, Guid Pool, int ProtocolVersion, long CurrentEpoch, long RetiredThrough);
    private sealed record SqlSnapshot(Guid Namespace, Guid Maintenance, DatasetLifecycleState State, EpochObservation[] Epochs, OwnerState[] OwnerStates);

    private sealed class Fixture(CanDoItAllTestEnvironment environment, TestDatabaseProfile profile,
        ServiceProvider services, ResolvedDatabaseProfile resolved, TcpListener blockedPort) : IAsyncDisposable {
        internal ServiceProvider Services => services;
        private string ResourceRecord => Path.Combine(environment.RootPath, "owned-dataset-activation-process.json");
        private int sequence;

        internal static async Task<Fixture> CreateAsync() {
            var environment = CanDoItAllTestEnvironment.Create("dataset-activation-cli");
            ServiceProvider? provider = null;
            TcpListener? port = null;
            try {
                var profile = environment.CreatePostgreSqlProfile("offline-command");
                var registrations = new ServiceCollection();
                TestApplicationBootstrap.ConfigureDefaultServices(registrations,
                    TestApplicationBootstrap.BuildConfiguration(profile, Overrides(environment)),
                    environment.CreateHostEnvironment("CanDoItAll.Tests.Integration"));
                provider = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
                var resolved = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>().ResolveCurrentProfile();
                Assert.Equal(DatabaseProviderKind.PostgreSql, resolved.Profile.ProviderKind);
                Assert.Equal(profile.WorkspaceRootPath, resolved.Profile.Storage.WorkspaceRoot);
                port = new TcpListener(IPAddress.Loopback, 0);
                port.Server.ExclusiveAddressUse = true;
                port.Start(1);
                return new(environment, profile, provider, resolved, port);
            } catch {
                port?.Stop();
                try {
                    if (provider is not null) {
                        await provider.DisposeAsync();
                    }
                } finally {
                    await environment.DisposeAsync();
                }
                throw;
            }
        }

        internal AppDbContext Projects() {
            var builder = new DbContextOptionsBuilder<AppDbContext>();
            AppDbContextOptionsConfigurator.Configure(builder, resolved);
            return new(builder.Options);
        }

        internal async Task<DatasetActivationFileRecord?> ReadFileAsync() {
            using var lease = services.GetRequiredService<DatasetActivationService>().Acquire(resolved.Profile.Id);
            return await lease.ReadAsync();
        }

        internal async Task<SqlSnapshot> ReadSqlAsync() {
            var builder = new DbContextOptionsBuilder<DatasetLifecycleDbContext>();
            AppDbContextOptionsConfigurator.Configure(builder, resolved);
            await using var lifecycle = new DatasetLifecycleDbContext(builder.Options);
            var active = await lifecycle.Set<DatasetLifecycleRecord>().AsNoTracking().SingleAsync();
            await using var owner = Projects();
            var epochs = await owner.Set<FiniteOperationEpochRecord>().AsNoTracking().OrderBy(row => row.NamespaceId)
                .ThenBy(row => row.PoolId).ThenBy(row => row.Epoch)
                .Select(row => new EpochObservation(row.NamespaceId, row.PoolId, row.Epoch, row.State, row.OpenedAtUtc,
                    row.AdmissionClosesAtUtc, row.ClosedAtUtc, row.MutationDeadlineMilliseconds, row.ReplayRetentionSeconds)).ToArrayAsync();
            var states = await owner.Set<FiniteOperationStateRecord>().AsNoTracking().OrderBy(row => row.NamespaceId).ThenBy(row => row.PoolId)
                .Select(row => new OwnerState(row.NamespaceId, row.PoolId, row.RetentionProtocolVersion, row.CurrentEpoch, row.RetiredThroughEpoch)).ToArrayAsync();
            return new(active.NamespaceId, active.MaintenanceId, active.State, epochs, states);
        }

        internal async Task<string> RunAsync(string action, bool succeeds = true, string[]? extra = null,
            TimeSpan? taskPlanAdmissionWindow = null) {
            var assembly = IntegrationTestPaths.ResolveProjectOutputAssembly(Path.Combine("App", "CanDoItAll.Web"), "CanDoItAll.Web.dll");
            var endpoint = $"http://127.0.0.1:{((IPEndPoint)blockedPort.LocalEndpoint).Port}";
            var start = new ProcessStartInfo("dotnet") {
                WorkingDirectory = environment.RootPath, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var key in start.Environment.Keys.Where(key => new[] { "Database__", "ControlPlane__", "Storage__", "SecretVault__", "DataProtection__", "Kestrel__", "Projects__DefinitionOperations__", "Workbench__TaskPlanOperations__" }
                    .Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).ToArray()) {
                start.Environment.Remove(key);
            }
            foreach (var pair in profile.CreateEnvironmentVariables(Overrides(environment))) {
                start.Environment[pair.Key] = pair.Value;
            }
            start.Environment["Database__PasswordFile"] = string.Empty;
            start.Environment["DOTNET_ENVIRONMENT"] = "Development";
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            start.Environment["ASPNETCORE_URLS"] = endpoint;
            start.Environment["ASPNETCORE_HTTP_PORTS"] = string.Empty;
            start.Environment["ASPNETCORE_HTTPS_PORTS"] = string.Empty;
            if (taskPlanAdmissionWindow is { } admissionWindow) {
                start.Environment["Workbench__TaskPlanOperations__AdmissionWindow"] = admissionWindow.ToString("c", System.Globalization.CultureInfo.InvariantCulture);
            }
            start.ArgumentList.Add(assembly);
            start.ArgumentList.Add("dataset-activation");
            start.ArgumentList.Add(action);
            start.ArgumentList.Add("--profile-id");
            start.ArgumentList.Add(resolved.Profile.Id.ToString("D"));
            start.ArgumentList.Add("--writers-stopped");
            foreach (var argument in extra ?? []) {
                start.ArgumentList.Add(argument);
            }
            var ownedSequence = ++sequence;
            await RecordAsync("prepared", null, null);
            using var child = new Process { StartInfo = start };
            Assert.True(child.Start(), "The owned offline activation child could not start.");
            var output = DrainTailAsync(child.StandardOutput);
            var error = DrainTailAsync(child.StandardError);
            string diagnostics;
            try {
                await RecordAsync("running", child.Id, null);
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                await child.WaitForExitAsync(deadline.Token);
                var tail = await output;
                var errorTail = await error;
                diagnostics = DescribeFailure(tail + "\n" + errorTail);
                Assert.True(succeeds == (child.ExitCode == 0),
                    $"Offline action {action} returned exit code {child.ExitCode}; expected success: {succeeds}. {diagnostics}");
                if (succeeds) {
                    Assert.True(tail.Contains("Dataset activation complete.", StringComparison.Ordinal), "The child did not report successful activation; raw process diagnostics are suppressed.");
                }
                Assert.False(tail.Contains("Now listening on:", StringComparison.Ordinal), "The offline child started a web listener.");
                Assert.True(blockedPort.Server.IsBound);
            } finally {
                if (!child.HasExited) {
                    child.Kill(entireProcessTree: true);
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await child.WaitForExitAsync(cleanup.Token);
                }
                await Task.WhenAll(output, error);
                await RecordAsync("exited", child.Id, child.ExitCode);
                Assert.True(child.HasExited);
            }
            return diagnostics;

            Task RecordAsync(string state, int? childId, int? exitCode)
                => File.WriteAllTextAsync(ResourceRecord, JsonSerializer.Serialize(new {
                    Owner = nameof(DatasetActivationCommandProcessTests), Sequence = ownedSequence, Action = action, State = state,
                    ParentProcessId = Environment.ProcessId, ChildProcessId = childId, ExitCode = exitCode,
                    ProfileId = resolved.Profile.Id, ProfileKey = profile.ProfileKey, Root = environment.RootPath, BlockedEndpoint = endpoint
                }));
        }

        private static async Task<string> DrainTailAsync(StreamReader stream) {
            const int maximum = 4096;
            var buffer = new char[maximum];
            var head = new StringBuilder(maximum);
            var tail = new StringBuilder(maximum * 2);
            while (true) {
                var read = await stream.ReadAsync(buffer.AsMemory());
                if (read == 0) {
                    return head.ToString() + "\n" + tail;
                }
                head.Append(buffer, 0, Math.Min(read, maximum - head.Length));
                tail.Append(buffer, 0, read);
                if (tail.Length > maximum) {
                    tail.Remove(0, tail.Length - maximum);
                }
            }
        }

        private static string DescribeFailure(string errorTail) {
            var types = Regex.Matches(errorTail, @"\b(?:System|Microsoft|CanDoItAll)\.[A-Za-z0-9_.`+]+Exception\b")
                .Select(match => match.Value).Distinct(StringComparer.Ordinal).Take(8);
            var dependencies = Regex.Matches(errorTail, @"Unable to resolve service for type '[A-Za-z0-9_.`+]+(?:\[[A-Za-z0-9_.,`+ ]+\])?'")
                .Select(match => match.Value).Distinct(StringComparer.Ordinal).Take(8);
            return $"Exception types: {string.Join(", ", types)}. Missing services: {string.Join(", ", dependencies)}. Raw child output is suppressed.";
        }

        private static IReadOnlyDictionary<string, string?> Overrides(CanDoItAllTestEnvironment environment) => new Dictionary<string, string?> {
            ["ControlPlane:RootPath"] = environment.ControlPlaneRootPath,
            ["ControlPlane:StateRootPath"] = Path.Combine(environment.RootPath, "state"),
            ["ControlPlane:LogsRootPath"] = Path.Combine(environment.RootPath, "logs"),
            ["ControlPlane:RuntimeTemporaryRootPath"] = Path.Combine(environment.RootPath, "runtime"),
            ["Database:PasswordFile"] = string.Empty,
            ["DevelopmentManager:TuningModeEnabled"] = "false"
        };

        public async ValueTask DisposeAsync() {
            blockedPort.Stop();
            try {
                await services.DisposeAsync();
            } finally {
                await environment.DisposeAsync();
            }
        }
    }
}
