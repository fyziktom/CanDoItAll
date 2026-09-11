using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafWorkspaceToolResultDisclosureIntegrationTests {
    [Fact]
    public async Task An_interactive_journal_cannot_acquire_background_artifact_authority_from_ambient_process_metadata() {
        await using var fixture = await Fixture.CreateAsync(ToolContractCatalog.WorkspaceReadFile);
        await fixture.ConfigureExecutionArtifactAsync(useProject: false, admitBackground: false);
        var original = fixture.AdmittedRun;
        Assert.Null(fixture.AdmittedSession.BackgroundSource);
        Assert.Equal(AgentRuntimeContextPurpose.InteractiveChat, original.ToolAdmission!.Session.Purpose);
        await fixture.CompleteThroughLostFinalAcknowledgementAsync();
        var saved = await fixture.ProposalAsync();
        Assert.Equal(AgentToolProposalState.Completed, saved.State);
        Assert.Equal(AgentToolEffectState.NotCommitted, saved.EffectState);
        Assert.Null(saved.DisclosureEvidence);
        Assert.Contains("workspace.result-disclosure-denied", saved.Result!.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("original current-run artifact", saved.Result.PayloadJson, StringComparison.Ordinal);
        var retained = (await fixture.Journal.NewStore().GetExecutionRunAsync(original.Id))!;
        Assert.Equal(original.ToolAdmission.Session, retained.ToolAdmission!.Session);
        Assert.Equal(original.MetadataJson, retained.MetadataJson);
        Assert.Equal(original.ProcessRunId, retained.ProcessRunId);
        Assert.Equal(original.ProcessStepId, retained.ProcessStepId);
        var restored = new ToolClient(fixture.ToolName);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(restored));
        Assert.Contains("workspace.result-authority-unavailable", Codes(denied));
        Assert.Equal(0, restored.Requests);
        Assert.Equal(saved, await fixture.ProposalAsync());
    }

    private sealed partial class Fixture {
        private ExecutionRunRecord? backgroundRun;
        private BackgroundArtifactSource? backgroundSource;
        internal ExecutionRunRecord AdmittedRun => backgroundRun ?? journal.Detail.Run;
        internal AgentToolSessionReference AdmittedSession => AdmittedRun.ToolAdmission!.Session.Reference;

        private AgentToolAdmissionJournal NewAdmissionJournal()
            => backgroundSource is null ? journal.NewJournal(journal.NewStore())
                : new(journal.NewStore(), journal.Profile, backgroundSources: [backgroundSource]);

        private async Task AdmitBackgroundArtifactAsync() {
            Assert.Null(backgroundRun);
            var run = journal.Detail.Run with {
                Id = Guid.NewGuid(), ChatSessionId = null,
                SourceKind = BackgroundArtifactSource.Kind, SourceId = BackgroundArtifactSource.StepKey,
                CorrelationId = processRunId, CausationId = processStepId,
                RequestedBy = BackgroundArtifactSource.Requestor, RequestedByKind = "system",
                ProcessRunId = processRunId, ProcessStepId = processStepId,
                MetadataJson = "{}", ToolAdmission = null, State = ExecutionState.Preparing,
                Outcome = null, Revision = 1, PendingApprovals = []
            };
            backgroundSource = new(journal.Profile, run);
            run = run with {
                ToolAdmission = await NewAdmissionJournal().CreateForNewBackgroundRunAsync(run, "Run the reviewed workspace operation.")
            };
            await journal.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
            backgroundRun = run;
            var persisted = (await journal.NewStore().GetExecutionRunDetailAsync(run.Id))!;
            Assert.Null(persisted.ChatSession);
            Assert.Equal(run.ToolAdmission.Session, persisted.Run.ToolAdmission!.Session);
            Assert.Equal(run.ToolAdmission.BackgroundInput, persisted.Run.ToolAdmission.BackgroundInput);
            Assert.NotEqual(journal.Session.ExecutionRunId, AdmittedSession.ExecutionRunId);
        }

        internal void SetBackgroundReadAllowed(bool allowed)
            => Assert.IsType<BackgroundArtifactSource>(backgroundSource).ReadAllowed = allowed;

        private async Task ApproveAsync(IReadOnlyList<PendingToolApprovalRecord> pending) {
            if (backgroundSource is null) {
                await journal.ApproveAsync(pending);
                return;
            }
            await ((ISandboxWorkspaceExecutionRunMutationStore)journal.Store).UpdateExecutionRunDetailAsync(AdmittedSession.ExecutionRunId,
                current => {
                    var decisions = pending.Select(item => new PendingToolApprovalDecision(item.ApprovalId, true) {
                        ToolAdmission = item.ToolAdmission
                    }).ToArray();
                    var approved = AgentToolJournalTransitions.ApplyDecisions(current.Run.ToolAdmission!, pending, decisions, automatic: false);
                    return current with { Run = current.Run with {
                        ToolAdmission = approved, PendingApprovals = [], State = ExecutionState.Running,
                        Revision = current.Run.Revision + 1, UpdatedAtUtc = DateTimeOffset.UtcNow
                    } };
                });
        }
    }

    private sealed class BackgroundArtifactSource(AgentToolProfileBinding originalProfile, ExecutionRunRecord original)
        : IAgentToolBackgroundSourcePolicy {
        internal const string Kind = "process-step";
        internal const string StepKey = "workspace-artifact-step";
        internal const string Requestor = "process-runtime";
        public string SourceKind => Kind;
        internal bool ReadAllowed { get; set; } = true;
        private readonly AgentToolSemanticDigest fingerprint = AgentToolProtocolEnvelope.ComputeDigest(original.Id.ToString("D"));

        public ValueTask<AgentToolBackgroundSourceObservation> ObserveAsync(ExecutionRunRecord execution,
            AgentToolProfileBinding profile, CancellationToken cancellationToken = default) {
            Assert.Equal(originalProfile, profile);
            Assert.Equal(original.Id, execution.Id);
            Assert.Equal(original.AgentId, execution.AgentId);
            Assert.Equal(original.SourceKind, execution.SourceKind);
            Assert.Equal(original.SourceId, execution.SourceId);
            Assert.Equal(original.ProcessRunId, execution.ProcessRunId);
            Assert.Equal(original.ProcessStepId, execution.ProcessStepId);
            Assert.Equal(original.CorrelationId, execution.CorrelationId);
            Assert.Equal(original.CausationId, execution.CausationId);
            Assert.Equal(original.RequestedBy, execution.RequestedBy);
            Assert.Equal(original.RequestedByKind, execution.RequestedByKind);
            Assert.Equal(original.MetadataJson, execution.MetadataJson);
            Assert.Null(execution.ChatSessionId);
            return ValueTask.FromResult(new AgentToolBackgroundSourceObservation(fingerprint, ReadAllowed, DispatchAllowed: true));
        }
    }
}
