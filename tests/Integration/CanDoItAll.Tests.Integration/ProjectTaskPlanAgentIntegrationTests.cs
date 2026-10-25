using System.Data;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.ProjectTaskPlanAgentFixture;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanAgentIntegrationTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Configured_provider_to_registered_saved_tool_commits_exact_plan_once(ReadBasisMode mode) {
        await using var fixture = await CreateAsync(mode);
        var waiting = await fixture.PrepareAsync();
        var original = waiting.ToolAdmission!.PlanningDecision!;
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        var response = await fixture.PublishAsync(waiting);
        var result = Result(response);
        Assert.Equal(ProjectTaskPlanAgentResultKind.Published, result.Kind);
        Assert.False(result.Replayed);
        await fixture.Sql.VerifyAsync(1, 1);
        await using var context = await fixture.Sql.ContextAsync();
        var task = await context.Set<ProjectObjectRecord>().SingleAsync(item => item.ObjectType == ProjectObjectType.WorkItem);
        Assert.Equal("Review concurrent edits", task.Title);
        Assert.Equal(task.Id, Assert.Single(result.TaskIds));
        var completed = await fixture.ReadAsync();
        Assert.Equal(AgentPlanningGenerationState.Completed, completed.ToolAdmission!.PlanningDecision!.Current.State);
        Assert.Equal(original.Work.ClosedWorkTicks, completed.ToolAdmission.PlanningDecision.Work.ClosedWorkTicks);
        Assert.Single(fixture.Wire.Requests);
        Assert.DoesNotContain("\"tools\"", fixture.Wire.Requests[0], StringComparison.Ordinal);
        Assert.Contains("Task-plan owner proof", fixture.Wire.Requests[0], StringComparison.Ordinal);
        Assert.Equal(1, fixture.Wire.Completed);
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.ResumeAsync(waiting));
        Assert.Single(fixture.Wire.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Changed_basis_requires_new_generation_operation_and_approval(bool automatic) {
        await using var fixture = await CreateAsync(ReadBasisMode.RunSnapshot, replans: automatic ? 1 : 0);
        var waiting = await fixture.PrepareAsync();
        var first = Binding(waiting);
        Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Sql.Publisher()
            .PublishAsync(await fixture.Sql.CommandAsync(1), fixture.Sql.Invocation));
        var refused = Result(await fixture.PublishAsync(waiting));
        Assert.Equal(ProjectTaskPlanRefusal.BasisChanged, refused.Refusal);
        Assert.Empty(refused.TaskIds);
        await fixture.Sql.VerifyAsync(1, 1);
        var paused = await fixture.ReadAsync();
        Assert.Equal(AgentPlanningGenerationState.PausedBasisChanged, paused.ToolAdmission!.PlanningDecision!.Current.State);
        await fixture.Owner.RefreshAsync(AgentPlanningDecisionExpectation.From(paused), automatic, fixture.Participant.CanReadAsync, default);
        var refreshed = await fixture.PrepareAsync();
        Assert.Equal(2, refreshed.ToolAdmission!.PlanningDecision!.CurrentGeneration);
        Assert.NotEqual(first.IntentId, Binding(refreshed).IntentId);
        Assert.NotEqual(first.Digest, Binding(refreshed).Digest);
        Assert.Single(refreshed.PendingApprovals);
        Assert.Equal(2, fixture.Wire.Requests.Count);
        var result = Result(await fixture.PublishAsync(refreshed));
        Assert.Equal(ProjectTaskPlanAgentResultKind.Published, result.Kind);
        Assert.NotEqual(refused.OperationId, result.OperationId);
        await fixture.Sql.VerifyAsync(2, 2);
    }

    [Fact]
    public async Task Current_write_revocation_refuses_without_changing_readable_original_plan() {
        await using var fixture = await CreateAsync();
        var waiting = await fixture.PrepareAsync();
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent.Id != fixture.Agent.Id ? agent : agent with {
                ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson,
                    new() { CanRead = true, CanWriteTasks = false, AllowAllProjects = true })
            }).ToArray()
        });
        Assert.Equal(ProjectTaskPlanRefusal.AuthorityDenied, Result(await fixture.PublishAsync(waiting)).Refusal);
        await fixture.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Expiry_after_task_flush_rolls_back_with_definite_no_effect_evidence() {
        await using var fixture = await CreateAsync();
        var waiting = await fixture.PrepareAsync();
        fixture.Sql.Faults.Save.AfterFlush = _ => {
            fixture.Clock.Advance(TimeSpan.FromMinutes(5));
            return Task.CompletedTask;
        };
        var result = await fixture.PublishAsync(waiting);
        Assert.Equal(AgentToolEffectState.NotCommitted, result.EffectState);
        Assert.Equal(ProjectTaskPlanRefusal.BudgetElapsed, Result(result).Refusal);
        await fixture.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Actual_live_work_expires_while_waiting_for_the_project_lock() {
        await using var fixture = await CreateAsync();
        var waiting = await fixture.PrepareAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var blocker = await fixture.Sql.ContextAsync(deadline.Token);
        await using var held = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, deadline.Token);
        var pid = await blocker.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync(deadline.Token);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(blocker,
            [ProjectMutationScopeKeys.ForProject(fixture.Sql.Project.ProjectId)], deadline.Token);
        fixture.Sql.Probe.Reset();
        var publishing = fixture.PublishAsync(waiting, deadline.Token);
        try {
            await fixture.Sql.WaitForProjectWaiterAsync(pid, deadline.Token);
            fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        } finally {
            await held.RollbackAsync(CancellationToken.None);
        }
        Assert.Equal(ProjectTaskPlanRefusal.BudgetElapsed, Result(await publishing).Refusal);
        await fixture.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Unknown_commit_is_reconciled_from_the_original_receipt_without_resuming_work() {
        await using var fixture = await CreateAsync();
        var waiting = await fixture.PrepareAsync();
        fixture.Sql.Faults.Commit.AfterCommit = () => {
            fixture.Sql.Probe.BeforeCommand = (command, _) => command.CommandText.Contains("LEFT JOIN \"Workbench_TaskPlanReceipts\"", StringComparison.Ordinal)
                ? Task.FromException(new IOException("receipt-temporarily-unavailable")) : Task.CompletedTask;
            throw new IOException("commit-acknowledgement-lost");
        };
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.PublishAsync(waiting));
        fixture.Sql.Probe.BeforeCommand = null;
        await fixture.Sql.VerifyAsync(1, 1);
        var uncertain = await fixture.ReadAsync();
        Assert.Equal(AgentToolProposalState.ReconciliationRequired,
            AgentToolJournalTransitions.RequireProposal(uncertain.ToolAdmission!, Binding(waiting)).State);
        var originalWork = uncertain.ToolAdmission!.PlanningDecision!.Work;
        await using (var lease = await fixture.Journal.AcquireInterruptedReconciliationAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            await fixture.Journal.ReconcileInterruptedAsync(lease,
                fixture.Sql.Services.GetServices<IAgentToolReceiptReconciliationProvider>().ToArray(), fixture.Participant.CanReadAsync, default);
        }
        var reconciled = await fixture.ReadAsync();
        var proposal = AgentToolJournalTransitions.RequireProposal(reconciled.ToolAdmission!, Binding(waiting));
        Assert.Equal(AgentToolEffectState.Committed, proposal.EffectState);
        Assert.NotNull(proposal.OwnerReceipt);
        Assert.Equal(originalWork, reconciled.ToolAdmission!.PlanningDecision!.Work);
        await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.Owner.RefreshAsync(
            AgentPlanningDecisionExpectation.From(reconciled), false, fixture.Participant.CanReadAsync, default));
        Assert.Single(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Another_operation_cannot_restore_as_the_original_approved_publication() {
        await using var fixture = await CreateAsync();
        var waiting = await fixture.PrepareAsync();
        var binding = Binding(waiting);
        var payload = AgentToolJournalTransitions.RequireProposal(waiting.ToolAdmission!, binding).Payload;
        var source = ProjectTaskPlanAgentCodec.Read<ProjectTaskPlanAgentSource>(payload.SourcePreparation!, ProjectTaskPlanAgentCodec.SourceFormat, 65_536);
        var altered = source with { Operation = new(source.Operation.Namespace, source.Operation.Epoch, Guid.NewGuid(), source.Operation.CallerBinding) };
        using var arguments = JsonDocument.Parse(payload.ArgumentsJson);
        var replacement = ProjectTaskPlanAgentCodec.Restore(arguments.RootElement,
            ProjectTaskPlanAgentCodec.Write(ProjectTaskPlanAgentCodec.SourceFormat, altered, 65_536));
        Assert.Throws<AgentToolAdmissionException>(() => ProjectTaskPlanAgentCodec.Resolve(AgentPlanningDecisionObservation.From(waiting)!,
            waiting.ToolAdmission!.Session, replacement, new(binding.BatchId, binding.IntentId, replacement.SemanticVersion, replacement.Digest)));
        await fixture.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deleted_or_recreated_target_cannot_resume_the_original_plan(bool recreate) {
        await using var fixture = await CreateAsync();
        var waiting = await fixture.PrepareAsync();
        await new ProjectLifecycleTestDriver(fixture.Sql.Services).DeleteAsync(fixture.Sql.Project.ProjectId);
        if (recreate) {
            await using var context = await fixture.Sql.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
            context.Add(new Project { Id = fixture.Sql.Project.ProjectId, Name = "Replacement lifetime", Slug = Guid.NewGuid().ToString("N") });
            await context.SaveChangesAsync();
            Assert.NotEqual(fixture.Sql.Project, await fixture.Sql.Services.GetRequiredService<ProjectWriteAdmissionService>()
                .CaptureAsync(fixture.Sql.Project.ProjectId));
        }
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.ResumeAsync(waiting));
        Assert.Equal(waiting.Revision, (await fixture.ReadAsync()).Revision);
        await fixture.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Read_revocation_prevents_approval_before_opening_a_work_interval() {
        await using var fixture = await CreateAsync();
        var waiting = await fixture.PrepareAsync();
        await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent.Id != fixture.Agent.Id ? agent : agent with {
                ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, new())
            }).ToArray()
        });
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.ResumeAsync(waiting));
        Assert.Equal(waiting.Revision, (await fixture.ReadAsync()).Revision);
        await fixture.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Lost_sql_acknowledgement_recovers_the_original_receipt_after_releasing_authority() {
        await using var fixture = await CreateAsync();
        var waiting = await fixture.PrepareAsync();
        fixture.Sql.Faults.Commit.AfterCommit = () => throw new IOException("commit-acknowledgement-lost");
        var result = Result(await fixture.PublishAsync(waiting));
        Assert.Equal(ProjectTaskPlanAgentResultKind.Published, result.Kind);
        Assert.True(result.Replayed);
        Assert.Single(fixture.Wire.Requests);
        await fixture.Sql.VerifyAsync(1, 1);
    }
}
