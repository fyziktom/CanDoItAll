using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessAcceptedExecutionInputTests {
    [Fact]
    public async Task Registered_reader_preserves_original_input_after_current_prompt_repair_and_full_launch_corruption() {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        var selected = fixture.State.Steps.Single().StepInstanceId;
        var reader = fixture.Services.GetRequiredService<IProcessAcceptedExecutionInputReader>();
        var before = await reader.ReadAsync(fixture.State.RunId, selected);
        Assert.Equal(fixture.Source, before.Source);
        Assert.Equal(fixture.State.RootRunId, before.RootRunId);
        Assert.Equal(fixture.State.Steps.Single().StepDefinitionId, before.RequireRequirements().StepDefinitionId);
        await using var context = Context(fixture.Services);
        await context.RuntimeStepAssignments.Where(item => item.RunId == fixture.State.RunId.Value)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Prompt, "Repaired current prompt"));
        await context.PreparedLaunches.Where(item => item.Id == before.AdmissionId.Value)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.PayloadJson, "Invalid review copy"));
        await context.RuntimeStates.Where(item => item.RunId == fixture.State.RunId.Value)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, ProcessRuntimeStatus.Completed));
        await using var reopened = Context(fixture.Services);
        var after = await Reader(reopened, fixture.Services).ReadAsync(fixture.State.RunId, selected);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        Assert.NotEqual("Repaired current prompt", after.Assignment.Prompt);
        Assert.Equal("Invalid review copy", await reopened.PreparedLaunches.Select(item => item.PayloadJson).SingleAsync());
    }

    [Fact]
    public async Task Historical_assignment_is_not_silently_recaptured_from_the_mutable_plan() {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        var row = await context.AcceptedAssignmentSources.SingleAsync();
        var launch = await context.PreparedLaunches.AsNoTracking().SingleAsync();
        var selected = fixture.State.Steps.Single().StepInstanceId;
        var original = await Reader(context, fixture.Services).ReadAsync(fixture.State.RunId, selected);
        var domain = "process-accepted-execution-input/v1\n" + launch.AcceptedSourceHeaderFingerprint + "\n" + row.Fingerprint + "\n";
        var legacy = ProcessAcceptedSourceCodec.Encode(original.Assignment, domain, ProcessAcceptedExecutionInputCodec.MaximumPayloadUtf8Bytes);
        row.InputVersion = ProcessAcceptedExecutionInputVersion.OriginalAssignment;
        row.InputJson = legacy.PayloadJson;
        row.InputFingerprint = legacy.Fingerprint;
        await context.SaveChangesAsync();
        await context.PreparedLaunches.ExecuteUpdateAsync(update => update.SetProperty(item => item.PayloadJson, "Corrupted current review copy"));
        await using var reopened = Context(fixture.Services);
        var result = await Reader(reopened, fixture.Services).ReadAsync(fixture.State.RunId, selected);
        Assert.Equal(JsonSerializer.Serialize(original.Assignment), JsonSerializer.Serialize(result.Assignment));
        Assert.Throws<ProcessAcceptedStepRequirementsRequiredException>(() => result.RequireRequirements());
        Assert.Equal(ProcessAcceptedExecutionInputVersion.OriginalAssignment, (await reopened.AcceptedAssignmentSources.SingleAsync()).InputVersion);
    }

    [Fact]
    public async Task Replayed_acceptance_preserves_input_and_never_recaptures_missing_evidence() {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        var launch = await context.PreparedLaunches.AsNoTracking().SingleAsync();
        var saved = ProcessPreparedLaunchCodec.Read(launch);
        var request = ProcessPreparedLaunchFixture.Commit(saved);
        var owner = ProcessRootMaterialFixture.Owner(context, fixture.Services);
        var before = JsonSerializer.Serialize(await context.AcceptedAssignmentSources.AsNoTracking().ToArrayAsync());
        Assert.False((await owner.CommitAsync(request)).CommittedNow);
        Assert.Equal(before, JsonSerializer.Serialize(await context.AcceptedAssignmentSources.AsNoTracking().ToArrayAsync()));
        await context.AcceptedAssignmentSources.ExecuteUpdateAsync(update => update
            .SetProperty(item => item.InputVersion, ProcessAcceptedExecutionInputVersion.NotCaptured)
            .SetProperty(item => item.InputJson, (string?)null).SetProperty(item => item.InputFingerprint, (string?)null));
        Assert.False((await owner.CommitAsync(request)).CommittedNow);
        await Assert.ThrowsAsync<ProcessAcceptedExecutionInputRequiredException>(() => Reader(context, fixture.Services)
            .ReadAsync(fixture.State.RunId, fixture.State.Steps.Single().StepInstanceId));
        Assert.Null((await context.AcceptedAssignmentSources.AsNoTracking().SingleAsync()).InputJson);
    }

    [Theory]
    [InlineData(Change.MissingRow)]
    [InlineData(Change.Uncaptured)]
    [InlineData(Change.Legacy)]
    [InlineData(Change.HeaderFingerprint)]
    [InlineData(Change.MetadataFingerprint)]
    [InlineData(Change.InputFingerprint)]
    [InlineData(Change.InputPayload)]
    [InlineData(Change.OtherStep)]
    [InlineData(Change.RuntimeLifetime)]
    [InlineData(Change.DatabaseProfile)]
    public async Task Missing_or_changed_input_never_uses_the_mutable_assignment_or_prepared_launch(Change change) {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync(change == Change.OtherStep ? 2 : 1);
        await using var context = Context(fixture.Services);
        var selected = fixture.State.Steps[0].StepInstanceId;
        var row = await context.AcceptedAssignmentSources.SingleAsync(item => item.StepInstanceId == selected.Value);
        var before = await Reader(context, fixture.Services).ReadAsync(fixture.State.RunId, selected);
        switch (change) {
            case Change.MissingRow:
                context.AcceptedAssignmentSources.Remove(row);
                break;
            case Change.Uncaptured:
                row.InputVersion = ProcessAcceptedExecutionInputVersion.NotCaptured;
                row.InputJson = null;
                row.InputFingerprint = null;
                break;
            case Change.Legacy:
                context.AcceptedAssignmentSources.Remove(row);
                var legacy = await context.PreparedLaunches.SingleAsync();
                legacy.AcceptedSourceVersion = ProcessAcceptedSourceVersion.LegacyPreparedLaunch;
                legacy.AcceptedSourceHeaderJson = null;
                legacy.AcceptedSourceHeaderFingerprint = null;
                break;
            case Change.HeaderFingerprint:
                (await context.PreparedLaunches.SingleAsync()).AcceptedSourceHeaderFingerprint = Digest;
                break;
            case Change.MetadataFingerprint:
                row.Fingerprint = Digest;
                break;
            case Change.InputFingerprint:
                row.InputFingerprint = Digest;
                break;
            case Change.InputPayload:
                row.InputJson = "{}";
                break;
            case Change.OtherStep:
                var other = await context.AcceptedAssignmentSources.SingleAsync(item => item.StepInstanceId != selected.Value);
                row.InputJson = other.InputJson;
                row.InputFingerprint = other.InputFingerprint;
                break;
            case Change.RuntimeLifetime:
                (await context.RuntimeStates.SingleAsync()).ProjectAdmissionLifetimeId = Guid.NewGuid();
                break;
        }
        await context.SaveChangesAsync();
        await using var reopened = Context(fixture.Services);
        var database = fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var reader = change == Change.DatabaseProfile
            ? new EfProcessAcceptedExecutionInputReader(reopened, new OtherDatabase(database.Profile with { Profile = new() { Id = Guid.NewGuid() } }))
            : Reader(reopened, fixture.Services);
        var failure = await Record.ExceptionAsync(() => reader.ReadAsync(fixture.State.RunId, selected));
        var expected = change switch {
            Change.Uncaptured or Change.Legacy => typeof(ProcessAcceptedExecutionInputRequiredException),
            Change.RuntimeLifetime or Change.DatabaseProfile => typeof(ProcessExecutionAuthorityMismatchException),
            _ => typeof(InvalidDataException)
        };
        Assert.IsType(expected, failure);
        Assert.Equal(before.Assignment.Prompt, await reopened.RuntimeStepAssignments
            .Where(item => item.StepInstanceId == selected.Value).Select(item => item.Prompt).SingleAsync());
        Assert.Equal(before.PreparationFingerprint, ProcessPreparedLaunchCodec.Read(
            await reopened.PreparedLaunches.AsNoTracking().SingleAsync()).PreparationFingerprint);
    }

    [Fact]
    public async Task Oversized_decision_input_rolls_back_first_acceptance_and_all_runtime_rows() {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var preparation = ProcessPreparedLaunchScalingFixture.Create(1, ProcessAcceptedExecutionInputCodec.MaximumPayloadUtf8Bytes);
        var saved = await services.GetRequiredService<IProcessPreparedLaunchStore>().PrepareAsync(preparation);
        await using (var context = Context(services)) {
            await Assert.ThrowsAsync<InvalidDataException>(() => ProcessDecisionRunScopeFixture.Owner(context, services)
                .CommitAsync(ProcessPreparedLaunchFixture.Commit(saved)));
        }
        await using var reopened = Context(services);
        Assert.Null((await reopened.PreparedLaunches.SingleAsync()).AcceptedAtUtc);
        Assert.Empty(await reopened.AcceptedAssignmentSources.ToArrayAsync());
        Assert.Empty(await reopened.RuntimeStates.ToArrayAsync());
        Assert.Empty(await reopened.RuntimeStepAssignments.ToArrayAsync());
        Assert.Empty(await reopened.IdempotencyKeys.ToArrayAsync());
    }

    private static EfProcessAcceptedExecutionInputReader Reader(ProcessPersistenceDbContext context, IServiceProvider services)
        => new(context, services.GetRequiredService<ICanonicalRuntimeDatabase>());

    private static string Digest => "sha256:" + new string('0', 64);

    private sealed class OtherDatabase(ResolvedDatabaseProfile profile) : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile { get; } = profile;
        public long Generation => 0;
    }

    public enum Change { MissingRow, Uncaptured, Legacy, HeaderFingerprint, MetadataFingerprint, InputFingerprint, InputPayload, OtherStep, RuntimeLifetime, DatabaseProfile }
}
