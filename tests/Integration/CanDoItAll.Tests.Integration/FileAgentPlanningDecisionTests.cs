using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;
using static CanDoItAll.Tests.Support.AgentPlanningDecisionTestData;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class FileAgentPlanningDecisionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Both_file_writers_refuse_changed_provenance_and_preserve_the_original_bytes(bool fullReplacement) {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = AwaitApproval(Respond(Enter(Attach(Begin(Create(source.Detail.Run.ToolAdmission!.Session))))), TimeSpan.FromSeconds(4));
        await using var files = await AgentToolAdmissionJournalFixture.RestoreRetainedAsync(source.Agent, source.Provider, source.Profile,
            source.Detail.Run with { ToolAdmission = journal });
        var before = await files.NewStore().GetExecutionRunDetailAsync(files.Session.ExecutionRunId);
        Assert.NotNull(before);
        var serialized = JsonSerializer.Serialize(before.Run.ToolAdmission);
        ExecutionRunDetail Tamper(ExecutionRunDetail current) {
            var original = current.Run.ToolAdmission!;
            return current with { Run = current.Run with {
                Revision = current.Run.Revision + 1,
                ToolAdmission = original with {
                    Revision = original.Revision + 1,
                    PlanningDecision = original.PlanningDecision! with {
                        Provenance = original.PlanningDecision.Provenance with { WorkspaceHeadRevision = Guid.NewGuid() }
                    }
                }
            } };
        }
        await Assert.ThrowsAsync<AgentToolAdmissionException>(async () => {
            if (fullReplacement) {
                await files.Store.SaveExecutionRunDetailAsync(Tamper(before));
            } else {
                await ((ISandboxWorkspaceExecutionRunMutationStore)files.Store).UpdateExecutionRunDetailAsync(before.Run.Id, Tamper);
            }
        });
        var reloaded = await files.NewStore().GetExecutionRunDetailAsync(before.Run.Id);
        Assert.Equal(serialized, JsonSerializer.Serialize(reloaded!.Run.ToolAdmission));
        Assert.Equal(before.Run.Revision, reloaded.Run.Revision);
        Assert.Equal(TimeSpan.FromSeconds(4).Ticks, reloaded.Run.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
    }

    [Fact]
    public async Task File_restart_retains_the_closed_wait_and_refuses_legacy_provider_entry() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = AwaitApproval(Respond(Enter(Attach(Begin(Create(source.Detail.Run.ToolAdmission!.Session))))), TimeSpan.FromSeconds(5));
        await using var files = await AgentToolAdmissionJournalFixture.RestoreRetainedAsync(source.Agent, source.Provider, source.Profile,
            source.Detail.Run with { ToolAdmission = journal });
        var owner = files.NewJournal(files.NewStore());
        await using var lease = await owner.AcquireRunAsync(files.Session, default);
        using var bound = lease.Bind();
        var retained = await owner.ReadAsync(lease, default);
        Assert.Equal(AgentPlanningDecisionCodec.Serialize(journal.PlanningDecision!),
            AgentPlanningDecisionCodec.Serialize(retained.PlanningDecision!));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => owner.BeginEffectSegmentAsync(lease, default));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => owner.BeginSegmentAsync(lease, Envelope(), null, default));
        var reloaded = await files.NewStore().GetExecutionRunAsync(files.Session.ExecutionRunId);
        Assert.Equal(AgentPlanningGenerationState.AwaitingApproval, reloaded!.ToolAdmission!.PlanningDecision!.Current.State);
        Assert.Equal(TimeSpan.FromSeconds(5).Ticks, reloaded.ToolAdmission.PlanningDecision.Work.ClosedWorkTicks);
    }

    [Fact]
    public async Task Existing_file_run_cannot_be_promoted_to_schema_six() {
        await using var files = await AgentToolAdmissionJournalFixture.CreateAsync();
        var original = files.Detail.Run.ToolAdmission!;
        var planning = Create(original.Session) with { Revision = original.Revision + 1 };
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => files.Store.SaveExecutionRunDetailAsync(files.Detail with {
            Run = files.Detail.Run with { Revision = files.Detail.Run.Revision + 1, ToolAdmission = planning }
        }));
        var retained = await files.NewStore().GetExecutionRunAsync(files.Session.ExecutionRunId);
        Assert.Equal(original.SchemaVersion, retained!.ToolAdmission!.SchemaVersion);
        Assert.Null(retained.ToolAdmission.PlanningDecision);
    }
}
