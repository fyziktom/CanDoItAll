using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.ProcessRecovery;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "LiveProcess")]
[Trait("Category", "HostPlatform")]
public sealed class ProcessCancellationRecoveryRestartTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(ReadBasisMode.StepSnapshot, RecoveryHostMode.BeforeAccounting)]
    [InlineData(ReadBasisMode.StepSnapshot, RecoveryHostMode.AfterAccounting)]
    [InlineData(ReadBasisMode.RunSnapshot, RecoveryHostMode.BeforeAccounting)]
    [InlineData(ReadBasisMode.RunSnapshot, RecoveryHostMode.AfterAccounting)]
    public async Task Killed_worker_retains_original_action_and_restart_accounts_real_child_once(ReadBasisMode snapshot, RecoveryHostMode boundary) {
        await using var environment = CanDoItAllTestEnvironment.Create("candoitall-tests");
        var profile = environment.CreatePostgreSqlProfile("primary");
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(snapshot, environment, profile);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var token = deadline.Token;
        var stopped = await new ProcessRuntimeEngine(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>())
            .BeginRootCancellationAsync(fixture.State, fixture.CommandContext, token);
        Assert.True(stopped.CommittedNow);
        var profileId = fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id;
        await using var inspection = Context(fixture.Services);
        RecoveryHostFrame held;
        string originalIntent;
        ProcessRuntimeIdempotencyEntity[] receipts;
        await using (var first = await Child.StartAsync(profile, profileId, stopped.State.RootRunId.Value, boundary, output, token)) {
            held = await first.ReadAsync(token);
            Assert.Equal(RecoveryHostFrameKind.Held, held.Kind);
            Assert.Equal(stopped.State.RootRunId.Value, held.RootRunId);
            Assert.NotNull(held.ActionId);
            var queue = await inspection.CancellationRecoveries.AsNoTracking().SingleAsync(token);
            Assert.Equal(held.LeaseToken, queue.LeaseToken);
            Assert.Equal(held.LeaseGeneration, queue.LeaseGeneration);
            Assert.Equal(held.ProgressRevision, queue.ProgressRevision);
            Assert.False(queue.Completed);
            originalIntent = queue.IntentJson!;
            Assert.False(string.IsNullOrWhiteSpace(originalIntent));
            var child = await inspection.DecisionChildren.AsNoTracking().SingleAsync(token);
            Assert.Equal(boundary == RecoveryHostMode.AfterAccounting, child.OwnerRevision is not null);
            receipts = await inspection.IdempotencyKeys.AsNoTracking().Where(item => item.DecisionBudgetReceiptJson != null)
                .OrderBy(item => item.CommandId).ToArrayAsync(token);
            await first.KillAsync();
            var retained = await inspection.CancellationRecoveries.AsNoTracking().SingleAsync(token);
            Assert.Equal(originalIntent, retained.IntentJson);
            Assert.Equal(queue.PositionJson, retained.PositionJson);
            Assert.Equal(queue.ProgressRevision, retained.ProgressRevision);
        }
        await using (var second = await Child.StartAsync(profile, profileId, stopped.State.RootRunId.Value, RecoveryHostMode.Resume, output, token)) {
            var replays = 0;
            while (true) {
                var frame = await second.ReadAsync(token);
                Assert.Equal(held.RootRunId, frame.RootRunId);
                Assert.True(frame.ProcessId != held.ProcessId || frame.ProcessStartedUtc != held.ProcessStartedUtc);
                if (frame.Kind == RecoveryHostFrameKind.Completed) {
                    break;
                }
                Assert.Equal(RecoveryHostFrameKind.Replayed, frame.Kind);
                Assert.Equal(held.ActionId, frame.ActionId);
                Assert.Equal(held.ProgressRevision, frame.ProgressRevision);
                Assert.True(frame.LeaseGeneration > held.LeaseGeneration);
                Assert.NotEqual(held.LeaseToken, frame.LeaseToken);
                Assert.InRange(++replays, 1, 32);
            }
            Assert.True(replays > 0);
            await second.WaitForSuccessAsync(token);
        }
        var root = (await new EfProcessRuntimeUnitOfWork(inspection).LoadAsync(stopped.State.RunId, token))!;
        Assert.Equal(ProcessRuntimeStatus.Cancelled, root.Status);
        Assert.True((await inspection.CancellationRecoveries.AsNoTracking().SingleAsync(token)).Completed);
        var finalChild = await inspection.DecisionChildren.AsNoTracking().SingleAsync(token);
        var allocation = await inspection.DecisionBudgetAllocations.AsNoTracking()
            .SingleAsync(item => item.BudgetId == finalChild.BudgetId && item.AllocationId == finalChild.AllocationId, token);
        var originalWork = fixture.Waiting.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks;
        Assert.Equal(originalWork, allocation.ConsumedWorkTicks);
        Assert.NotNull(finalChild.OwnerRevision);
        Assert.Equal(finalChild.OwnerRevision, allocation.OwnerWorkRevision);
        Assert.Equal(0, (await new EfProcessDecisionBudgetStore(inspection).ReadAsync(root.RunId, null, token))!.ReservedWorkTicks);
        var finalReceipts = await inspection.IdempotencyKeys.AsNoTracking().Where(item => item.DecisionBudgetReceiptJson != null)
            .OrderBy(item => item.CommandId).ToArrayAsync(token);
        Assert.Equal(receipts.Length + (boundary == RecoveryHostMode.BeforeAccounting ? 1 : 0), finalReceipts.Length);
        foreach (var receipt in receipts) {
            Assert.Equal(JsonSerializer.Serialize(receipt), JsonSerializer.Serialize(finalReceipts.Single(item => item.CommandId == receipt.CommandId)));
        }
        if (snapshot == ReadBasisMode.RunSnapshot) {
            Assert.Equal(ProcessDecisionRunScopePhase.Cancelled, (await inspection.DecisionRunScopes.AsNoTracking().SingleAsync(token)).Phase);
        }
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    private sealed class Child(Process process, DateTimeOffset startedUtc, Task<string> errors, ITestOutputHelper output) : IAsyncDisposable {
        internal static async Task<Child> StartAsync(TestDatabaseProfile profile, Guid profileId, Guid rootId,
            RecoveryHostMode mode, ITestOutputHelper output, CancellationToken token) {
            var assembly = Path.Combine(IntegrationTestPaths.RepositoryRoot, "tests", "Support", "CanDoItAll.ProcessRecovery.TestHost",
                "bin", IntegrationTestPaths.BuildConfiguration, "net10.0", "CanDoItAll.ProcessRecovery.TestHost.dll");
            Assert.True(File.Exists(assembly), "Build the standard-configuration recovery child before running this row.");
            var intent = new { Owner = nameof(ProcessCancellationRecoveryRestartTests), Mode = mode, ProfileId = profileId, RootId = rootId,
                Assembly = assembly, Sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(assembly, token))),
                EnvironmentRoot = profile.EnvironmentRootPath, RecordedUtc = DateTimeOffset.UtcNow };
            var record = JsonSerializer.Serialize(intent);
            await File.WriteAllTextAsync(Path.Combine(profile.EnvironmentRootPath, $"recovery-{Guid.NewGuid():N}.intent.json"), record, token);
            output.WriteLine(record);
            var start = new ProcessStartInfo("dotnet") {
                WorkingDirectory = profile.EnvironmentRootPath, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            string[] prefixes = ["Database__", "ControlPlane__", "Storage__", "SecretVault__", "DataProtection__", "Processes__", "Api__", "AgentFramework__Providers__"];
            foreach (var key in start.Environment.Keys.Where(key => prefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).ToArray()) {
                start.Environment.Remove(key);
            }
            foreach (var pair in profile.CreateEnvironmentVariables(new Dictionary<string, string?> {
                ["Database:PasswordFile"] = string.Empty, ["DevelopmentManager:TuningModeEnabled"] = "false"
            })) {
                start.Environment[pair.Key] = pair.Value;
            }
            start.Environment[RecoveryHostProtocol.EnvironmentRoot] = profile.EnvironmentRootPath;
            start.ArgumentList.Add(assembly);
            start.ArgumentList.Add(mode.ToString());
            start.ArgumentList.Add(profileId.ToString("D"));
            start.ArgumentList.Add(rootId.ToString("D"));
            var process = new Process { StartInfo = start };
            var started = false;
            try {
                Assert.True(process.Start());
                started = true;
                var startedUtc = new DateTimeOffset(process.StartTime.ToUniversalTime());
                output.WriteLine(JsonSerializer.Serialize(new { Started = process.Id, StartedUtc = startedUtc, Mode = mode }));
                return new(process, startedUtc, ReadErrorsAsync(process.StandardError), output);
            } catch (Exception failure) {
                try {
                    if (started && !process.HasExited) {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    }
                } catch (Exception cleanup) {
                    throw new AggregateException(failure, cleanup);
                } finally {
                    process.Dispose();
                }
                throw;
            }
        }

        internal async Task<RecoveryHostFrame> ReadAsync(CancellationToken token) {
            var line = await process.StandardOutput.ReadLineAsync(token);
            Assert.True(line is not null, $"Recovery child exited without its expected frame: {await ErrorsIfExitedAsync()}");
            Assert.InRange(line.Length, 1, RecoveryHostProtocol.MaximumFrameCharacters);
            var frame = JsonSerializer.Deserialize<RecoveryHostFrame>(line)!;
            Assert.Equal(process.Id, frame.ProcessId);
            Assert.Equal(startedUtc, frame.ProcessStartedUtc);
            output.WriteLine(line);
            return frame;
        }

        internal async Task KillAsync() {
            Assert.False(process.HasExited);
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            output.WriteLine(JsonSerializer.Serialize(new { Killed = process.Id, StartedUtc = startedUtc, process.ExitCode }));
        }

        internal async Task WaitForSuccessAsync(CancellationToken token) {
            await process.WaitForExitAsync(token);
            var error = await errors.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(process.ExitCode == 0, error);
            Assert.Equal(string.Empty, error);
            output.WriteLine(JsonSerializer.Serialize(new { Completed = process.Id, StartedUtc = startedUtc, process.ExitCode }));
        }

        public async ValueTask DisposeAsync() {
            try {
                if (!process.HasExited) {
                    await KillAsync();
                }
                await errors.WaitAsync(TimeSpan.FromSeconds(5));
            } finally {
                process.Dispose();
            }
        }

        private async Task<string> ErrorsIfExitedAsync() => process.HasExited ? await errors.WaitAsync(TimeSpan.FromSeconds(5)) : "child still running";

        private static async Task<string> ReadErrorsAsync(StreamReader reader) {
            var retained = new System.Text.StringBuilder();
            var buffer = new char[1024];
            int read;
            while ((read = await reader.ReadAsync(buffer)) != 0) {
                retained.Append(buffer, 0, Math.Min(read, 8192 - retained.Length));
            }
            return retained.ToString();
        }
    }
}
