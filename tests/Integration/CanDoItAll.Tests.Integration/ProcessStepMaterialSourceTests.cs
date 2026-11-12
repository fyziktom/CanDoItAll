using System.Data.Common;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessStepMaterialSourceTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(1)]
    [InlineData(128)]
    public async Task Selected_source_uses_three_bounded_reads_without_unrelated_prompts(int steps) {
        await using var fixture = await ProcessStepMaterialFixture.CreateAsync(steps: steps);
        var probe = new ReadProbe();
        await using var context = Context(fixture.Services, probe);
        var selected = await fixture.Query(context).ReadAsync(fixture.State, fixture.Claim);
        Assert.Equal(fixture.Claim.StepInstanceId, selected.Policy.StepInstanceId);
        Assert.Null(selected.Head);
        Assert.Null(selected.Material);
        Assert.False(selected.HasUnclosedWork);
        Assert.Equal(3, probe.Commands.Count);
        Assert.DoesNotContain(probe.Commands, sql => sql.Contains("\"Prompt\"", StringComparison.Ordinal));
        Assert.DoesNotContain(probe.Commands, sql => sql.Contains("launch.\"PayloadJson\"", StringComparison.Ordinal));
        var bytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(selected));
        Assert.InRange(bytes, 1, 16_384);
        output.WriteLine($"Steps={steps}; commands={probe.Commands.Count}; selected scalar bytes={bytes}.");
    }

    [Fact]
    public async Task Original_projection_does_not_decode_corrupt_full_review_copy() {
        await using var fixture = await ProcessStepMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        await context.PreparedLaunches.Where(item => item.RunId == fixture.State.RunId.Value)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.PayloadJson, "{"));
        Assert.NotNull(await fixture.Query(context).ReadAsync(fixture.State, fixture.Claim));
    }

    [Fact]
    public async Task Legacy_preparation_refuses_without_decoding_full_source_or_reserving_work() {
        await using var fixture = await ProcessStepMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        await context.AcceptedAssignmentSources.Where(item => item.AdmissionId == fixture.State.LaunchAdmissionId!.Value.Value).ExecuteDeleteAsync();
        await context.PreparedLaunches.Where(item => item.RunId == fixture.State.RunId.Value).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.AcceptedSourceVersion, ProcessAcceptedSourceVersion.LegacyPreparedLaunch)
            .SetProperty(item => item.AcceptedSourceHeaderJson, (string?)null)
            .SetProperty(item => item.AcceptedSourceHeaderFingerprint, (string?)null)
            .SetProperty(item => item.PayloadJson, "{"));
        var result = await fixture.Coordinator(context).PrepareAsync(fixture.State, fixture.Claim, TimeSpan.FromSeconds(20));
        Assert.Equal(ProcessDecisionStepPreparationStatus.SourceProjectionRequired, result.Status);
        Assert.Empty(await context.DecisionBudgetAllocations.ToArrayAsync());
    }

    [Theory]
    [InlineData(SourceChange.Executor)]
    [InlineData(SourceChange.Readiness)]
    [InlineData(SourceChange.PolicyRemoved)]
    public async Task Mutable_assignment_cannot_replace_original_preparation_authority(SourceChange change) {
        await using var fixture = await ProcessStepMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        var assignment = await context.RuntimeStepAssignments.SingleAsync();
        switch (change) {
            case SourceChange.Executor:
                assignment.ExecutorId = Guid.NewGuid().ToString("D");
                break;
            case SourceChange.Readiness:
                assignment.ReadinessHash = "sha256:replacement";
                break;
            case SourceChange.PolicyRemoved:
                assignment.DecisionDataPolicyJson = null;
                break;
        }
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => fixture.Query(context).ReadAsync(fixture.State, fixture.Claim));
        Assert.Empty(await context.DecisionBudgetAllocations.ToArrayAsync());
    }

    [Theory]
    [InlineData(ClaimChange.Owner)]
    [InlineData(ClaimChange.Token)]
    [InlineData(ClaimChange.Expired)]
    public async Task Preparation_requires_the_exact_live_selected_claim(ClaimChange change) {
        await using var fixture = await ProcessStepMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        var claim = fixture.Claim;
        if (change == ClaimChange.Expired) {
            await context.DispatchClaims.Where(item => item.RunId == fixture.State.RunId.Value)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.ExpiresAtUtc, DateTimeOffset.UtcNow.AddSeconds(-1)));
        } else {
            claim = change == ClaimChange.Owner ? claim with { OwnerId = new("different-owner") }
                : claim with { ClaimToken = new(Guid.NewGuid()) };
        }
        var failure = await Assert.ThrowsAsync<ProcessRuntimeDispatchPreconditionException>(
            () => fixture.Query(context).ReadAsync(fixture.State, claim));
        Assert.Equal(change == ClaimChange.Expired ? ProcessRuntimeDispatchPreconditionRefusal.LeaseExpired
            : ProcessRuntimeDispatchPreconditionRefusal.ClaimChanged, failure.Refusal);
    }

    public enum SourceChange { Executor, Readiness, PolicyRemoved }
    public enum ClaimChange { Owner, Token, Expired }

    private sealed class ReadProbe : DbCommandInterceptor {
        internal List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
