using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Integration.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProjectTaskPlanProcessOutcomeReaderTests {
    [Theory]
    [InlineData(AfterPublication.None)]
    [InlineData(AfterPublication.LauncherRevoked)]
    [InlineData(AfterPublication.ExecutorRevoked)]
    [InlineData(AfterPublication.ProfileGeneration)]
    [InlineData(AfterPublication.AssignmentRepair)]
    public async Task Registered_accounting_retains_actual_work_and_receipt_after_access_or_assignment_changes(AfterPublication change) {
        var probe = new SourceProbe();
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync(processReadProbe: probe);
        var waiting = await fixture.PrepareAsync();
        var published = ProjectTaskPlanAgentFixture.Result(await fixture.PublishAsync(waiting));
        var source = fixture.Source;
        var original = await source.ReadAsync();
        if (change is AfterPublication.LauncherRevoked or AfterPublication.ExecutorRevoked) {
            await source.RevokeAsync(change == AfterPublication.LauncherRevoked ? source.Launcher!.Id : source.Executor.Id,
                agent => agent with { Permissions = agent.Permissions with { CanUseTools = false } });
        } else if (change == AfterPublication.ProfileGeneration) {
            source.Generations.Advance();
        } else if (change == AfterPublication.AssignmentRepair) {
            await using var context = Context(source.Sql.Services);
            var assignment = await context.RuntimeStepAssignments.SingleAsync(item => item.RunId == source.Process.State.RunId.Value);
            assignment.ExecutorId = Guid.NewGuid().ToString("D");
            assignment.ReadinessHash = "sha256:repaired-outcome-assignment";
            await context.SaveChangesAsync();
        }
        await using (var context = Context(source.Sql.Services)) {
            Assert.Equal(ProcessAcceptedSourceVersion.OriginalProjection, await context.PreparedLaunches
                .Where(item => item.RunId == source.Process.State.RunId.Value)
                .Select(item => item.AcceptedSourceVersion).SingleAsync());
        }
        source.ExpireClaim();
        var reader = Reader(fixture);
        probe.Commands.Clear();
        source.Sql.Probe.Reset();
        var root = source.Sql.Services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        using var files = await HistoricalFileObservation.StartAsync(root);
        ProcessDecisionChildResult.Closed result;
        await using (var publication = new FileStream(new FileSandboxWorkspaceStorageLayout(root, source.Scope).WorkspaceLockPath,
            FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            result = Assert.IsType<ProcessDecisionChildResult.Closed>(await reader.ReadAsync(source.Process.Parent, deadline.Token));
        }
        await files.AssertNoChangesAsync();
        Assert.Equal(ProcessDecisionChildTerminalStatus.Succeeded, result.Status);
        Assert.Equal(new ProcessDecisionEffectReceiptId(published.OperationId), result.Closure.AcceptedReceipt);
        var work = original.ToolAdmission!.PlanningDecision!.Work;
        Assert.Equal(work.Revision, result.Work.OwnerWorkRevision);
        Assert.Equal(work.Revision, result.Closure.OwnerRevision);
        Assert.Equal(work.ClosedWorkTicks, result.Work.Consumed.WorkTicks);
        Assert.Equal(0, result.Work.Consumed.Replans);
        Assert.Equal(4, probe.Commands.Count);
        Assert.All(probe.Commands, sql => {
            Assert.StartsWith("SELECT", sql.TrimStart(), StringComparison.Ordinal);
            Assert.Contains("LIMIT 2", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("LaunchSnapshotJson", sql, StringComparison.Ordinal);
            if (sql.Contains("process_decision_materials", StringComparison.Ordinal)) {
                Assert.DoesNotContain("PayloadJson", sql, StringComparison.Ordinal);
            }
        });
        var sourceQuery = Assert.Single(probe.Commands,
            sql => sql.Contains("process_accepted_assignment_sources", StringComparison.Ordinal));
        var legacyRead = Assert.Single(Regex.Matches(sourceQuery,
            $$"""CASE\s+WHEN\s+(?<launch>\w+)\."AcceptedSourceVersion" = {{(int)ProcessAcceptedSourceVersion.LegacyPreparedLaunch}} THEN \k<launch>\."PayloadJson"\s+ELSE ''\s+END""").Cast<Match>());
        Assert.DoesNotContain(legacyRead.Groups["launch"].Value + ".\"PayloadJson\"",
            sourceQuery.Remove(legacyRead.Index, legacyRead.Length), StringComparison.Ordinal);
        Assert.Empty(source.Sql.Probe.Commands);
        Assert.Single(fixture.Wire.Requests);
        var retained = await source.ReadAsync();
        Assert.Equal(original.Revision, retained.Revision);
        Assert.Equal(original.ToolAdmission.Revision, retained.ToolAdmission!.Revision);
        Assert.Equal(work, retained.ToolAdmission.PlanningDecision!.Work);
        await source.Sql.VerifyAsync(1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Initial_and_approval_waits_do_not_settle_the_original_allocation(bool prepared) {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        if (prepared) {
            await fixture.PrepareAsync();
        }
        var result = Assert.IsType<ProcessDecisionChildResult.Pending>(await Reader(fixture).ReadAsync(fixture.Source.Process.Parent));
        Assert.Equal(prepared ? ProcessDecisionChildPendingReason.ApprovalRequired : ProcessDecisionChildPendingReason.ExecutionIncomplete, result.Reason);
        Assert.Equal(prepared ? 1 : 0, fixture.Wire.Requests.Count);
        await fixture.Source.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task A_definite_saved_tool_refusal_retains_closed_work_and_explicit_no_effect() {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync(includeOperationContract: false);
        await fixture.PublishAsync(await fixture.PrepareAsync());
        var result = Assert.IsType<ProcessDecisionChildResult.Closed>(await Reader(fixture).ReadAsync(fixture.Source.Process.Parent));
        Assert.Equal(ProcessDecisionChildTerminalStatus.Failed, result.Status);
        Assert.Equal(ProcessDecisionChildOutcome.NoEffect, result.Closure.Outcome);
        Assert.Null(result.Closure.AcceptedReceipt);
        Assert.Equal((await fixture.Source.ReadAsync()).ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks, result.Work.Consumed.WorkTicks);
        Assert.Single(fixture.Wire.Requests);
        await fixture.Source.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Changed_basis_remains_a_manager_wait_without_another_capture() {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        var waiting = await fixture.PrepareAsync();
        var source = fixture.Source;
        Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await source.Sql.Publisher().PublishAsync(await source.Sql.CommandAsync(1), source.Sql.Invocation));
        await fixture.PublishAsync(waiting);
        Assert.Equal(ProcessDecisionChildPendingReason.BasisChanged,
            Assert.IsType<ProcessDecisionChildResult.Pending>(await Reader(fixture).ReadAsync(source.Process.Parent)).Reason);
        Assert.Equal(waiting.ToolAdmission!.PlanningDecision!.InheritedOrigin, (await source.ReadAsync()).ToolAdmission!.PlanningDecision!.InheritedOrigin);
        Assert.Single(fixture.Wire.Requests);
        await source.Sql.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task Recovered_effect_receipt_does_not_close_work_interrupted_by_lost_acknowledgement() {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        var source = fixture.Source;
        var waiting = await fixture.PrepareAsync();
        source.Sql.Faults.Commit.AfterCommit = () => {
            source.Sql.Probe.BeforeCommand = (command, _) => command.CommandText.Contains("LEFT JOIN \"Workbench_TaskPlanReceipts\"", StringComparison.Ordinal)
                ? Task.FromException(new IOException("outcome-receipt-temporarily-unavailable")) : Task.CompletedTask;
            throw new IOException("outcome-commit-acknowledgement-lost");
        };
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.PublishAsync(waiting));
        source.Sql.Probe.BeforeCommand = null;
        var originalWork = (await source.ReadAsync()).ToolAdmission!.PlanningDecision!.Work;
        Assert.NotNull(originalWork.Open);
        source.ExpireClaim();
        await using (var lease = await source.Journal.AcquireInterruptedReconciliationAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            await source.Journal.ReconcileInterruptedAsync(lease,
                source.Sql.Services.GetServices<IAgentToolReceiptReconciliationProvider>().ToArray(), fixture.CanReadAsync, default);
        }
        var recovered = await source.ReadAsync();
        var proposal = AgentToolJournalTransitions.RequireProposal(recovered.ToolAdmission!, ProjectTaskPlanAgentFixture.Binding(waiting));
        Assert.Equal(AgentToolProposalState.OwnerReceiptConfirmed, proposal.State);
        Assert.NotNull(proposal.OwnerReceipt);
        Assert.Equal(originalWork, recovered.ToolAdmission!.PlanningDecision!.Work);
        Assert.Equal(ProcessDecisionChildPendingReason.ActiveWork,
            Assert.IsType<ProcessDecisionChildResult.Pending>(await Reader(fixture).ReadAsync(source.Process.Parent)).Reason);
        Assert.Single(fixture.Wire.Requests);
        await source.Sql.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task A_profile_switch_during_original_source_read_refuses_the_accounting_observation() {
        var probe = new SourceProbe();
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync(processReadProbe: probe);
        await fixture.PublishAsync(await fixture.PrepareAsync());
        probe.OnRead = fixture.Source.Generations.Advance;
        await Assert.ThrowsAsync<InvalidDataException>(() => Reader(fixture).ReadAsync(fixture.Source.Process.Parent));
        Assert.Single(fixture.Wire.Requests);
        await fixture.Source.Sql.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task Missing_child_journal_remains_unknown_and_does_not_borrow_another_run() {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        var parent = fixture.Source.Process.Parent;
        var missing = parent with { Child = parent.Child with { Address = parent.Child.Address with { ChildId = new(Guid.NewGuid()) } } };
        Assert.Equal(ProcessDecisionChildPendingReason.MissingJournal,
            Assert.IsType<ProcessDecisionChildResult.Pending>(await Reader(fixture).ReadAsync(missing)).Reason);
        Assert.Empty(fixture.Wire.Requests);
        await fixture.Source.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Cancellation_before_read_does_not_query_original_source() {
        var probe = new SourceProbe();
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync(processReadProbe: probe);
        probe.Commands.Clear();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Reader(fixture).ReadAsync(fixture.Source.Process.Parent, cancellation.Token));
        Assert.Empty(probe.Commands);
        Assert.Empty(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Pending_storage_recovery_refuses_accounting_without_recovering_or_querying_the_source() {
        var probe = new SourceProbe();
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync(processReadProbe: probe);
        await fixture.PublishAsync(await fixture.PrepareAsync());
        var source = fixture.Source;
        var root = source.Sql.Services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        var layout = new FileSandboxWorkspaceStorageLayout(root, source.Scope);
        var pending = Path.Combine(layout.ExecutionStorageRoot, "pending-run-detail-update.json");
        Assert.False(File.Exists(pending));
        await File.WriteAllTextAsync(pending, "retained unfinished publication");
        try {
            probe.Commands.Clear();
            using var files = await HistoricalFileObservation.StartAsync(root);
            var refusal = await Assert.ThrowsAsync<IOException>(() => Reader(fixture).ReadAsync(source.Process.Parent));
            Assert.Contains(nameof(AgentHistoricalStorageReadStatus.RecoveryRequired), refusal.Message, StringComparison.Ordinal);
            await files.AssertNoChangesAsync();
            Assert.Empty(probe.Commands);
            Assert.Single(fixture.Wire.Requests);
        } finally {
            File.Delete(pending);
        }
        await source.Sql.VerifyAsync(1, 1);
    }

    private static IProcessDecisionChildOutcomeReader Reader(ProjectTaskPlanProcessRuntimeFixture fixture)
        => Assert.Single(fixture.Source.Sql.Services.GetServices<IProcessDecisionChildOutcomeReader>(),
            reader => reader.Contract == ProjectTaskPlanProcessContract.Execution);

    public enum AfterPublication { None, LauncherRevoked, ExecutorRevoked, ProfileGeneration, AssignmentRepair }

    private sealed class SourceProbe : DbCommandInterceptor {
        internal ConcurrentQueue<string> Commands { get; } = new();
        internal Action? OnRead { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Enqueue(command.CommandText);
            OnRead?.Invoke();
            return ValueTask.FromResult(result);
        }
    }
}
