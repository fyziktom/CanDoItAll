using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProjectTaskPlanProcessRuntimeTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Registered_inherited_participant_and_saved_tool_publish_original_material_once(bool agentSource, bool sameExecutor) {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync(agentSource, sameExecutor);
        var source = fixture.Source;
        var waiting = await fixture.PrepareAsync();
        var original = waiting.ToolAdmission!.PlanningDecision!;
        Assert.Equal(source.Process.Material.PayloadJson, (await fixture.Participant.ReadOriginalBasisAsync(original.InheritedOrigin!, default)).Value.PayloadJson);
        Assert.Single(source.Sql.Services.GetServices<IAgentRuntimeToolProvider>().Where(item => item.SupportsSavedEffect(
            AgentToolJournalTransitions.RequireProposal(waiting.ToolAdmission, ProjectTaskPlanAgentFixture.Binding(waiting)).Payload)));
        Assert.Single(source.Sql.Services.GetServices<IAgentToolReceiptReconciliationProvider>()
            .Where(item => item.Supports(ProjectStructureToolPolicy.ProjectTaskPlanPublish)));
        var result = ProjectTaskPlanAgentFixture.Result(await fixture.PublishAsync(waiting));
        Assert.Equal(ProjectTaskPlanAgentResultKind.Published, result.Kind);
        Assert.False(result.Replayed);
        Assert.Single(result.TaskIds);
        await source.Sql.VerifyAsync(1, 1);
        Assert.Equal(original.InheritedOrigin, (await source.ReadAsync()).ToolAdmission!.PlanningDecision!.InheritedOrigin);
        Assert.Equal(AgentPlanningGenerationState.Completed, (await source.ReadAsync()).ToolAdmission!.PlanningDecision!.Current.State);
        var request = Assert.Single(fixture.Wire.Requests);
        Assert.Contains("Task-plan owner proof", request, StringComparison.Ordinal);
        Assert.DoesNotContain("\"tools\"", request, StringComparison.Ordinal);
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.PublishAsync(waiting));
        Assert.Single(fixture.Wire.Requests);
        await source.Sql.VerifyAsync(1, 1);
    }

    [Theory]
    [InlineData(ReadRevocation.SourceRead)]
    [InlineData(ReadRevocation.ExecutorTools)]
    [InlineData(ReadRevocation.ProfileGeneration)]
    public async Task Current_read_revocation_denies_original_data_without_recapture_or_publication(ReadRevocation revocation) {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        var source = fixture.Source;
        var waiting = await fixture.PrepareAsync();
        if (revocation == ReadRevocation.ProfileGeneration) {
            source.Generations.Advance();
        } else if (revocation == ReadRevocation.ExecutorTools) {
            await source.RevokeAsync(source.Executor.Id, agent => agent with { Permissions = agent.Permissions with { CanUseTools = false } });
        } else {
            await source.RevokeAsync(source.Launcher!.Id, agent => agent with {
                ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, new())
            });
        }
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.Participant.RequireCurrentReadAsync(waiting.ToolAdmission!.PlanningDecision!, default));
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.PublishAsync(waiting));
        await source.Sql.VerifyAsync(0, 0);
        Assert.Single(fixture.Wire.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Write_revocation_preserves_read_access_but_refuses_saved_publication(bool launcher) {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        var source = fixture.Source;
        var waiting = await fixture.PrepareAsync();
        await source.RevokeAsync(launcher ? source.Launcher!.Id : source.Executor.Id, agent => {
            var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
            access.CanWrite = false;
            access.CanWriteTasks = false;
            return agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, access) };
        });
        await fixture.Participant.RequireCurrentReadAsync(waiting.ToolAdmission!.PlanningDecision!, default);
        if (launcher) {
            await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.PublishAsync(waiting));
        } else {
            var refused = ProjectTaskPlanAgentFixture.Result(await fixture.PublishAsync(waiting));
            Assert.Equal(ProjectTaskPlanRefusal.AuthorityDenied, refused.Refusal);
        }
        await source.Sql.VerifyAsync(0, 0);
        Assert.Single(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Missing_original_operation_contract_denies_saved_publication_before_owner_effect() {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync(includeOperationContract: false);
        var waiting = await fixture.PrepareAsync();
        var result = await fixture.PublishAsync(waiting);
        Assert.Null(result.OwnerResult);
        Assert.Equal(AgentToolEffectState.NotCommitted, result.EffectState);
        var retained = await fixture.Source.ReadAsync();
        var proposal = AgentToolJournalTransitions.RequireProposal(retained.ToolAdmission!, result.Binding);
        Assert.Equal(AgentToolProposalState.Completed, proposal.State);
        Assert.Contains("ToolPolicyDenied", proposal.Result!.PayloadJson, StringComparison.Ordinal);
        Assert.Equal(AgentPlanningGenerationState.Failed, retained.ToolAdmission!.PlanningDecision!.Current.State);
        await fixture.Source.Sql.VerifyAsync(0, 0);
        Assert.Single(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Changed_project_basis_pauses_without_replacing_original_inherited_material() {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        var source = fixture.Source;
        var waiting = await fixture.PrepareAsync();
        Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await source.Sql.Publisher().PublishAsync(await source.Sql.CommandAsync(1), source.Sql.Invocation));
        var result = ProjectTaskPlanAgentFixture.Result(await fixture.PublishAsync(waiting));
        Assert.Equal(ProjectTaskPlanRefusal.BasisChanged, result.Refusal);
        Assert.Equal(AgentPlanningGenerationState.PausedBasisChanged, (await source.ReadAsync()).ToolAdmission!.PlanningDecision!.Current.State);
        Assert.Equal(source.Process.Material.PayloadJson,
            (await fixture.Participant.ReadOriginalBasisAsync(waiting.ToolAdmission!.PlanningDecision!.InheritedOrigin!, default)).Value.PayloadJson);
        await source.Sql.VerifyAsync(1, 1);
        Assert.Single(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Claim_expiry_after_actual_task_flush_rolls_back_registered_tool_effect() {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        var waiting = await fixture.PrepareAsync();
        var flushed = false;
        fixture.Source.Sql.Faults.Save.AfterFlush = _ => {
            flushed = true;
            fixture.Source.ExpireClaim();
            return Task.CompletedTask;
        };
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.PublishAsync(waiting));
        Assert.True(flushed);
        var retained = await fixture.Source.ReadAsync();
        var proposal = AgentToolJournalTransitions.RequireProposal(retained.ToolAdmission!, ProjectTaskPlanAgentFixture.Binding(waiting));
        Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
        var checkpoint = MafToolResultCheckpointCodec.Decode(proposal.Result!);
        var result = checkpoint.Value.Deserialize<ProjectTaskPlanAgentResult>(MafToolProtocolCodec.SerializationOptions)!;
        Assert.Equal(ProjectTaskPlanRefusal.SourceChanged, result.Refusal);
        await fixture.Source.Sql.VerifyAsync(0, 0);
        Assert.Single(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Lost_commit_acknowledgement_reconciles_original_receipt_without_provider_replay() {
        await using var fixture = await ProjectTaskPlanProcessRuntimeFixture.CreateAsync();
        var source = fixture.Source;
        var waiting = await fixture.PrepareAsync();
        source.Sql.Faults.Commit.AfterCommit = () => {
            source.Sql.Probe.BeforeCommand = (command, _) => command.CommandText.Contains("LEFT JOIN \"Workbench_TaskPlanReceipts\"", StringComparison.Ordinal)
                ? Task.FromException(new IOException("receipt-temporarily-unavailable")) : Task.CompletedTask;
            throw new IOException("commit-acknowledgement-lost");
        };
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.PublishAsync(waiting));
        source.Sql.Probe.BeforeCommand = null;
        await source.Sql.VerifyAsync(1, 1);
        var uncertain = await source.ReadAsync();
        var binding = ProjectTaskPlanAgentFixture.Binding(waiting);
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, AgentToolJournalTransitions.RequireProposal(uncertain.ToolAdmission!, binding).State);
        var originalWork = uncertain.ToolAdmission!.PlanningDecision!.Work;
        source.ExpireClaim();
        await using (var lease = await source.Journal.AcquireInterruptedReconciliationAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            await source.Journal.ReconcileInterruptedAsync(lease,
                source.Sql.Services.GetServices<IAgentToolReceiptReconciliationProvider>().ToArray(), fixture.CanReadAsync, default);
        }
        var recovered = await source.ReadAsync();
        var proposal = AgentToolJournalTransitions.RequireProposal(recovered.ToolAdmission!, binding);
        Assert.Equal(AgentToolEffectState.Committed, proposal.EffectState);
        Assert.NotNull(proposal.OwnerReceipt);
        Assert.Equal(originalWork, recovered.ToolAdmission!.PlanningDecision!.Work);
        Assert.Single(fixture.Wire.Requests);
        await source.Sql.VerifyAsync(1, 1);
    }

    public enum ReadRevocation { SourceRead, ExecutorTools, ProfileGeneration }
}
