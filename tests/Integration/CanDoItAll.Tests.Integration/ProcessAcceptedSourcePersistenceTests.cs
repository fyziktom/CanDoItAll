using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionDispatchAuthorityFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessAcceptedSourcePersistenceTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, 0)]
    [InlineData(ReadBasisMode.StepSnapshot, 0)]
    [InlineData(ReadBasisMode.RunSnapshot, 7)]
    [InlineData(ReadBasisMode.StepSnapshot, -7)]
    public async Task Projected_and_explicit_legacy_sources_preserve_complete_dispatch_and_history_bytes(ReadBasisMode mode, int ticks) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await CreateAsync(context, services, mode, prepared: true,
            assignmentCreatedAt: new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(ticks));
        var projected = await ObserveAsync(services, clock, fixture);
        var launch = await context.PreparedLaunches.AsNoTracking().SingleAsync(item => item.RunId == fixture.State.RunId.Value);
        Assert.Equal(ProcessAcceptedSourceVersion.OriginalProjection, launch.AcceptedSourceVersion);
        Assert.Single(await context.AcceptedAssignmentSources.Where(item => item.AdmissionId == launch.Id).ToArrayAsync());
        await context.AcceptedAssignmentSources.Where(item => item.AdmissionId == launch.Id).ExecuteDeleteAsync();
        await context.PreparedLaunches.Where(item => item.Id == launch.Id).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.AcceptedSourceVersion, ProcessAcceptedSourceVersion.LegacyPreparedLaunch)
            .SetProperty(item => item.AcceptedSourceHeaderJson, (string?)null)
            .SetProperty(item => item.AcceptedSourceHeaderFingerprint, (string?)null));
        Assert.Equal(projected, await ObserveAsync(services, clock, fixture));
        Assert.Empty(await context.AcceptedAssignmentSources.ToArrayAsync());
        Assert.Equal(launch.PayloadJson, await context.PreparedLaunches.Where(item => item.Id == launch.Id).Select(item => item.PayloadJson).SingleAsync());
    }

    [Theory]
    [InlineData(Corruption.MissingAssignment)]
    [InlineData(Corruption.HeaderDigest)]
    [InlineData(Corruption.AssignmentDigest)]
    [InlineData(Corruption.HeaderPayload)]
    [InlineData(Corruption.AssignmentPayload)]
    public async Task Invalid_projected_evidence_never_falls_back_to_valid_original_payload_or_current_assignment(Corruption change) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await CreateAsync(context, services, prepared: true);
        context.ChangeTracker.Clear();
        var launch = await context.PreparedLaunches.SingleAsync(item => item.RunId == fixture.State.RunId.Value);
        var assignment = await context.AcceptedAssignmentSources.SingleAsync(item => item.AdmissionId == launch.Id);
        var original = ProcessPreparedLaunchCodec.Read(launch);
        switch (change) {
            case Corruption.MissingAssignment:
                context.AcceptedAssignmentSources.Remove(assignment);
                break;
            case Corruption.HeaderDigest:
                launch.AcceptedSourceHeaderFingerprint = "sha256:" + new string('0', 64);
                break;
            case Corruption.AssignmentDigest:
                assignment.Fingerprint = "sha256:" + new string('0', 64);
                break;
            case Corruption.HeaderPayload:
                launch.AcceptedSourceHeaderJson = "{}";
                break;
            case Corruption.AssignmentPayload:
                assignment.PayloadJson = "{}";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }
        await context.SaveChangesAsync();
        var expected = change == Corruption.MissingAssignment ? typeof(ProcessExecutionAuthorityMismatchException) : typeof(InvalidDataException);
        Assert.IsType(expected, await Record.ExceptionAsync(() => fixture.ReadAsync(Query(services, clock))));
        Assert.IsType(expected, await Record.ExceptionAsync(() => HistoricalAsync(services, clock, fixture)));
        await using var reopened = Context(services);
        var retained = await reopened.PreparedLaunches.SingleAsync(item => item.Id == launch.Id);
        Assert.Equal(original.PreparationFingerprint, ProcessPreparedLaunchCodec.Read(retained).PreparationFingerprint);
        Assert.Equal(ProcessAcceptedSourceVersion.OriginalProjection, retained.AcceptedSourceVersion);
        Assert.Equal(change == Corruption.MissingAssignment ? 0 : 1,
            await reopened.AcceptedAssignmentSources.CountAsync(item => item.AdmissionId == launch.Id));
    }

    [Fact]
    public async Task Independent_accepted_source_survives_corruption_of_the_full_review_copy_without_repairing_it() {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await CreateAsync(context, services, prepared: true);
        var before = await ObserveAsync(services, clock, fixture);
        var invalid = (await context.PreparedLaunches.AsNoTracking().SingleAsync(item => item.RunId == fixture.State.RunId.Value)).PayloadJson + " ";
        await context.PreparedLaunches.Where(item => item.RunId == fixture.State.RunId.Value)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.PayloadJson, invalid));
        Assert.Equal(before, await ObserveAsync(services, clock, fixture));
        var launch = await context.PreparedLaunches.AsNoTracking().SingleAsync(item => item.RunId == fixture.State.RunId.Value);
        Assert.Equal(invalid, launch.PayloadJson);
        Assert.Throws<InvalidOperationException>(() => ProcessPreparedLaunchCodec.Read(launch));
    }

    [Fact]
    public async Task Duplicate_acceptance_preserves_original_source_bytes_and_does_not_repair_missing_evidence() {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var saved = await services.GetRequiredService<IProcessPreparedLaunchStore>().PrepareAsync(ProcessPreparedLaunchFixture.Create());
        var request = ProcessPreparedLaunchFixture.Commit(saved);
        await using var context = Context(services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, services);
        Assert.True((await owner.CommitAsync(request)).Succeeded);
        var launch = await context.PreparedLaunches.AsNoTracking().SingleAsync(item => item.Id == saved.Preparation.AdmissionId.Value);
        var before = JsonSerializer.Serialize(await context.AcceptedAssignmentSources.AsNoTracking().ToArrayAsync());
        var replay = await owner.CommitAsync(request);
        Assert.Equal(request.Mutation.State.RunId, replay.State.RunId);
        Assert.Equal(before, JsonSerializer.Serialize(await context.AcceptedAssignmentSources.AsNoTracking().ToArrayAsync()));
        await context.AcceptedAssignmentSources.Where(item => item.AdmissionId == launch.Id).ExecuteDeleteAsync();
        await owner.CommitAsync(request);
        Assert.Empty(await context.AcceptedAssignmentSources.ToArrayAsync());
        var retained = await context.PreparedLaunches.AsNoTracking().SingleAsync(item => item.Id == launch.Id);
        Assert.Equal(launch.AcceptedSourceHeaderJson, retained.AcceptedSourceHeaderJson);
        Assert.Equal(launch.AcceptedSourceHeaderFingerprint, retained.AcceptedSourceHeaderFingerprint);
        var state = await context.RuntimeStates.AsNoTracking().SingleAsync(item => item.RunId == launch.RunId);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => EfProcessAcceptedSource.ReadAsync(context, state,
            request.InitialAssignments!.Single().StepInstanceId, CancellationToken.None));
    }

    [Fact]
    public async Task Oversized_new_metadata_rejects_acceptance_without_legacy_fallback_or_partial_runtime_rows() {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var preparation = ProcessPreparedLaunchFixture.Create();
        preparation = preparation with { InitialCommit = preparation.InitialCommit with {
            InitialAssignments = preparation.InitialCommit.InitialAssignments!.Select(item => item with {
                OperationTargetScope = new string('x', ProcessAcceptedSourceCodec.MaximumPayloadUtf8Bytes)
            }).ToArray()
        } };
        var saved = await services.GetRequiredService<IProcessPreparedLaunchStore>().PrepareAsync(preparation);
        await using (var context = Context(services)) {
            await Assert.ThrowsAsync<InvalidDataException>(() => ProcessDecisionRunScopeFixture.Owner(context, services)
                .CommitAsync(ProcessPreparedLaunchFixture.Commit(saved)));
        }
        await using var reopened = Context(services);
        var retained = await reopened.PreparedLaunches.SingleAsync(item => item.Id == preparation.AdmissionId.Value);
        Assert.Null(retained.AcceptedAtUtc);
        Assert.Equal(ProcessAcceptedSourceVersion.LegacyPreparedLaunch, retained.AcceptedSourceVersion);
        Assert.Null(retained.AcceptedSourceHeaderJson);
        Assert.Null(retained.AcceptedSourceHeaderFingerprint);
        Assert.Empty(await reopened.AcceptedAssignmentSources.ToArrayAsync());
        Assert.Empty(await reopened.RuntimeStates.ToArrayAsync());
        Assert.Empty(await reopened.RuntimeStepAssignments.ToArrayAsync());
        Assert.Empty(await reopened.IdempotencyKeys.ToArrayAsync());
    }

    private static async Task<string[]> ObserveAsync(IServiceProvider services, TimeProvider clock, ProcessDecisionDispatchAuthorityFixture fixture) {
        var live = await fixture.ReadAsync(Query(services, clock));
        Assert.True(live.Snapshot!.ObservedCurrentDispatch);
        var history = await HistoricalAsync(services, clock, fixture);
        Assert.False(history.Snapshot!.ObservedCurrentDispatch);
        return [JsonSerializer.Serialize(live), JsonSerializer.Serialize(history)];
    }

    private static Task<ProcessExecutionDispatchAuthorityResult> HistoricalAsync(IServiceProvider services, TimeProvider clock,
        ProcessDecisionDispatchAuthorityFixture fixture)
        => Query(services, clock).ReadOriginalDecisionAsync(fixture.Evidence, fixture.Original.Binding,
            fixture.Original.Binding.ExecutionContract, fixture.Original.Material.ContentContract);

    public enum Corruption { MissingAssignment, HeaderDigest, AssignmentDigest, HeaderPayload, AssignmentPayload }
}
