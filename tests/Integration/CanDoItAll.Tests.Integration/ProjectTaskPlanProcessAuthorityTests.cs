using System.Data;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProjectTaskPlanProcessAuthorityTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Actual_original_child_and_live_work_publish_once_with_separate_source_and_executor(bool agentSource, bool sameExecutor) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(agentSource, sameExecutor);
        var waiting = await fixture.PrepareAsync();
        var original = waiting.ToolAdmission!.PlanningDecision!;
        Assert.Equal(waiting.CreatedAtUtc, fixture.Process.Source.Evidence.ExecutionCreatedAtUtc);
        var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.PublishAsync(waiting));
        Assert.False(accepted.Replayed);
        Assert.Equal(ProjectTaskPlanSourceKind.ProcessAgentDecision, accepted.Acknowledgement.Source.Kind);
        Assert.Equal(ProjectTaskPlanSource.ActiveWorkVersion, accepted.Acknowledgement.Source.Version);
        await fixture.Sql.VerifyAsync(1, 1, accepted.Acknowledgement);
        if (!sameExecutor) {
            var personal = AgentProjectStructureAccessMetadata.Read(fixture.Executor.ConfigurationJson);
            Assert.False(personal.AllowAllProjects);
            Assert.Empty(personal.AllowedProjectIds);
            Assert.Empty(personal.AllowedProjectLifetimes);
        }
        fixture.ExpireClaim();
        var replay = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.ReplayAsync());
        Assert.True(replay.Replayed);
        Assert.Equal(ProjectTaskPlanOperationCodec.EncodeAccepted(accepted.Acknowledgement),
            ProjectTaskPlanOperationCodec.EncodeAccepted(replay.Acknowledgement));
        Assert.Equal(original.InheritedOrigin, (await fixture.ReadAsync()).ToolAdmission!.PlanningDecision!.InheritedOrigin);
        var child = (await fixture.Sql.Services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(fixture.Process.Parent.Child.Address))!;
        Assert.Null(child.Closure);
        Assert.Null(child.Settlement);
        await RequireReleasedAsync(fixture);
    }

    [Theory]
    [InlineData(true, Revocation.Tools)]
    [InlineData(true, Revocation.Write)]
    [InlineData(true, Revocation.Inactive)]
    [InlineData(true, Revocation.Template)]
    [InlineData(true, Revocation.Missing)]
    [InlineData(true, Revocation.ExactLifetime)]
    [InlineData(false, Revocation.Tools)]
    [InlineData(false, Revocation.Write)]
    [InlineData(false, Revocation.Inactive)]
    [InlineData(false, Revocation.Template)]
    [InlineData(false, Revocation.Missing)]
    public async Task Current_launcher_or_executor_revocation_refuses_without_borrowing_the_other_grant(bool launcher, Revocation kind) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync();
        var waiting = await fixture.PrepareAsync();
        await fixture.RevokeAsync(launcher ? fixture.Launcher!.Id : fixture.Executor.Id, agent => Revoke(agent, kind, fixture.Sql.Project));
        var outcome = Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.PublishAsync(waiting));
        Assert.Equal(kind == Revocation.Missing ? ProjectTaskPlanRefusal.SourceChanged : ProjectTaskPlanRefusal.AuthorityDenied, outcome.Reason);
        await fixture.Sql.VerifyAsync(0, 0);
        await RequireReleasedAsync(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_parent_claim_expiry_before_staging_or_after_flush_rolls_back_every_effect(bool afterFlush) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync();
        var waiting = await fixture.PrepareAsync();
        var flushed = false;
        if (afterFlush) {
            fixture.Sql.Faults.Save.AfterFlush = _ => {
                flushed = true;
                fixture.ExpireClaim();
                return Task.CompletedTask;
            };
        } else {
            fixture.BeforePublication = () => {
                fixture.ExpireClaim();
                return Task.CompletedTask;
            };
        }
        var outcome = Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.PublishAsync(waiting, settleOwner: false));
        Assert.Equal(ProjectTaskPlanRefusal.SourceChanged, outcome.Reason);
        Assert.Equal(afterFlush, flushed);
        var retained = await fixture.ReadAsync();
        var proposal = AgentToolJournalTransitions.RequireProposal(retained.ToolAdmission!, ProjectTaskPlanAgentFixture.Binding(waiting));
        Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
        Assert.Equal(AgentToolProposalState.Completed, proposal.State);
        Assert.NotNull(retained.ToolAdmission!.PlanningDecision!.Work.Open);
        await fixture.Sql.VerifyAsync(0, 0);
        await RequireReleasedAsync(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Operator_launched_executor_profile_generation_is_rechecked_before_staging_and_after_flush(bool afterFlush) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(agentSource: false);
        var waiting = await fixture.PrepareAsync();
        var flushed = false;
        if (afterFlush) {
            fixture.Sql.Faults.Save.AfterFlush = _ => {
                flushed = true;
                fixture.Generations.Advance();
                return Task.CompletedTask;
            };
        } else {
            fixture.BeforePublication = () => {
                fixture.Generations.Advance();
                return Task.CompletedTask;
            };
        }
        Assert.Equal(ProjectTaskPlanRefusal.SourceChanged,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.PublishAsync(waiting, settleOwner: false)).Reason);
        Assert.Equal(afterFlush, flushed);
        await fixture.Sql.VerifyAsync(0, 0);
        await RequireReleasedAsync(fixture);
    }

    [Fact]
    public async Task Operator_profile_change_after_the_enlisted_recheck_is_refused_by_final_live_work_check() {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(agentSource: false);
        var waiting = await fixture.PrepareAsync();
        var changed = false;
        fixture.Sql.Faults.Save.AfterFlush = _ => {
            fixture.Sql.Probe.AfterReader = (command, _) => {
                if (command.CommandText.Contains("clock_timestamp()", StringComparison.Ordinal)) {
                    fixture.Sql.Probe.AfterReader = null;
                    fixture.Generations.Advance();
                    changed = true;
                }
                return Task.CompletedTask;
            };
            return Task.CompletedTask;
        };
        Assert.Equal(ProjectTaskPlanRefusal.SourceChanged,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.PublishAsync(waiting, settleOwner: false)).Reason);
        Assert.True(changed);
        await fixture.Sql.VerifyAsync(0, 0);
        await RequireReleasedAsync(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_finite_work_exhaustion_before_staging_or_after_flush_rolls_back_every_effect(bool afterFlush) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync();
        var waiting = await fixture.PrepareAsync();
        Task Exhaust() {
            fixture.Clock.Timestamp += fixture.Process.Parent.Child.Grant.WorkTicks;
            return Task.CompletedTask;
        }
        if (afterFlush) {
            fixture.Sql.Faults.Save.AfterFlush = _ => Exhaust();
        } else {
            fixture.BeforePublication = Exhaust;
        }
        var outcome = Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.PublishAsync(waiting));
        Assert.Equal(ProjectTaskPlanRefusal.BudgetElapsed, outcome.Reason);
        await fixture.Sql.VerifyAsync(0, 0);
        await RequireReleasedAsync(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Snapshot_requires_the_exact_launcher_set_and_current_original_run_revision(bool staleRevision) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync();
        var waiting = await fixture.PrepareAsync();
        fixture.ChangeExpectation = expected => AgentPlanningPublicationExpectation.Inherited(expected.Scope, expected.Session,
            expected.RunRevision - (staleRevision ? 1 : 0), expected.JournalRevision, staleRevision ? expected.AdditionalAgentId : null);
        Assert.Equal(ProjectTaskPlanRefusal.SourceChanged,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.PublishAsync(waiting)).Reason);
        await fixture.Sql.VerifyAsync(0, 0);
        await RequireReleasedAsync(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_or_recreated_original_project_cannot_receive_the_saved_plan(bool recreate) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(agentSource: false);
        var waiting = await fixture.PrepareAsync();
        fixture.BeforePublication = async () => {
            await new ProjectLifecycleTestDriver(fixture.Sql.Services).DeleteAsync(fixture.Sql.Project.ProjectId);
            if (recreate) {
                await using var context = await fixture.Sql.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
                context.Add(new Project { Id = fixture.Sql.Project.ProjectId, Name = "Replacement lifetime", Slug = Guid.NewGuid().ToString("N") });
                await context.SaveChangesAsync();
            }
        };
        Assert.Equal(ProjectTaskPlanRefusal.SourceChanged,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.PublishAsync(waiting)).Reason);
        await fixture.Sql.VerifyAsync(0, 0);
        await RequireReleasedAsync(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lost_commit_acknowledgement_keeps_one_original_receipt_and_never_reenters_publication(bool unreadable) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync();
        var waiting = await fixture.PrepareAsync();
        fixture.Sql.Faults.Commit.AfterCommit = () => {
            if (unreadable) {
                fixture.Sql.Probe.BeforeCommand = (command, _) => command.CommandText.Contains("LEFT JOIN \"Workbench_TaskPlanReceipts\"", StringComparison.Ordinal)
                    ? Task.FromException(new IOException("receipt-unavailable-after-inherited-commit")) : Task.CompletedTask;
            }
            throw new IOException("inherited-commit-acknowledgement-lost");
        };
        var outcome = await fixture.PublishAsync(waiting);
        if (unreadable) {
            Assert.IsType<ProjectTaskPlanOutcome.Unconfirmed>(outcome);
        } else {
            Assert.True(Assert.IsType<ProjectTaskPlanOutcome.Accepted>(outcome).Replayed);
        }
        fixture.Sql.Probe.BeforeCommand = null;
        await fixture.Sql.VerifyAsync(1, 1);
        fixture.ExpireClaim();
        var recovered = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.ReplayAsync());
        Assert.True(recovered.Replayed);
        await fixture.Sql.VerifyAsync(1, 1, recovered.Acknowledgement);
        await RequireReleasedAsync(fixture);
    }

    [Fact]
    public async Task Work_is_rechecked_after_waiting_for_the_actual_project_lock() {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync();
        var waiting = await fixture.PrepareAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var blocker = await fixture.Sql.ContextAsync(deadline.Token);
        await using var held = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, deadline.Token);
        var pid = await blocker.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync(deadline.Token);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(blocker,
            [ProjectMutationScopeKeys.ForProject(fixture.Sql.Project.ProjectId)], deadline.Token);
        fixture.Sql.Probe.Reset();
        var pending = fixture.PublishAsync(waiting, deadline.Token);
        try {
            await fixture.Sql.WaitForProjectWaiterAsync(pid, deadline.Token);
            fixture.Clock.Timestamp += fixture.Process.Parent.Child.Grant.WorkTicks;
        } finally {
            await held.RollbackAsync(CancellationToken.None);
        }
        Assert.Equal(ProjectTaskPlanRefusal.BudgetElapsed, Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await pending).Reason);
        await fixture.Sql.VerifyAsync(0, 0);
        await RequireReleasedAsync(fixture);
    }

    private static async Task RequireReleasedAsync(ProjectTaskPlanProcessFixture fixture) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await fixture.Store.UpdateCatalogAsync(catalog => catalog, deadline.Token);
    }

    private static AgentDefinition? Revoke(AgentDefinition agent, Revocation kind, ProjectWriteAdmission project) => kind switch {
        Revocation.Tools => agent with { Permissions = agent.Permissions with { CanUseTools = false } },
        Revocation.Write => agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write("{}", new() {
            CanRead = true, AllowAllProjects = true
        }) },
        Revocation.Inactive => agent with { Status = AgentLifecycleStatus.Archived },
        Revocation.Template => agent with { IsTemplate = true },
        Revocation.Missing => null,
        Revocation.ExactLifetime => agent with {
            ConfigurationJson = AgentProjectStructureAccessMetadata.RevokeProjectLifetime(agent.ConfigurationJson,
                AgentProjectStructureRevocationTarget.ForLifetime(new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId))).ConfigurationJson
        },
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public enum Revocation { Tools, Write, Inactive, Template, Missing, ExactLifetime }
}
