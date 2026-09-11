using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class MafSdkSkillAdmissionIntegrationTests {
    [Theory]
    [InlineData(AgentSkillsProvider.LoadSkillToolName, false)]
    [InlineData(AgentSkillsProvider.LoadSkillToolName, true)]
    [InlineData(AgentSkillsProvider.ReadSkillResourceToolName, false)]
    [InlineData(AgentSkillsProvider.ReadSkillResourceToolName, true)]
    public async Task Actual_SDK_context_tools_are_admitted_in_skills_only_and_mixed_runtime(string toolName, bool mixed) {
        await using var fixture = await Fixture.CreateAsync();
        var client = new SkillClient(toolName, mixed);
        var response = await fixture.ExecuteAsync(client);
        Assert.Equal("completed", response.ResponseText);
        Assert.Equal(2, client.Requests);
        Assert.Contains("Retained", client.Inputs[^1], StringComparison.Ordinal);
        var saved = await fixture.ReadJournalAsync();
        Assert.Single(saved.Segments);
        var proposals = Assert.Single(saved.Batches.Where(batch => batch.Proposals.Length != 0)).Proposals;
        Assert.Equal(mixed ? 2 : 1, proposals.Length);
        Assert.Equal(toolName, proposals[0].Payload.ToolName);
        Assert.All(proposals, proposal => Assert.Equal(AgentToolProposalState.Completed, proposal.State));
        Assert.Equal(AgentToolProposalRecovery.RevalidateAndRead, proposals[0].Payload.Recovery);
        Assert.Equal(0, fixture.Process.Commands);
        Assert.Equal(mixed ? 1 : 0, fixture.OrdinaryCalls);
        var returned = proposals[0].Result;
        var replay = new SkillClient(toolName, mixed);
        Assert.Equal("completed", (await fixture.ExecuteAsync(replay)).ResponseText);
        Assert.Equal(0, replay.Requests);
        Assert.NotNull(proposals[0].DisclosureEvidence);
        Assert.Equal(mixed ? 1 : 0, fixture.OrdinaryCalls);
        Assert.Equal(returned, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0].Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_SDK_file_script_approval_binds_late_function_and_persists_lost_ack_without_repeat(bool mixed) {
        await using var fixture = await Fixture.CreateAsync();
        var initial = new SkillClient(AgentSkillsProvider.RunSkillScriptToolName, mixed);
        var awaiting = await fixture.ExecuteAsync(initial);
        var approval = Assert.Single(awaiting.PendingApprovals);
        Assert.Equal(AgentSkillsProvider.RunSkillScriptToolName, approval.ToolName);
        Assert.NotNull(approval.ToolAdmission);
        Assert.Equal(0, fixture.Process.Commands);
        var before = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        Assert.Equal(AgentToolProposalState.Prepared, before.State);
        Assert.Equal(AgentToolProposalRecovery.ReconcileBeforeRetry, before.Payload.Recovery);
        await fixture.Journal.ApproveAsync(awaiting.PendingApprovals);
        var loss = new SkillClient(AgentSkillsProvider.RunSkillScriptToolName, mixed, loseFinalAck: true);
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(loss));
        Assert.Contains(SkillClient.LostAcknowledgement, exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, fixture.Process.Commands);
        Assert.Equal(1, fixture.Process.MaximumActive);
        Assert.Equal(before.IntentId, Assert.Single(fixture.Process.Intents));
        var completed = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        Assert.Equal(AgentToolProposalState.Completed, completed.State);
        Assert.Equal(before.Payload, completed.Payload);
        Assert.Equal(AgentToolEffectState.Unknown, completed.EffectState);
        Assert.Contains("Retained script output", completed.Result!.PayloadJson, StringComparison.Ordinal);
        var restart = new SkillClient(AgentSkillsProvider.RunSkillScriptToolName, mixed);
        Assert.Equal("completed", (await fixture.ExecuteAsync(restart)).ResponseText);
        Assert.Equal(1, restart.Requests);
        Assert.Contains("Retained script output", Assert.Single(restart.Inputs), StringComparison.Ordinal);
        Assert.NotNull(completed.DisclosureEvidence);
        Assert.Equal(1, fixture.Process.Commands);
        Assert.Equal(completed, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Uncertain_file_script_dispatch_requires_reconciliation_before_any_replay(bool mixed) {
        await using var fixture = await Fixture.CreateAsync();
        var awaiting = await fixture.ExecuteAsync(new(AgentSkillsProvider.RunSkillScriptToolName, mixed));
        await fixture.Journal.ApproveAsync(awaiting.PendingApprovals);
        fixture.Process.FailAfterDispatch = true;
        var dispatchClient = new SkillClient(AgentSkillsProvider.RunSkillScriptToolName, mixed);
        var dispatchFailure = await UsageAdmissionFailureAsync(() => fixture.ExecuteAsync(dispatchClient));
        Assert.Equal("tool-admission.reconciliation-required", dispatchFailure.Code);
        var injected = Assert.IsType<IOException>(fixture.Process.InjectedDispatchFailure);
        Assert.Equal("Fixture process acknowledgement was lost after dispatch.", injected.Message);
        Assert.Equal(0, dispatchClient.Requests);
        Assert.Equal(1, fixture.Process.Commands);
        var proposal = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, proposal.State);
        Assert.Equal(AgentToolEffectState.Unknown, proposal.EffectState);
        Assert.Null(proposal.Result);
        var retry = new SkillClient(AgentSkillsProvider.RunSkillScriptToolName, mixed);
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.ExecuteAsync(retry));
        Assert.Equal("tool-admission.reconciliation-required", failure.Code);
        Assert.Equal(0, retry.Requests);
        Assert.Equal(1, fixture.Process.Commands);
        Assert.Equal(proposal, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Two_approved_SDK_script_calls_keep_distinct_durable_intents_and_execute_serially(bool mixed) {
        await using var fixture = await Fixture.CreateAsync();
        var awaiting = await fixture.ExecuteAsync(new(AgentSkillsProvider.RunSkillScriptToolName, mixed, scriptCalls: 2));
        Assert.Equal(2, awaiting.PendingApprovals.Count);
        Assert.Equal(0, fixture.Process.Commands);
        var prepared = (await fixture.ReadJournalAsync()).Batches[0].Proposals;
        Assert.Equal(2, prepared.Select(proposal => proposal.IntentId).Distinct().Count());
        await fixture.Journal.ApproveAsync(awaiting.PendingApprovals);
        var completed = await fixture.ExecuteAsync(new(AgentSkillsProvider.RunSkillScriptToolName, mixed, scriptCalls: 2));
        Assert.Equal("completed", completed.ResponseText);
        Assert.Equal(2, fixture.Process.Commands);
        Assert.Equal(1, fixture.Process.MaximumActive);
        Assert.Equal(prepared.Select(proposal => proposal.IntentId), fixture.Process.Intents);
        Assert.All((await fixture.ReadJournalAsync()).Batches[0].Proposals,
            proposal => Assert.Equal(AgentToolProposalState.Completed, proposal.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unchanged_original_skill_source_can_complete_its_exact_approved_proposal_after_restart(bool mixed) {
        await using var fixture = await Fixture.CreateAsync(projectScope: true);
        var awaiting = await fixture.ExecuteAsync(new(AgentSkillsProvider.RunSkillScriptToolName, mixed));
        var original = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        Assert.Equal(2, original.Payload.SemanticVersion);
        var source = MafSkillSourceEvidence.Read(Assert.IsType<AgentToolProtocolEnvelope>(original.Payload.SourcePreparation));
        Assert.Contains(source.Candidates, item => item.Name == "journal-skill" && item.Origin.Kind == MafSkillOriginKind.File);
        Assert.Contains(fixture.Project!.LifetimeId.ToString(), source.Source.PayloadJson, StringComparison.OrdinalIgnoreCase);
        await fixture.Journal.ApproveAsync(awaiting.PendingApprovals);
        var completed = await fixture.ExecuteAsync(new(AgentSkillsProvider.RunSkillScriptToolName, mixed));
        Assert.Equal("completed", completed.ResponseText);
        Assert.Equal(1, fixture.Process.Commands);
        var saved = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        Assert.Equal(original.IntentId, saved.IntentId);
        Assert.Equal(original.Payload, saved.Payload);
        Assert.Equal(original.Payload.Digest, saved.ApprovedDigest);
        Assert.Equal(AgentToolProposalState.Completed, saved.State);
    }

    [Theory]
    [InlineData(SourceChange.Root, false)]
    [InlineData(SourceChange.Root, true)]
    [InlineData(SourceChange.Inline, false)]
    [InlineData(SourceChange.Inline, true)]
    [InlineData(SourceChange.CapabilityIdentity, false)]
    [InlineData(SourceChange.CapabilityIdentity, true)]
    public async Task Approved_skill_does_not_migrate_to_changed_source_configuration(SourceChange change, bool mixed) {
        await using var fixture = await Fixture.CreateAsync();
        if (change == SourceChange.Inline) {
            await fixture.AddInlineAsync();
        }
        var awaiting = await fixture.ExecuteAsync(new(AgentSkillsProvider.RunSkillScriptToolName, mixed));
        await fixture.Journal.ApproveAsync(awaiting.PendingApprovals);
        var original = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        await fixture.ChangeSourceAsync(change);
        var restarted = new SkillClient(AgentSkillsProvider.RunSkillScriptToolName, mixed);
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.ExecuteAsync(restarted));
        Assert.Equal("tool-admission.source-preparation-changed", denied.Code);
        Assert.Equal(0, restarted.Requests);
        Assert.Equal(0, fixture.Process.Commands);
        Assert.Equal(original, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
        await fixture.RestoreSourceAsync();
        Assert.Equal("completed", (await fixture.ExecuteAsync(new(AgentSkillsProvider.RunSkillScriptToolName, mixed))).ResponseText);
        Assert.Equal(1, fixture.Process.Commands);
        Assert.Equal(original.IntentId, Assert.Single(fixture.Process.Intents));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approved_skill_keeps_original_Project_lifetime_despite_current_replacement_grant(bool mixed) {
        await using var fixture = await Fixture.CreateAsync(projectScope: true);
        var awaiting = await fixture.ExecuteAsync(new(AgentSkillsProvider.RunSkillScriptToolName, mixed));
        await fixture.Journal.ApproveAsync(awaiting.PendingApprovals);
        var original = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        var originalLifetime = fixture.Project!.LifetimeId;
        await fixture.ReplaceProjectAndGrantAsync();
        Assert.NotEqual(originalLifetime, fixture.Project.LifetimeId);
        Assert.True((await fixture.CurrentAuthorityAsync()).ReadAllowed);
        var restart = new SkillClient(AgentSkillsProvider.RunSkillScriptToolName, mixed);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.ExecuteAsync(restart));
        Assert.Equal(0, restart.Requests);
        Assert.Equal(0, fixture.Process.Commands);
        Assert.Equal(original, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
    }

    public enum SourceChange { Root, Inline, CapabilityIdentity }

    [Fact]
    public async Task SDK_discovery_cannot_move_an_approved_skill_between_locations_under_the_same_configured_root() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.UseBroadDiscoveryRootAsync();
        var awaiting = await fixture.ExecuteAsync(new(AgentSkillsProvider.RunSkillScriptToolName, false));
        await fixture.Journal.ApproveAsync(awaiting.PendingApprovals);
        var original = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        fixture.MoveSelectedSkill(replacement: true);
        var restarted = new SkillClient(AgentSkillsProvider.RunSkillScriptToolName, false);
        var failure = await UsageAdmissionFailureAsync(() => fixture.ExecuteAsync(restarted));
        Assert.Equal("tool-admission.source-preparation-changed", failure.Code);
        Assert.Equal(0, restarted.Requests);
        Assert.Equal(0, fixture.Process.Commands);
        Assert.Equal(original, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
        fixture.MoveSelectedSkill(replacement: false);
        Assert.Equal("completed", (await fixture.ExecuteAsync(new(AgentSkillsProvider.RunSkillScriptToolName, false))).ResponseText);
        Assert.Equal(1, fixture.Process.Commands);
    }

    [Theory]
    [InlineData(AgentSkillsProvider.LoadSkillToolName)]
    [InlineData(AgentSkillsProvider.ReadSkillResourceToolName)]
    public async Task Cached_file_skill_result_rechecks_original_Project_read_and_keeps_evidence_through_revocation(string toolName) {
        await using var fixture = await Fixture.CreateAsync(projectScope: true);
        Assert.Equal("completed", (await fixture.ExecuteAsync(new(toolName, false))).ResponseText);
        var original = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        Assert.NotNull(original.DisclosureEvidence);
        await fixture.SetProjectReadAsync(false);
        var deniedClient = new SkillClient(toolName, false);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.ExecuteAsync(deniedClient));
        Assert.Equal(0, deniedClient.Requests);
        Assert.Equal(original, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
        await fixture.SetProjectReadAsync(true);
        var restored = new SkillClient(toolName, false);
        Assert.Equal("completed", (await fixture.ExecuteAsync(restored)).ResponseText);
        Assert.Equal(0, restored.Requests);
        Assert.Equal(original, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
        Assert.Equal(0, fixture.Process.Commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_previously_attached_skill_cannot_disclose_after_its_canonical_capability_assignment_is_removed(bool mixed) {
        await using var fixture = await Fixture.CreateAsync();
        Assert.Equal("completed", (await fixture.ExecuteAsync(new(AgentSkillsProvider.ReadSkillResourceToolName, mixed))).ResponseText);
        var original = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        await fixture.SetSkillAssignmentAsync(false);
        var retry = new SkillClient(AgentSkillsProvider.ReadSkillResourceToolName, mixed);
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.ExecuteAsync(retry, retainOriginalToolset: true));
        Assert.Equal("skill.result-disclosure-denied", denied.Code);
        Assert.Equal(0, retry.Requests);
        Assert.Equal(original, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
        await fixture.SetSkillAssignmentAsync(true);
        Assert.Equal("completed", (await fixture.ExecuteAsync(new(AgentSkillsProvider.ReadSkillResourceToolName, mixed))).ResponseText);
        Assert.Equal(original, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
        Assert.Equal(mixed ? 1 : 0, fixture.OrdinaryCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Completed_script_observation_requires_read_but_does_not_require_permission_to_run_the_script_again(bool mixed) {
        await using var fixture = await Fixture.CreateAsync(projectScope: true);
        var awaiting = await fixture.ExecuteAsync(new(AgentSkillsProvider.RunSkillScriptToolName, mixed));
        await fixture.Journal.ApproveAsync(awaiting.PendingApprovals);
        var lost = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(new(AgentSkillsProvider.RunSkillScriptToolName,
            mixed, loseFinalAck: true)));
        Assert.Contains(SkillClient.LostAcknowledgement, lost.ToString(), StringComparison.Ordinal);
        var original = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        Assert.Equal(AgentToolProposalState.Completed, original.State);
        Assert.Equal(AgentToolEffectState.Unknown, original.EffectState);
        await fixture.SetScriptExecutionAsync(false);
        var restored = new SkillClient(AgentSkillsProvider.RunSkillScriptToolName, mixed);
        Assert.Equal("completed", (await fixture.ExecuteAsync(restored)).ResponseText);
        Assert.Contains("Retained script output", Assert.Single(restored.Inputs), StringComparison.Ordinal);
        Assert.Equal(1, fixture.Process.Commands);
        Assert.Equal(original, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
    }

    [Theory]
    [InlineData(AgentSkillsProvider.LoadSkillToolName)]
    [InlineData(AgentSkillsProvider.ReadSkillResourceToolName)]
    public async Task Completed_skill_result_does_not_migrate_to_a_recreated_Project_even_with_current_read_grants(string toolName) {
        await using var fixture = await Fixture.CreateAsync(projectScope: true);
        Assert.Equal("completed", (await fixture.ExecuteAsync(new(toolName, false))).ResponseText);
        var original = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        await fixture.ReplaceProjectAndGrantAsync();
        Assert.True((await fixture.CurrentAuthorityAsync()).ReadAllowed);
        var retry = new SkillClient(toolName, false);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.ExecuteAsync(retry));
        Assert.Equal(0, retry.Requests);
        Assert.Equal(original, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
    }

    [Theory]
    [InlineData(AgentSkillsProvider.LoadSkillToolName)]
    [InlineData(AgentSkillsProvider.ReadSkillResourceToolName)]
    public async Task Inline_result_retains_the_actual_capability_and_semantic_configuration_across_restart(string toolName) {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddInlineAsync();
        var first = new SkillClient(toolName, false, skillName: "inline-source", resourceName: "reference");
        Assert.Equal("completed", (await fixture.ExecuteAsync(first)).ResponseText);
        var original = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        var evidence = MafSkillResultEvidence.Read(Assert.IsType<AgentToolProtocolEnvelope>(original.DisclosureEvidence));
        Assert.Equal(MafSkillOriginKind.Inline, evidence.Selected.Origin.Kind);
        Assert.NotNull(evidence.Selected.Origin.CapabilityId);
        await fixture.ChangeSourceAsync(SourceChange.Inline);
        var deniedClient = new SkillClient(toolName, false, skillName: "inline-source", resourceName: "reference");
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.ExecuteAsync(deniedClient));
        Assert.Equal("skill.result-disclosure-denied", denied.Code);
        Assert.Equal(0, deniedClient.Requests);
        Assert.Equal(original, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
        await fixture.RestoreSourceAsync();
        var restored = new SkillClient(toolName, false, skillName: "inline-source", resourceName: "reference");
        Assert.Equal("completed", (await fixture.ExecuteAsync(restored)).ResponseText);
        Assert.Equal(0, restored.Requests);
        Assert.Equal(original, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
    }

    [Fact]
    public async Task Registered_resource_keeps_fresh_use_but_no_remote_ACL_probe_means_explicit_cached_disclosure_unavailability() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddRegisteredAsync();
        var first = new SkillClient(AgentSkillsProvider.ReadSkillResourceToolName, false, skillName: "registered-source", resourceName: "remote");
        Assert.Equal("completed", (await fixture.ExecuteAsync(first)).ResponseText);
        Assert.Equal(1, fixture.Registered.Reads);
        var original = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        Assert.Equal(AgentToolProposalState.Completed, original.State);
        var evidence = MafSkillResultEvidence.Read(Assert.IsType<AgentToolProtocolEnvelope>(original.DisclosureEvidence));
        Assert.Equal(MafSkillResultKind.Unsupported, evidence.Kind);
        var retry = new SkillClient(AgentSkillsProvider.ReadSkillResourceToolName, false, skillName: "registered-source", resourceName: "remote");
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.ExecuteAsync(retry));
        Assert.Equal("skill.result-authority-unavailable", failure.Code);
        Assert.Equal(0, retry.Requests);
        Assert.Equal(1, fixture.Registered.Reads);
        Assert.Equal(original, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
    }

    [Fact]
    public async Task Failure_after_script_return_but_before_evidence_checkpoint_retains_dispatch_uncertainty() {
        await using var fixture = await Fixture.CreateAsync();
        var awaiting = await fixture.ExecuteAsync(new(AgentSkillsProvider.RunSkillScriptToolName, false));
        await fixture.Journal.ApproveAsync(awaiting.PendingApprovals);
        fixture.FailResultCapture = true;
        var dispatchClient = new SkillClient(AgentSkillsProvider.RunSkillScriptToolName, false);
        var failure = await UsageAdmissionFailureAsync(() => fixture.ExecuteAsync(dispatchClient));
        Assert.Equal("tool-admission.reconciliation-required", failure.Code);
        var injected = Assert.IsType<IOException>(fixture.ResultCaptureFailure);
        Assert.Equal("Fixture result evidence checkpoint failed after the concrete tool returned.", injected.Message);
        Assert.Equal(0, dispatchClient.Requests);
        var original = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, original.State);
        Assert.Equal(AgentToolEffectState.Unknown, original.EffectState);
        Assert.Null(original.Result);
        Assert.Equal(1, fixture.Process.Commands);
        fixture.FailResultCapture = false;
        var retry = new SkillClient(AgentSkillsProvider.RunSkillScriptToolName, false);
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.ExecuteAsync(retry));
        Assert.Equal("tool-admission.reconciliation-required", denied.Code);
        Assert.Equal(0, retry.Requests);
        Assert.Equal(1, fixture.Process.Commands);
        Assert.Equal(original, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
    }

    [Fact]
    public async Task Skill_file_in_the_original_audited_execution_scope_can_be_disclosed_but_cannot_borrow_a_later_scope() {
        await using var fixture = await Fixture.CreateAsync(projectScope: true);
        var executionScope = WorkspaceScopeDescriptor.Process(Guid.NewGuid().ToString("N"));
        await fixture.CopySourceToScopeAsync(executionScope);
        AgentToolProposalRecord original;
        using (fixture.BeginExecutionScope(executionScope)) {
            Assert.Equal("completed", (await fixture.ExecuteAsync(new(AgentSkillsProvider.ReadSkillResourceToolName, false))).ResponseText);
            original = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
            var evidence = MafSkillResultEvidence.Read(Assert.IsType<AgentToolProtocolEnvelope>(original.DisclosureEvidence));
            Assert.Equal(executionScope, evidence.ExecutionWorkspaceScope);
            Assert.Equal(MafSkillResultKind.FileResource, evidence.Kind);
            Assert.Equal("completed", (await fixture.ExecuteAsync(new(AgentSkillsProvider.ReadSkillResourceToolName, false))).ResponseText);
        }
        using (fixture.BeginExecutionScope(WorkspaceScopeDescriptor.Process(Guid.NewGuid().ToString("N")))) {
            var deniedClient = new SkillClient(AgentSkillsProvider.ReadSkillResourceToolName, false);
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.ExecuteAsync(deniedClient));
            Assert.Equal(0, deniedClient.Requests);
        }
        Assert.Equal(original, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
    }

    [Fact]
    public async Task Configured_skill_from_an_uncaptured_foreign_managed_scope_keeps_fresh_use_but_cannot_seed_cached_disclosure() {
        await using var fixture = await Fixture.CreateAsync(projectScope: true);
        await fixture.CopySourceToScopeAsync(WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("N")));
        Assert.Equal("completed", (await fixture.ExecuteAsync(new(AgentSkillsProvider.ReadSkillResourceToolName, false))).ResponseText);
        var original = (await fixture.ReadJournalAsync()).Batches[0].Proposals[0];
        var evidence = MafSkillResultEvidence.Read(Assert.IsType<AgentToolProtocolEnvelope>(original.DisclosureEvidence));
        Assert.Equal(MafSkillResultKind.Unsupported, evidence.Kind);
        var deniedClient = new SkillClient(AgentSkillsProvider.ReadSkillResourceToolName, false);
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.ExecuteAsync(deniedClient));
        Assert.Equal("skill.result-authority-unavailable", failure.Code);
        Assert.Equal(0, deniedClient.Requests);
        Assert.Equal(original, (await fixture.ReadJournalAsync()).Batches[0].Proposals[0]);
    }

    private static async Task<AgentToolAdmissionException> UsageAdmissionFailureAsync(Func<Task<AgentRuntimeResponse>> execute) {
        var failure = await Assert.ThrowsAsync<AgentRuntimeUsageException>(execute);
        return Assert.IsType<AgentToolAdmissionException>(failure.InnerException);
    }

    private sealed class Fixture(TestApplication application, AsyncServiceScope scope, AgentToolAdmissionJournalFixture journal,
        AgentDefinition agent, CapabilityCatalogItem capability, ProjectWriteAdmission? project) : IAsyncDisposable {
        private const string OrdinaryTool = "sdk_fixture_read";
        internal AgentToolAdmissionJournalFixture Journal => journal;
        internal ProcessProbe Process { get; } = new();
        internal int OrdinaryCalls { get; private set; }
        internal ProjectWriteAdmission? Project { get; private set; } = project;
        private CapabilityCatalogItem? inlineCapability;
        private CapabilityCatalogItem? originalFile;
        private CapabilityCatalogItem? originalInline;
        private CapabilityCatalogItem? registeredCapability;
        private AgentDefinition? firstActor;
        private CapabilityCatalogItem[]? firstCapabilities;
        internal RegisteredSkill Registered { get; } = new();
        internal bool FailResultCapture { get; set; }
        internal IOException? ResultCaptureFailure { get; private set; }

        internal static async Task<Fixture> CreateAsync(bool projectScope = false) {
            var application = await TestApplication.CreateAsync();
            var scope = application.Services.CreateAsyncScope();
            AgentToolAdmissionJournalFixture? journal = null;
            try {
                var services = scope.ServiceProvider;
                ProjectWriteAdmission? project = null;
                if (projectScope) {
                    var projectId = Guid.NewGuid();
                    Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId,
                        new() { Name = "Original skill source" })).IsSuccess);
                    project = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId);
                    Assert.NotNull(project);
                }
                var profile = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
                var workspace = project is null ? WorkspaceScopeDescriptor.Sandbox : WorkspaceScopeDescriptor.Project(project.ProjectId.ToString("D"));
                journal = await AgentToolAdmissionJournalFixture.CreateAsync(profileBinding:
                    new(profile.ActiveProfileId!.Value, profile.ActiveFingerprint!, new(profile.Generation)),
                    transientContext: new("Original SDK source", workspace), storageScope: workspace,
                    configureAgent: actor => actor with {
                        Id = Guid.NewGuid(), Name = "SDK skill admission fixture", TemplateKey = string.Empty,
                        IsTemplate = false, Workload = AgentWorkloadKind.General, ConfigurationJson = "{}", Capabilities = []
                    });
                var root = Path.Combine(journal.WorkspaceRoot, "skills", "journal-skill");
                Directory.CreateDirectory(Path.Combine(root, "scripts"));
                Directory.CreateDirectory(Path.Combine(root, "references"));
                await File.WriteAllTextAsync(Path.Combine(root, "SKILL.md"), """
                    ---
                    name: journal-skill
                    description: Retained SDK skill fixture.
                    ---
                    Retained instructions. Read references/proof.md and run scripts/proof.ps1.
                    """);
                await File.WriteAllTextAsync(Path.Combine(root, "references", "proof.md"), "Retained resource.");
                await File.WriteAllTextAsync(Path.Combine(root, "scripts", "proof.ps1"), "Write-Output 'Retained script output'");
                var capability = new CapabilityCatalogItem(Guid.NewGuid(), CapabilityKind.Skill, "journal-skill", "Journal skill",
                    "SDK fixture", string.Empty, JsonSerializer.Serialize(new {
                        skillSource = "file", skillRoot = "skills/journal-skill", scriptApproval = true
                    }), CapabilityProofStatus.Verified, string.Empty, DateTimeOffset.UtcNow, true);
                var configuration = AgentWorkspaceToolAccessMetadata.Write("{}", new() {
                    Profile = AgentWorkspaceToolProfileKind.Custom, CanReadFiles = true, CanRunLocalScripts = true
                });
                if (project is not null) {
                    configuration = AgentProjectStructureAccessMetadata.Write(configuration, new() {
                        CanRead = true, AllowedProjectIds = [project.ProjectId],
                        AllowedProjectLifetimes = [new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId)]
                    });
                }
                var agent = journal.Agent with {
                    IsTemplate = false, TemplateKey = string.Empty, Status = AgentLifecycleStatus.Active,
                    ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged, Model = journal.Provider.DefaultModel,
                    Permissions = journal.Agent.Permissions with { CanUseTools = true },
                    ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(configuration),
                    Capabilities = [new(capability.Id, capability.Key, capability.Kind, capability.ProofStatus,
                        capability.LastVerifiedAtUtc, capability.ProofNotes)]
                };
                await journal.Store.UpdateCatalogAsync(current => current with {
                    Agents = current.Agents.Where(item => item.Id != agent.Id).Append(agent).ToArray(),
                    Capabilities = current.Capabilities.Where(item => item.Id != capability.Id).Append(capability).ToArray()
                });
                var catalog = services.GetRequiredService<ISandboxWorkspaceCatalogStore>();
                await catalog.UpdateCatalogAsync(current => current with {
                    Agents = current.Agents.Where(item => item.Id != agent.Id).Append(agent).ToArray(),
                    Capabilities = current.Capabilities.Where(item => item.Id != capability.Id).Append(capability).ToArray()
                });
                var readback = await catalog.LoadCatalogAsync();
                agent = readback.Agents.Single(item => item.Id == agent.Id);
                Assert.Contains(agent.Capabilities, item => item.CapabilityId == capability.Id);
                Assert.Null(await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(journal.Session.ExecutionRunId));
                Assert.NotNull(await journal.NewStore().GetExecutionRunAsync(journal.Session.ExecutionRunId));
                return new(application, scope, journal, agent, capability, project);
            } catch {
                if (journal is not null) {
                    await journal.DisposeAsync();
                }
                await scope.DisposeAsync();
                await application.DisposeAsync();
                throw;
            }
        }

        internal async Task<AgentToolJournalRecord> ReadJournalAsync()
            => (await journal.NewStore().GetExecutionRunAsync(journal.Session.ExecutionRunId))!.ToolAdmission!;

        internal async Task AddInlineAsync() {
            inlineCapability = new(Guid.NewGuid(), CapabilityKind.Skill, "inline-source", "Inline source", "Inline source", string.Empty,
                JsonSerializer.Serialize(new { skillSource = "inline", inlineSkill = new {
                    name = "inline-source", description = "Retained inline source", instructions = "Original inline instructions",
                    resources = new[] { new { name = "reference", content = "Original inline resource" } }
                } }), CapabilityProofStatus.Verified, string.Empty, DateTimeOffset.UtcNow, true);
            await SaveSourcesAsync();
        }

        internal async Task AddRegisteredAsync() {
            registeredCapability = new(Guid.NewGuid(), CapabilityKind.Skill, "registered-source", "Registered source", "Registered source", string.Empty,
                JsonSerializer.Serialize(new { skillSource = "registered", registeredSkillServiceType = typeof(RegisteredSkill).AssemblyQualifiedName }),
                CapabilityProofStatus.Verified, string.Empty, DateTimeOffset.UtcNow, true);
            await SaveSourcesAsync();
        }

        internal IDisposable BeginExecutionScope(WorkspaceScopeDescriptor executionScope)
            => WorkspaceExecutionAuditContext.BeginScope(journal.Detail.Run, journal.StorageScope, executionScope);

        internal async Task CopySourceToScopeAsync(WorkspaceScopeDescriptor targetScope) {
            var relativeRoot = targetScope.CombineArtifactPath("journal-skill");
            var targetRoot = Path.Combine(journal.WorkspaceRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            var originalRoot = Path.Combine(journal.WorkspaceRoot, "skills", "journal-skill");
            Directory.CreateDirectory(Path.Combine(targetRoot, "scripts"));
            Directory.CreateDirectory(Path.Combine(targetRoot, "references"));
            foreach (var relative in new[] { "SKILL.md", Path.Combine("scripts", "proof.ps1"), Path.Combine("references", "proof.md") }) {
                File.Copy(Path.Combine(originalRoot, relative), Path.Combine(targetRoot, relative));
            }
            var configuration = JsonNode.Parse(capability.ConfigurationJson)!.AsObject();
            configuration["skillRoot"] = relativeRoot;
            capability = capability with { ConfigurationJson = configuration.ToJsonString() };
            await SaveSourcesAsync();
        }

        internal async Task SetProjectReadAsync(bool allowed) {
            var catalog = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceCatalogStore>();
            await catalog.UpdateCatalogAsync(current => current with { Agents = current.Agents.Select(item => {
                if (item.Id != agent.Id) {
                    return item;
                }
                var access = AgentProjectStructureAccessMetadata.Read(item.ConfigurationJson);
                var original = AgentProjectStructureAccessMetadata.Read(firstActor!.ConfigurationJson);
                access.CanRead = allowed;
                access.AllowAllProjects = allowed && original.AllowAllProjects;
                access.AllowedProjectIds = allowed ? original.AllowedProjectIds.ToList() : [];
                access.AllowedProjectLifetimes = allowed ? original.AllowedProjectLifetimes.ToList() : [];
                return item with { ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(
                    AgentProjectStructureAccessMetadata.Write(item.ConfigurationJson, access)) };
            }).ToArray() });
            var saved = (await catalog.LoadCatalogAsync()).Agents.Single(item => item.Id == agent.Id);
            var savedAccess = AgentProjectStructureAccessMetadata.Read(saved.ConfigurationJson);
            Assert.Equal(allowed, savedAccess.CanRead);
            var originalAccess = AgentProjectStructureAccessMetadata.Read(firstActor!.ConfigurationJson);
            Assert.Equal(allowed ? originalAccess.AllowedProjectIds : [], savedAccess.AllowedProjectIds);
            Assert.Equal(allowed ? originalAccess.AllowedProjectLifetimes : [], savedAccess.AllowedProjectLifetimes);
        }

        internal async Task SetSkillAssignmentAsync(bool assigned) {
            var catalog = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceCatalogStore>();
            await catalog.UpdateCatalogAsync(current => current with { Agents = current.Agents.Select(item => item.Id != agent.Id ? item : item with {
                ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(item.ConfigurationJson),
                Capabilities = assigned ? firstActor!.Capabilities : item.Capabilities.Where(value => value.CapabilityId != capability.Id).ToArray()
            }).ToArray() });
            var saved = (await catalog.LoadCatalogAsync()).Agents.Single(item => item.Id == agent.Id);
            Assert.Equal(assigned, saved.Capabilities.Any(value => value.CapabilityId == capability.Id));
        }

        internal async Task SetScriptExecutionAsync(bool allowed) {
            var catalog = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceCatalogStore>();
            await catalog.UpdateCatalogAsync(current => current with { Agents = current.Agents.Select(item => {
                if (item.Id != agent.Id) {
                    return item;
                }
                var access = AgentWorkspaceToolAccessMetadata.Read(item.ConfigurationJson);
                access.CanRunLocalScripts = allowed;
                return item with { ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(
                    AgentWorkspaceToolAccessMetadata.Write(item.ConfigurationJson, access)) };
            }).ToArray() });
            var saved = (await catalog.LoadCatalogAsync()).Agents.Single(item => item.Id == agent.Id);
            Assert.Equal(allowed, AgentWorkspaceToolAccessMetadata.Read(saved.ConfigurationJson).CanRunLocalScripts);
        }

        internal async Task UseBroadDiscoveryRootAsync() {
            var configuration = JsonNode.Parse(capability.ConfigurationJson)!.AsObject();
            configuration["skillRoot"] = "skills";
            capability = capability with { ConfigurationJson = configuration.ToJsonString() };
            await SaveSourcesAsync();
        }

        internal void MoveSelectedSkill(bool replacement) {
            var original = Path.Combine(journal.WorkspaceRoot, "skills", "journal-skill");
            var moved = Path.Combine(journal.WorkspaceRoot, "skills", "moved-skill");
            Directory.Move(replacement ? original : moved, replacement ? moved : original);
        }

        internal async Task ChangeSourceAsync(SourceChange change) {
            originalFile = capability;
            originalInline = inlineCapability;
            if (change == SourceChange.Root) {
                var oldRoot = Path.Combine(journal.WorkspaceRoot, "skills", "journal-skill");
                var newRoot = Path.Combine(journal.WorkspaceRoot, "skills", "replacement-skill");
                Directory.CreateDirectory(Path.Combine(newRoot, "scripts"));
                Directory.CreateDirectory(Path.Combine(newRoot, "references"));
                foreach (var relative in new[] { "SKILL.md", Path.Combine("scripts", "proof.ps1"), Path.Combine("references", "proof.md") }) {
                    File.Copy(Path.Combine(oldRoot, relative), Path.Combine(newRoot, relative));
                }
                var configuration = JsonNode.Parse(capability.ConfigurationJson)!.AsObject();
                configuration["skillRoot"] = "skills/replacement-skill";
                capability = capability with { ConfigurationJson = configuration.ToJsonString() };
            } else if (change == SourceChange.Inline) {
                var configuration = JsonNode.Parse(inlineCapability!.ConfigurationJson)!.AsObject();
                configuration["inlineSkill"]!["instructions"] = "Replacement inline instructions";
                inlineCapability = inlineCapability with { ConfigurationJson = configuration.ToJsonString() };
            } else {
                capability = capability with { Id = Guid.NewGuid() };
            }
            await SaveSourcesAsync();
        }

        internal async Task RestoreSourceAsync() {
            capability = originalFile ?? throw new InvalidOperationException("No source mutation was prepared.");
            inlineCapability = originalInline;
            await SaveSourcesAsync();
        }

        internal async Task ReplaceProjectAndGrantAsync() {
            var original = Project!;
            var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
            await projects.DeleteAsync(original.ProjectId);
            Assert.True((await projects.CreateAsync(original.ProjectId, new() { Name = "Unrelated skill source replacement" })).IsSuccess);
            Project = await scope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(original.ProjectId);
            Assert.NotNull(Project);
            Assert.NotEqual(original.LifetimeId, Project.LifetimeId);
            await SaveSourcesAsync();
        }

        internal async Task<AgentExecutionAuthorityRecord> CurrentAuthorityAsync() {
            var original = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(journal.Detail.Run.MetadataJson)!;
            var source = AgentTurnContextMetadata.TryReadTurnContextReference(journal.Detail.Run.MetadataJson)!;
            return await scope.ServiceProvider.GetRequiredService<IAgentExecutionAuthorityResolver>().ResolveAsync(new(agent.Id,
                source.SourceKind, source.SourceId, original.WorkspaceScope, journal.Profile.Generation, UiAccessHint: null));
        }

        private async Task SaveSourcesAsync() {
            var capabilities = new[] { capability, inlineCapability, registeredCapability }.OfType<CapabilityCatalogItem>().ToArray();
            var catalog = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceCatalogStore>();
            var configuration = (await catalog.LoadCatalogAsync()).Agents.Single(item => item.Id == agent.Id).ConfigurationJson;
            if (Project is { } original) {
                configuration = AgentProjectStructureAccessMetadata.Write(configuration, new() {
                    CanRead = true, AllowedProjectIds = [original.ProjectId],
                    AllowedProjectLifetimes = [new(original.DatabaseProfileId, original.ProjectId, original.LifetimeId)]
                });
            }
            agent = agent with {
                ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(configuration),
                Capabilities = capabilities.Select(item => new AgentCapabilityAssignment(item.Id, item.Key, item.Kind,
                    item.ProofStatus, item.LastVerifiedAtUtc, item.ProofNotes)).ToArray()
            };
            await catalog.UpdateCatalogAsync(current => current with {
                Agents = current.Agents.Where(item => item.Id != agent.Id).Append(agent).ToArray(),
                Capabilities = current.Capabilities.Where(item => !capabilities.Any(saved => saved.Key == item.Key)).Concat(capabilities).ToArray()
            });
            var readback = await catalog.LoadCatalogAsync();
            agent = readback.Agents.Single(item => item.Id == agent.Id);
            Assert.Equal(capabilities.Select(item => item.Id).Order(), agent.Capabilities.Select(item => item.CapabilityId).Order());
            foreach (var saved in capabilities) {
                Assert.Equal(saved.ConfigurationJson, readback.Capabilities.Single(item => item.Id == saved.Id).ConfigurationJson);
            }
            if (Project is { } project) {
                Assert.Equal(new AgentProjectStructureLifetime(project.DatabaseProfileId, project.ProjectId, project.LifetimeId),
                    Assert.Single(AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson).AllowedProjectLifetimes));
            }
        }

        internal async Task<AgentRuntimeResponse> ExecuteAsync(SkillClient client, bool retainOriginalToolset = false) {
            var services = scope.ServiceProvider;
            var current = await services.GetRequiredService<ISandboxWorkspaceCatalogStore>().LoadCatalogAsync();
            agent = current.Agents.Single(item => item.Id == agent.Id);
            var assigned = agent.Capabilities.Select(item => item.CapabilityId).ToHashSet();
            var capabilities = current.Capabilities.Where(item => assigned.Contains(item.Id)).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            firstActor ??= agent;
            firstCapabilities ??= capabilities;
            if (retainOriginalToolset) {
                agent = firstActor;
                capabilities = firstCapabilities;
            }
            var dependencies = MafAgentRuntimeDependencies.FromServices(services);
            var activeJournal = journal.NewJournal(journal.NewStore());
            var policies = new AgentToolPolicyCatalog([
                ToolCapabilityMetadataFactory.Read(OrdinaryTool, ToolCapabilitySideEffectKind.InternalDataRead)
            ]);
            var ordinary = new OrdinaryProvider(() => {
                Assert.NotNull(AgentToolInvocationClaim.Current);
                OrdinaryCalls++;
                return "Retained ordinary result";
            }, activeJournal, journal.Session);
            dependencies = dependencies with {
                ProviderAgentFactory = new AgentFactory(client), ToolAdmissionJournal = activeJournal, ToolPolicies = policies,
                RuntimeToolProviderComposer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter(policies), policies),
                WorkspaceRuntimeServicesFactory = new RuntimeServicesFactory(
                    services.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
                    services.GetRequiredService<IWorkspaceDocumentMarkdownConverter>(), Process),
                CapabilityDependencies = dependencies.CapabilityDependencies with {
                    ContextContributors = [], RuntimeToolProviders = client.Mixed ? [ordinary] : [], ToolPolicies = policies,
                    RegisteredCapabilityServices = new RegisteredSource(Registered),
                    WorkspaceToolResultSource = new CompletionProbe(new WorkspaceToolResultSource(activeJournal,
                        services.GetRequiredService<IAgentExecutionAuthorityResolver>(), services.GetRequiredService<IAgentCatalogReadLeaseStore>(),
                        services.GetRequiredService<ICanonicalRuntimeDatabase>(), services.GetRequiredService<IDatabaseRuntimeState>(),
                        services.GetRequiredService<ProjectWriteAdmissionService>()), () => FailResultCapture, failure => {
                            Assert.Null(ResultCaptureFailure);
                            ResultCaptureFailure = failure;
                        })
                }
            };
            var runtime = new MafAgentRuntime(journal.WorkspaceRoot, journal.StorageScope, dependencies);
            var options = new AgentRuntimeExecutionOptions(null, AgentFinalizerMode.Disabled, true, 0) {
                AdmittedToolSession = journal.Session, ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable,
                Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(journal.Detail.Run.MetadataJson),
                AuthorityPolicyFingerprint = "fixture-authority", ModelContextDigest = "fixture-context",
                ContextIntent = AgentRuntimeContextIntent.Empty with {
                    Purpose = AgentRuntimeContextPurpose.InteractiveChat, RuntimeToolProvidersEnabled = client.Mixed,
                    ToolCapabilitiesEnabled = true, WorkspaceToolsEnabled = false, WorkspaceScope = journal.StorageScope
                }
            };
            return await runtime.ExecutionPort.ExecuteAsync(new(agent,
                journal.Provider with { Transport = ProviderTransportKind.ChatCompletions },
                journal.Detail.ChatSession!, capabilities, [], "Use the reviewed skill.", string.Empty,
                (_, _, _) => Task.CompletedTask, ExecutionOptions: options));
        }

        public async ValueTask DisposeAsync() {
            await scope.DisposeAsync();
            await journal.DisposeAsync();
            await application.DisposeAsync();
            Process.AssertAliasCleanup();
        }

        private sealed class OrdinaryProvider(Func<string> invoke, AgentToolAdmissionJournal journal,
            AgentToolSessionReference session) : IAgentRuntimeToolProvider {
            public int Order => 1;
            public AgentRuntimeToolProviderDescriptor Descriptor { get; } = new("sdk-fixture", "SDK fixture", "", [],
                [AgentRuntimeToolProviderPurpose.InteractiveChat]);
            public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(AgentRuntimeToolProviderContext context, CancellationToken cancellationToken)
                => ValueTask.FromResult<IReadOnlyList<AITool>>([AIFunctionFactory.Create(invoke, OrdinaryTool)]);
            public IReadOnlyList<AgentRuntimeToolMetadata> GetToolMetadata(AgentRuntimeToolProviderContext context)
                => [new(Descriptor.ProviderKey, OrdinaryTool, AgentRuntimeToolOperationKind.Read, false) {
                    AuthorizeResultDisclosureAsync = async (disclosure, token) => {
                        await journal.RequireSessionAsync(session, token);
                        Assert.Equal("Retained ordinary result", disclosure.Result.GetString());
                        return null;
                    }
                }];
        }

        internal static string OrdinaryToolName => OrdinaryTool;
    }

    private sealed class CompletionProbe(IAgentWorkspaceToolResultSource inner, Func<bool> fail,
        Action<IOException> captureFailure) : IAgentWorkspaceToolResultSource {
        public ValueTask<AgentToolProtocolEnvelope> CaptureAsync(AgentRuntimeToolProviderContext context, WorkspaceScopeDescriptor scope,
            CancellationToken cancellationToken = default) => inner.CaptureAsync(context, scope, cancellationToken);
        public ValueTask<AgentToolProtocolEnvelope> CompleteAsync(AgentToolProtocolEnvelope original, CancellationToken cancellationToken = default) {
            if (!fail()) {
                return inner.CompleteAsync(original, cancellationToken);
            }
            var failure = new IOException("Fixture result evidence checkpoint failed after the concrete tool returned.");
            captureFailure(failure);
            throw failure;
        }
        public ValueTask<IAgentWorkspaceToolResultReadLease> AcquireReadAsync(AgentRuntimeToolProviderContext context, WorkspaceScopeDescriptor scope,
            AgentToolProtocolEnvelope original, CancellationToken cancellationToken = default)
            => inner.AcquireReadAsync(context, scope, original, cancellationToken);
    }

    private sealed class RegisteredSource(RegisteredSkill skill) : IRegisteredCapabilityServiceSource {
        public object? Resolve(Type serviceType) => serviceType == typeof(RegisteredSkill) ? skill : null;
    }

    private sealed class RegisteredSkill : AgentSkill {
        internal int Reads { get; private set; }
        public override AgentSkillFrontmatter Frontmatter { get; } = new("registered-source", "Registered source");
        public override ValueTask<string> GetContentAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult("Retained registered instructions");
        public override ValueTask<AgentSkillResource?> GetResourceAsync(string name, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<AgentSkillResource?>(name == "remote" ? new Resource(this) : null);
        public override ValueTask<AgentSkillScript?> GetScriptAsync(string name, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<AgentSkillScript?>(null);
        private sealed class Resource(RegisteredSkill owner) : AgentSkillResource("remote") {
            public override Task<object?> ReadAsync(IServiceProvider? services = null, CancellationToken cancellationToken = default) {
                owner.Reads++;
                return Task.FromResult<object?>("Retained remote result without a remote ACL probe");
            }
        }
    }

    private sealed class AgentFactory(SkillClient client) : IMafProviderAgentFactory {
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) {
            Assert.True(frameworkManagedHistory);
            Assert.True(new ProviderProfileService().ResolveFeatureMatrix(provider).SupportsApprovalRequiredAIFunction);
            Assert.NotNull(options.ChatHistoryProvider);
            Assert.Contains(options.AIContextProviders!, item => item is MafSkillsContextProvider);
            Assert.DoesNotContain(options.ChatOptions!.Tools ?? [], tool => tool.Name == client.ToolName);
            Assert.Equal(client.Mixed ? 1 : 0, options.ChatOptions.Tools?.Count ?? 0);
            return new ChatClientAgent(new MafToolAdmissionChatClient(client), options);
        }
    }

    private sealed class RuntimeServicesFactory(IPhysicalFileSystemPathPolicyFactory paths,
        IWorkspaceDocumentMarkdownConverter converter, ProcessProbe process) : IWorkspaceRuntimeServicesFactory {
        public WorkspaceRuntimeServices Create(WorkspaceExecutionScope scope) {
            var targets = new ExternalTargetPathRegistry();
            var commands = new WorkspaceCommandExecutionService(scope.WorkspaceRoot, process, paths, scope.Scope, externalTargetRegistry: targets);
            var images = new WorkspaceImageOperationService(scope.WorkspaceRoot, paths, scope.Scope, targets);
            return new(scope, new WorkspaceFileService(scope.WorkspaceRoot, paths, scope.Scope, targets), commands,
                new WorkspaceArtifactToolService(scope.WorkspaceRoot, commands, converter, paths, scope.Scope, images, targets),
                images, process, targets);
        }
    }

    private sealed class ProcessProbe : IWorkspaceProcessHost {
        private const string SkillScriptToolName = "skill_script_run";
        private const string SkillScriptRecipe = "skill_script_pwsh";
        private const string PathAliasToolName = "workspace_path_alias";
        private const string PathAliasCreateRecipe = "workspace_path_alias_create";
        private const string PathAliasDeleteRecipe = "workspace_path_alias_delete";
        private readonly Dictionary<string, AgentToolBusinessIntentId> activeAliases = [];
        private int active;
        internal int Commands { get; private set; }
        internal int MaximumActive { get; private set; }
        internal bool FailAfterDispatch { get; set; }
        internal IOException? InjectedDispatchFailure { get; private set; }
        internal List<AgentToolBusinessIntentId> Intents { get; } = [];
        internal void AssertAliasCleanup() => Assert.Empty(activeAliases);
        public ExecutionBoundaryDescriptor DescribeBoundary() => new("Fixture process", "Workspace", "None", "None", "Recorded", false,
            "Installed SDK, command preparation and receipts are real; the process transport is recorded.");
        public async Task<WorkspaceProcessExecutionResult> ExecuteAsync(WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default) {
            var claim = Assert.IsType<AgentToolInvocationClaim>(AgentToolInvocationClaim.Current);
            Assert.Equal(AgentSkillsProvider.RunSkillScriptToolName, claim.Proposal.Payload.ToolName);
            if (request.ToolName == PathAliasToolName) {
                Assert.Equal(2, request.Arguments.Count);
                if (request.RecipeId == PathAliasCreateRecipe) {
                    Assert.True(activeAliases.TryAdd(request.Arguments[0], claim.Proposal.IntentId));
                } else {
                    Assert.Equal(PathAliasDeleteRecipe, request.RecipeId);
                    Assert.Equal("/d", request.Arguments[1]);
                    Assert.True(activeAliases.Remove(request.Arguments[0], out var originalIntent));
                    Assert.Equal(claim.Proposal.IntentId, originalIntent);
                }
                var aliasTime = DateTimeOffset.UtcNow;
                return new(true, 0, string.Empty, string.Empty, false, false, aliasTime, aliasTime, false,
                    DescribeBoundary(), string.Empty);
            }
            Assert.Equal(SkillScriptToolName, request.ToolName);
            Assert.Equal(SkillScriptRecipe, request.RecipeId);
            Commands++;
            Intents.Add(claim.Proposal.IntentId);
            active++;
            MaximumActive = Math.Max(MaximumActive, active);
            try {
                await Task.Yield();
                if (FailAfterDispatch) {
                    Assert.Null(InjectedDispatchFailure);
                    InjectedDispatchFailure = new IOException("Fixture process acknowledgement was lost after dispatch.");
                    throw InjectedDispatchFailure;
                }
                var now = DateTimeOffset.UtcNow;
                return new(true, 0, "Retained script output", string.Empty, false, false, now, now, false,
                    DescribeBoundary(), string.Empty);
            } finally {
                active--;
            }
        }
    }

    private sealed class SkillClient(string toolName, bool mixed, bool loseFinalAck = false, int scriptCalls = 1,
        string skillName = "journal-skill", string? resourceName = null) : IChatClient {
        internal const string LostAcknowledgement = "Fixture final acknowledgement was lost after the saved skill result.";
        internal string ToolName => toolName;
        internal bool Mixed => mixed;
        internal int Requests { get; private set; }
        internal List<string> Inputs { get; } = [];
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            Requests++;
            var input = messages.ToArray();
            Inputs.Add(JsonSerializer.Serialize(input, MafToolProtocolCodec.SerializationOptions));
            Assert.Equal(mixed ? 4 : 3, options!.Tools!.Count);
            Assert.Single(options.Tools, tool => tool.Name == toolName);
            if (input.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any()) {
                if (loseFinalAck) {
                    throw new IOException(LostAcknowledgement);
                }
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
            }
            Dictionary<string, object?> arguments = new() { ["skillName"] = skillName };
            if (toolName == AgentSkillsProvider.ReadSkillResourceToolName) {
                arguments["resourceName"] = resourceName ?? "references/proof.md";
            } else if (toolName == AgentSkillsProvider.RunSkillScriptToolName) {
                arguments["scriptName"] = "scripts/proof.ps1";
            }
            List<AIContent> calls = [];
            for (var index = 0; index < scriptCalls; index++) {
                calls.Add(new FunctionCallContent($"sdk-skill-diagnostic-{index}", toolName, arguments));
            }
            if (mixed && toolName != AgentSkillsProvider.RunSkillScriptToolName) {
                calls.Add(new FunctionCallContent("ordinary-diagnostic", Fixture.OrdinaryToolName, new Dictionary<string, object?>()));
            }
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, calls)));
        }
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in response.ToChatResponseUpdates()) {
                yield return update;
            }
        }
        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() { }
    }
}
