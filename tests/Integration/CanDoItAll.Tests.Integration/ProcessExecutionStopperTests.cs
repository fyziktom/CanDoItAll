using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessExecutionStopperTests {
    [Fact]
    public async Task Registered_runtime_stage_selects_and_stops_the_original_admitted_Agent_through_durable_checkpoints() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await ProcessExecutionAuthorityPersistenceTests.SeedAsync(services, admitBackground: true);
        await StopRootAsync(services, fixture);
        var queue = services.GetRequiredService<IProcessCancellationRecoveryQueue>();
        var claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, TimeSpan.FromMinutes(1)))!;
        Assert.Equal(fixture.RunId, claim.RootRunId.Value);
        var step = services.GetRequiredService<ProcessCancellationRuntimeRecoveryStep>();
        for (var operation = 0; operation < 4 && claim.Position.Stage == ProcessCancellationRecoveryStage.Runtime; operation++) {
            var checkpoint = await step.AdvanceAsync(claim, default);
            var saved = await queue.CheckpointAsync(claim, checkpoint);
            Assert.Equal(ProcessCancellationCheckpointStatus.Applied, saved.Status);
            claim = saved.Claim!;
        }
        Assert.Equal(ProcessCancellationRecoveryStage.Decisions, claim.Position.Stage);
        Assert.False(claim.Position.CycleHadPending);
        Assert.Equal(ProcessCancellationRecoveryPending.None, claim.Pending);
        var run = (await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(fixture.Run.Id))!;
        Assert.Equal(RunOutcome.Cancelled, run.Outcome);
        Assert.Equal(fixture.Run.ToolAdmission!.Session, run.ToolAdmission!.Session);
        Assert.Equal(fixture.Run.ToolAdmission.BackgroundInput, run.ToolAdmission.BackgroundInput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registered_owner_stops_original_admission_and_replays_after_a_new_scope(bool renewedGeneration) {
        var generations = new Generation();
        await using var app = await TestApplication.CreateAsync(Harness(generations));
        ProcessExecutionAuthorityPersistenceTests.Fixture fixture;
        ExecutionRunRecord stopped;
        await using (var scope = app.Services.CreateAsyncScope()) {
            var services = scope.ServiceProvider;
            fixture = await ProcessExecutionAuthorityPersistenceTests.SeedAsync(services, admitBackground: true);
            var state = await StopRootAsync(services, fixture);
            if (renewedGeneration) {
                generations.Value++;
            }
            Assert.Equal(ProcessCancellationRecoveryPending.None, await StopAsync(services, fixture));
            stopped = (await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(fixture.Run.Id))!;
            Assert.Equal(RunOutcome.Cancelled, stopped.Outcome);
            Assert.Equal(fixture.Run.ToolAdmission!.Session, stopped.ToolAdmission!.Session);
            Assert.Equal(fixture.Run.ToolAdmission.BackgroundInput, stopped.ToolAdmission.BackgroundInput);
            Assert.Equal(ProcessRuntimeStatus.CancelRequested, (await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(state.RunId))!.Status);
        }
        await using var reopened = app.Services.CreateAsyncScope();
        Assert.Equal(ProcessCancellationRecoveryPending.None, await StopAsync(reopened.ServiceProvider, fixture));
        var replay = (await reopened.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(fixture.Run.Id))!;
        Assert.Equal(JsonSerializer.Serialize(stopped), JsonSerializer.Serialize(replay));
        await using var database = new ProcessPersistenceDbContext(ProcessExecutionAuthorityPersistenceTests.Options(reopened.ServiceProvider));
        Assert.Single(await database.CancellationRecoveries.ToArrayAsync());
    }

    [Fact]
    public async Task Active_original_root_cannot_signal_or_mutate_its_agent() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await ProcessExecutionAuthorityPersistenceTests.SeedAsync(services, admitBackground: true);
        using var registration = services.GetRequiredService<IAgentExecutionCancellationRegistry>().Register(fixture.Run, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => StopAsync(services, fixture));
        Assert.False(registration.Token.IsCancellationRequested);
        Assert.Equal(JsonSerializer.Serialize(fixture.Run), JsonSerializer.Serialize(await services
            .GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(fixture.Run.Id)));
    }

    [Fact]
    public async Task Changed_accepted_source_after_root_stop_cannot_publish_cancellation() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await ProcessExecutionAuthorityPersistenceTests.SeedAsync(services, admitBackground: true);
        await StopRootAsync(services, fixture);
        await using var database = new ProcessPersistenceDbContext(ProcessExecutionAuthorityPersistenceTests.Options(services));
        var launch = await database.PreparedLaunches.SingleAsync(item => item.RunId == fixture.RunId);
        launch.AcceptedSourceHeaderFingerprint = "sha256:" + new string('0', 64);
        await database.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => StopAsync(services, fixture));
        Assert.Equal(JsonSerializer.Serialize(fixture.Run), JsonSerializer.Serialize(await services
            .GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(fixture.Run.Id)));
    }

    [Fact]
    public async Task Unknown_provider_dispatch_remains_pending_under_the_real_stopped_root() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await ProcessExecutionAuthorityPersistenceTests.SeedAsync(services, admitBackground: true);
        var store = services.GetRequiredService<ISandboxWorkspaceStore>();
        var journal = new AgentToolAdmissionJournal(store, fixture.Run.ToolAdmission!.Session.Profile,
            services.GetRequiredService<TimeProvider>(), services.GetServices<IAgentToolBackgroundSourcePolicy>());
        var envelope = Runtime.AgentToolAdmissionJournalFixture.Envelope();
        await using (var lease = await journal.AcquireRunAsync(fixture.Run.ToolAdmission.Session.Reference, default)) {
            var segment = Assert.Single((await journal.BeginSegmentAsync(lease, envelope, null, default)).Segments);
            await journal.BeginProviderDispatchAsync(lease, segment.Id, envelope.Digest, envelope.Digest, [], default);
        }
        var reader = services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var original = JsonSerializer.Serialize(await reader.GetExecutionRunDetailAsync(fixture.Run.Id));
        await StopRootAsync(services, fixture);
        Assert.Equal(ProcessCancellationRecoveryPending.UnresolvedEffects, await StopAsync(services, fixture));
        Assert.Equal(original, JsonSerializer.Serialize(await reader.GetExecutionRunDetailAsync(fixture.Run.Id)));
    }

    [Fact]
    public async Task Existing_unadmitted_execution_is_pending_without_fabricating_a_journal() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await ProcessExecutionAuthorityPersistenceTests.SeedAsync(services);
        await StopRootAsync(services, fixture);
        Assert.Equal(ProcessCancellationRecoveryPending.MissingJournal, await StopAsync(services, fixture));
        Assert.Null((await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(fixture.Run.Id))!.ToolAdmission);
    }

    private static Task<ProcessCancellationRecoveryPending> StopAsync(IServiceProvider services,
        ProcessExecutionAuthorityPersistenceTests.Fixture fixture)
        => services.GetRequiredService<IProcessExecutionStopper>().StopAsync(new(fixture.RunId), new(fixture.RunId), new(fixture.Run.Id));

    private static async Task<ProcessRuntimeStateSnapshot> StopRootAsync(IServiceProvider services,
        ProcessExecutionAuthorityPersistenceTests.Fixture fixture) {
        var owner = services.GetRequiredService<IProcessRuntimeUnitOfWork>();
        var state = (await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(new(fixture.RunId)))!;
        return await ProcessRootCancellationFixture.StopAsync(owner, state);
    }

    private static TestHarnessOptions Harness(Generation? generations = null)
        => ProcessExecutionAuthorityPersistenceTests.Harness(configure: services => {
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new Clock()));
            services.Replace(ServiceDescriptor.Singleton<IAgentExecutionProfileGenerationSource>(generations ?? new Generation()));
        });

    private sealed class Clock : TimeProvider {
        public override DateTimeOffset GetUtcNow() => ProcessProjectAdmissionFixture.Now.AddSeconds(5);
    }

    private sealed class Generation : IAgentExecutionProfileGenerationSource {
        internal long Value = 1;
        public DatabaseProfileGeneration GetGeneration() => new(Value);
    }
}
