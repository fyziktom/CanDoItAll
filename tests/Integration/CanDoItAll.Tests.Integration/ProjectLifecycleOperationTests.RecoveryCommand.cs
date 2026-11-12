using System.Security.Cryptography;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectLifecycleOperationTests {
    [Fact]
    public Task Offline_previous_selection_restoration_preserves_native_work_and_enables_exact_release() =>
        WithFixtureAsync(async fixture => {
            await OfflineMaintenanceCommandFixture.PersistProfileAsync(fixture.Services);
            var completion = await PrepareHeldCompletionAsync(fixture);
            var address = completion.Original.Completion;
            var files = fixture.Services.GetRequiredService<DatasetActivationService>();
            var active = (await files.ObserveActiveBindingAsync(address.DatabaseProfileId)).Record;
            var reactivation = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Reactivate,
                new(new(Guid.NewGuid()), Guid.NewGuid(), DatasetLifecycleRecord.CurrentSchemaVersion, DatasetGuardProtocol.SharedRows), active.Plan.Binding);
            using (var selection = files.Acquire(address.DatabaseProfileId)) {
                await selection.WritePreparedAsync(reactivation);
            }
            var pending = await files.ObserveExistingRecordAsync(address.DatabaseProfileId);
            string activationPath = Path.Combine("database-profiles", "dataset-activations", $"{address.DatabaseProfileId:D}.json") + ":";
            var before = (await RecoveryImmutableStateAsync(fixture)).Where(value => !value.StartsWith(activationPath, StringComparison.Ordinal)).ToArray();
            var state = await fixture.StateAsync();
            var refused = await OfflineMaintenanceCommandFixture.RunAsync(fixture.Application, address, "inspect", expectedExitCode: 2);
            Assert.Equal("Refused", refused.GetProperty("status").GetString());
            var wrong = await RestoreAsync(Guid.NewGuid(), expectedExitCode: 2);
            Assert.Equal("Refused", wrong.GetProperty("status").GetString());
            Assert.Equal(pending, await files.ObserveExistingRecordAsync(address.DatabaseProfileId));
            var restored = await RestoreAsync(reactivation.PlanId);
            Assert.Equal("Restored", restored.GetProperty("status").GetString());
            var selectionAfter = await files.ObserveActiveBindingAsync(address.DatabaseProfileId);
            Assert.Equal(reactivation, selectionAfter.Record.Restoration!.AbandonedPlan);
            Assert.Equal(active.Plan.Binding, selectionAfter.Record.Plan.Binding);
            var repeated = await RestoreAsync(reactivation.PlanId);
            Assert.Equal(restored.GetProperty("result").GetProperty("replacementPlanId").GetString(),
                repeated.GetProperty("result").GetProperty("replacementPlanId").GetString());
            Assert.Equal(selectionAfter, await files.ObserveActiveBindingAsync(address.DatabaseProfileId));
            state.RequireEqual(await fixture.StateAsync());
            Assert.Equal(before, (await RecoveryImmutableStateAsync(fixture)).Where(value => !value.StartsWith(activationPath, StringComparison.Ordinal)).ToArray());
            Assert.IsType<ProjectDeletionCompletionObservation.AwaitingOwnerRelease>(await fixture.Services
                .GetRequiredService<ProjectDeletionCompletionReader>().ObserveAsync(completion));
            var inspected = await OfflineMaintenanceCommandFixture.RunAsync(fixture.Application, address, "inspect");
            string digest = inspected.GetProperty("result").GetProperty("plan").GetProperty("digest").GetProperty("hex").GetString()!;
            var released = await OfflineMaintenanceCommandFixture.RunAsync(fixture.Application, address, "reconcile", digest);
            Assert.Equal("Released", released.GetProperty("status").GetString());
            Assert.IsType<ProjectDeletionCompletionObservation.Released>(await fixture.Services
                .GetRequiredService<ProjectDeletionCompletionReader>().ObserveAsync(completion));
            state.RequireEqual(await fixture.StateAsync());
            Assert.Equal(before, (await RecoveryImmutableStateAsync(fixture)).Where(value => !value.StartsWith(activationPath, StringComparison.Ordinal)).ToArray());

            Task<System.Text.Json.JsonElement> RestoreAsync(Guid plan, int expectedExitCode = 0)
                => OfflineMaintenanceCommandFixture.RunAsync(fixture.Application, address.DatabaseProfileId,
                    ["dataset-activation", "restore-previous", "--profile-id", address.DatabaseProfileId.ToString("D"),
                     "--expected-namespace", active.Plan.Binding.Namespace.Value.ToString("D"), "--expected-plan-id", plan.ToString("D"), "--writers-stopped"],
                    expectedExitCode);
        }, new() { Failure = FailurePoint.OwnerDisposal }, nativeParticipants: true);

    [Fact]
    public Task Offline_command_releases_native_completion_without_starting_host_or_running_callbacks() =>
        WithFixtureAsync(async fixture => {
            await OfflineMaintenanceCommandFixture.PersistProfileAsync(fixture.Services);
            var completion = await PrepareHeldCompletionAsync(fixture);
            Assert.Equal(2, completion.Obligations.Count);
            var state = await fixture.StateAsync();
            var before = await RecoveryImmutableStateAsync(fixture);
            var address = completion.Original.Completion;
            var inspected = await OfflineMaintenanceCommandFixture.RunAsync(fixture.Application, address, "inspect");
            Assert.Equal("Observed", inspected.GetProperty("status").GetString());
            var plan = inspected.GetProperty("result").GetProperty("plan");
            Assert.Equal(2, plan.GetProperty("participants").GetArrayLength());
            string digest = plan.GetProperty("digest").GetProperty("hex").GetString()!;
            var released = await OfflineMaintenanceCommandFixture.RunAsync(fixture.Application, address, "reconcile", digest);
            Assert.Equal("Released", released.GetProperty("status").GetString());
            Assert.False(released.GetProperty("result").GetProperty("cleanupUnconfirmed").GetBoolean());
            var repeated = await OfflineMaintenanceCommandFixture.RunAsync(fixture.Application, address, "reconcile", digest);
            Assert.Equal("Released", repeated.GetProperty("status").GetString());
            Assert.Equal(released.GetProperty("result").GetProperty("releasedAtUtc").GetString(),
                repeated.GetProperty("result").GetProperty("releasedAtUtc").GetString());
            Assert.IsType<ProjectDeletionCompletionObservation.Released>(await fixture.Services
                .GetRequiredService<ProjectDeletionCompletionReader>().ObserveAsync(completion));
            state.RequireEqual(await fixture.StateAsync());
            Assert.Equal(before, await RecoveryImmutableStateAsync(fixture));
        }, new() { Failure = FailurePoint.OwnerDisposal }, nativeParticipants: true);

    private static async Task<string[]> RecoveryImmutableStateAsync(LifecycleFixture fixture) {
        string controlPlane = fixture.Application.TestEnvironment.ControlPlaneRootPath;
        var files = Directory.EnumerateFiles(controlPlane, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(path => Path.GetRelativePath(controlPlane, path) + ":" +
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))).ToList();
        var rows = await fixture.ReadAsync(async context => new[] {
            await context.Database.SqlQueryRaw<string>("""
                SELECT md5(COALESCE(jsonb_agg(to_jsonb(r) ORDER BY "Id")::text, '[]')) AS "Value"
                FROM "AgentFramework_ProjectAccessRevocations" r
                """).SingleAsync(),
            await context.Database.SqlQueryRaw<string>("""
                SELECT md5(COALESCE(jsonb_agg(to_jsonb(r) ORDER BY "Id")::text, '[]')) AS "Value"
                FROM "Workbench_ProjectCrossModuleMutations" r
                """).SingleAsync(),
            await context.Database.SqlQueryRaw<string>("""
                SELECT md5(COALESCE(jsonb_agg(to_jsonb(r) ORDER BY table_name, ordinal_position)::text, '[]')) AS "Value"
                FROM information_schema.columns r WHERE table_schema = 'public'
                """).SingleAsync()
        });
        files.AddRange(rows);
        return files.ToArray();
    }


}
