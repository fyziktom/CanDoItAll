using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure;
using CanDoItAll.Tests.Support;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafSkillResultDisclosureTests {
    [Fact]
    public async Task File_skill_disclosure_preserves_native_filename_separators() {
        var workspaceRoot = TestFileSystem.CreateTemporaryRoot("skill-result-native-path");
        try {
            var directoryName = Path.GetInvalidFileNameChars().Contains('\\')
                ? "file-skill"
                : @"artifacts\scopes\project\unrelated\skill";
            var skillRoot = Path.Combine(workspaceRoot, directoryName);
            Directory.CreateDirectory(skillRoot);
            await File.WriteAllTextAsync(Path.Combine(skillRoot, "SKILL.md"), "Synthetic retained skill content.");
            var fixture = new Fixture(workspaceRoot: workspaceRoot, fileSkillRoot: skillRoot);

            await using var held = await fixture.Owner.AuthorizeAsync(fixture.Disclosure, default);

            Assert.NotNull(held);
            Assert.Equal(1, fixture.Source.Reads);
            Assert.Equal(1, fixture.Source.Active);
        } finally {
            TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
        }
    }

    [Fact]
    public void Read_configuration_keeps_source_and_read_authority_without_requiring_script_execution() {
        var access = new AgentWorkspaceToolAccessSettings { CanReadFiles = true, CanRunLocalScripts = true };
        var configuration = new AgentRuntimeConfiguration();
        var original = MafSkillResultDisclosure.ConfigurationDigest(configuration, [], [], access);
        access.CanRunLocalScripts = false;
        Assert.Equal(original, MafSkillResultDisclosure.ConfigurationDigest(configuration, [], [], access));
        access.CanReadFiles = false;
        Assert.NotEqual(original, MafSkillResultDisclosure.ConfigurationDigest(configuration, [], [], access));
        access.CanReadFiles = true;
        configuration.PreferredSkillRoots = ["different-root"];
        Assert.NotEqual(original, MafSkillResultDisclosure.ConfigurationDigest(configuration, [], [], access));
    }

    [Fact]
    public void File_disclosure_uses_only_original_context_and_execution_scopes() {
        var context = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("N"));
        var execution = WorkspaceScopeDescriptor.Process(Guid.NewGuid().ToString("N"));
        var unrelated = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("N"));
        Assert.True(MafSkillResultDisclosure.IsWithinSourceScopes(context.CombineArtifactPath("skill", "SKILL.md"), context, execution));
        Assert.True(MafSkillResultDisclosure.IsWithinSourceScopes(execution.CombineArtifactPath("skill", "SKILL.md"), context, execution));
        Assert.False(MafSkillResultDisclosure.IsWithinSourceScopes(unrelated.CombineArtifactPath("skill", "SKILL.md"), context, execution));
        Assert.False(MafSkillResultDisclosure.IsWithinSourceScopes(execution.CombineArtifactPath("skill", "SKILL.md"), context, null));
    }

    [Fact]
    public async Task Exact_inline_result_holds_the_current_owner_snapshot_until_disclosure_finishes() {
        var fixture = new Fixture();
        var evidence = fixture.Result.Write();
        Assert.Equal(fixture.Result, MafSkillResultEvidence.Read(evidence));
        var held = await fixture.Owner.AuthorizeAsync(fixture.Disclosure, default);
        Assert.NotNull(held);
        Assert.Equal(1, fixture.Source.Active);
        Assert.Equal(1, fixture.Source.Reads);
        await held.DisposeAsync();
        Assert.Equal(0, fixture.Source.Active);
        Assert.Equal(evidence, fixture.Disclosure.Evidence);
    }

    [Theory]
    [InlineData(DisclosureMismatch.Intent)]
    [InlineData(DisclosureMismatch.Digest)]
    [InlineData(DisclosureMismatch.Source)]
    [InlineData(DisclosureMismatch.SelectedCapability)]
    public async Task Same_name_and_schema_cannot_substitute_another_intent_payload_or_source(DisclosureMismatch mismatch) {
        var fixture = new Fixture();
        var changed = mismatch switch {
            DisclosureMismatch.Intent => fixture.Result with { IntentId = new(Guid.NewGuid()) },
            DisclosureMismatch.Digest => fixture.Result with { PreparedDigest = Hash("another-proposal") },
            DisclosureMismatch.Source => fixture.Result with { Source = AgentToolProtocolEnvelope.Create("another-source", 1, "{}") },
            DisclosureMismatch.SelectedCapability => fixture.Result with { Selected = fixture.Result.Selected with {
                Origin = fixture.Result.Selected.Origin with { CapabilityId = Guid.NewGuid() }
            } },
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch))
        };
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(async () =>
            await fixture.Owner.AuthorizeAsync(fixture.Disclosure with { Evidence = changed.Write() }, default));
        Assert.Equal("skill.result-authority-unavailable", failure.Code);
        Assert.Equal(0, fixture.Source.Reads);
    }

    [Fact]
    public async Task Revoked_read_configuration_denies_without_discarding_evidence_and_restoration_reuses_it() {
        var fixture = new Fixture();
        var original = fixture.Disclosure;
        fixture.CurrentConfiguration = Hash("revoked");
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(async () =>
            await fixture.Owner.AuthorizeAsync(original, default));
        Assert.Equal("skill.result-disclosure-denied", denied.Code);
        Assert.Equal(0, fixture.Source.Active);
        fixture.CurrentConfiguration = fixture.Result.ReadConfiguration;
        await using var restored = await fixture.Owner.AuthorizeAsync(original, default);
        Assert.NotNull(restored);
        Assert.Equal(original.Evidence, fixture.Disclosure.Evidence);
        Assert.Equal(2, fixture.Source.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_registered_owner_evidence_is_explicitly_unavailable(bool registered) {
        var fixture = new Fixture(registered);
        var disclosure = registered ? fixture.Disclosure : fixture.Disclosure with { Evidence = null };
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(async () =>
            await fixture.Owner.AuthorizeAsync(disclosure, default));
        Assert.Equal("skill.result-authority-unavailable", denied.Code);
        Assert.Equal(0, fixture.Source.Reads);
    }

    [Theory]
    [InlineData(AgentToolProposalState.Prepared)]
    [InlineData(AgentToolProposalState.Executing)]
    [InlineData(AgentToolProposalState.ReconciliationRequired)]
    [InlineData(AgentToolProposalState.Rejected)]
    public void A_different_active_batch_cannot_inherit_an_identical_completed_call_source(AgentToolProposalState state) {
        var fixture = new Fixture();
        var original = Proposal(fixture, AgentToolProposalState.Completed);
        var next = original with { IntentId = new(Guid.NewGuid()), State = state };
        var first = Batch(original, 0);
        var second = Batch(next, 1);
        var segment = new AgentToolInvocationSegment(Guid.NewGuid(), 0, 0, Protocol(), PendingApprovals: []);
        var saved = Journal(fixture, [segment], [first, second]);
        var call = new FunctionCallContent(original.CallId, original.Payload.ToolName,
            new Dictionary<string, object?> { ["skillName"] = "inline-source" });
        Assert.Same(original, MafToolRunContext.FindCompletedContextProposal(saved, segment, first.Id, call));
        Assert.Null(MafToolRunContext.FindCompletedContextProposal(saved, segment, second.Id, call));
        Assert.Null(MafToolRunContext.FindCompletedContextProposal(saved, segment, new(Guid.NewGuid()), call));
    }

    [Fact]
    public void Approval_replay_uses_only_the_exact_saved_binding_and_does_not_search_by_name() {
        var fixture = new Fixture();
        var first = Proposal(fixture, AgentToolProposalState.Completed);
        var second = first with { IntentId = new(Guid.NewGuid()), Ordinal = 1, CallId = "other-diagnostic" };
        var batch = new AgentToolBatchRecord(new(Guid.NewGuid()), 0, Hash("request"), Protocol(), [first, second]);
        var approval = new PendingToolApprovalRecord("approval", second.CallId, second.Payload.ToolName, "function", "", second.Payload.ArgumentsJson) {
            ToolAdmission = new(batch.Id, second.IntentId, second.Payload.SemanticVersion, second.Payload.Digest)
        };
        var previous = new AgentToolInvocationSegment(Guid.NewGuid(), 0, 0, Protocol(), ApprovalCheckpoint: Protocol(), PendingApprovals: [approval]);
        var current = new AgentToolInvocationSegment(Guid.NewGuid(), 1, 1, Protocol(), previous.Id, PendingApprovals: []);
        var saved = Journal(fixture, [previous, current], [batch]);
        Assert.Same(second, MafToolRunContext.FindCompletedContextProposal(saved, current, null,
            new(second.CallId, second.Payload.ToolName, new Dictionary<string, object?>())));
        Assert.Null(MafToolRunContext.FindCompletedContextProposal(saved, current, null,
            new(first.CallId, first.Payload.ToolName, new Dictionary<string, object?>())));
    }

    public enum DisclosureMismatch { Intent, Digest, Source, SelectedCapability }

    private static AgentToolProposalRecord Proposal(Fixture fixture, AgentToolProposalState state)
        => new(fixture.Result.IntentId, 0, "diagnostic", fixture.Payload, false, state, ExecutionApprovalStatus.Approved,
            fixture.Payload.Digest, DispatchClaimId: Guid.NewGuid(), Result: Protocol());
    private static AgentToolBatchRecord Batch(AgentToolProposalRecord proposal, int ordinal)
        => new(new(Guid.NewGuid()), ordinal, Hash("request"), Protocol(), [proposal]);
    private static AgentToolJournalRecord Journal(Fixture fixture, ImmutableArray<AgentToolInvocationSegment> segments,
        ImmutableArray<AgentToolBatchRecord> batches)
        => new(1, 1, new(new(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid())), fixture.Actor.Id,
            AgentRuntimeContextPurpose.InteractiveChat, new(Guid.NewGuid(), "fixture", new(1))), segments, batches);
    private static AgentToolProtocolEnvelope Protocol() => AgentToolProtocolEnvelope.Create("fixture", 1, "{}");
    private static AgentToolSemanticDigest Hash(string value) => AgentToolProtocolEnvelope.ComputeDigest(value);

    private sealed class Fixture {
        internal AgentDefinition Actor { get; }
        internal ReadSource Source { get; }
        internal MafSkillResultDisclosure Owner { get; }
        internal AgentToolPreparedPayload Payload { get; }
        internal MafSkillResultEvidence Result { get; }
        internal AgentToolSemanticDigest CurrentConfiguration { get; set; } = Hash("configuration");
        internal AgentToolResultDisclosure Disclosure => new(Result.IntentId, Payload, AgentToolEffectState.None,
            JsonSerializer.SerializeToElement("Retained inline content"), Result.Write());

        internal Fixture(bool registered = false, string? workspaceRoot = null, string? fileSkillRoot = null) {
            workspaceRoot ??= Path.GetTempPath();
            var catalog = SandboxWorkspaceSeedFactory.Create().ToCatalog();
            Actor = catalog.Agents.First(item => item.ProviderProfileId.HasValue);
            Source = new(Actor);
            var context = new AgentRuntimeToolProviderContext(Actor, catalog.Providers.Single(item => item.Id == Actor.ProviderProfileId), [],
                false, AgentRuntimeToolProviderPurpose.InteractiveChat, "fixture", AgentRuntimeContextIntent.Empty, new Dictionary<string, string>()) {
                AdmittedToolSession = new(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid())),
                ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable
            };
            var origin = fileSkillRoot is null
                ? new MafSkillOrigin(registered ? MafSkillOriginKind.Registered : MafSkillOriginKind.Inline,
                    Guid.NewGuid(), string.Empty, string.Empty, registered ? "fixture-registered-owner" : string.Empty)
                : new MafSkillOrigin(MafSkillOriginKind.File, null, workspaceRoot, fileSkillRoot, string.Empty);
            var selected = new MafSkillSourceEntry(fileSkillRoot is null ? "inline-source" : "file-source", origin);
            var source = new MafSkillSourceEvidence(Protocol(), workspaceRoot, Hash("source"), Hash("candidates"), selected.Name, [selected]);
            var arguments = JsonSerializer.Serialize(new { skillName = selected.Name });
            Payload = MafContextToolSourceContract.Bind(new(AgentSkillsProvider.LoadSkillToolName, 1, Hash(arguments), arguments,
                AgentToolProposalEffect.Read, AgentToolProposalRecovery.RevalidateAndRead), source.Write());
            Result = new(new(Guid.NewGuid()), Payload.Digest, Protocol(), CurrentConfiguration, selected, MafSkillResultKind.Content, "", "");
            Owner = new(Source, context, workspaceRoot, WorkspaceScopeDescriptor.Sandbox, CurrentConfiguration,
                _ => CurrentConfiguration, new PhysicalFileSystemPathPolicyFactory());
        }
    }

    private sealed class ReadSource(AgentDefinition actor) : IAgentWorkspaceToolResultSource {
        internal int Reads { get; private set; }
        internal int Active { get; private set; }
        public ValueTask<AgentToolProtocolEnvelope> CaptureAsync(AgentRuntimeToolProviderContext context, WorkspaceScopeDescriptor scope,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Disclosure must not capture a new source.");
        public ValueTask<AgentToolProtocolEnvelope> CompleteAsync(AgentToolProtocolEnvelope original, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Disclosure must not complete another effect.");
        public ValueTask<IAgentWorkspaceToolResultReadLease> AcquireReadAsync(AgentRuntimeToolProviderContext context, WorkspaceScopeDescriptor scope,
            AgentToolProtocolEnvelope original, CancellationToken cancellationToken = default) {
            Reads++;
            Active++;
            return ValueTask.FromResult<IAgentWorkspaceToolResultReadLease>(new Lease(this, actor));
        }
        private sealed class Lease(ReadSource owner, AgentDefinition actor) : IAgentWorkspaceToolResultReadLease {
            public AgentDefinition Agent => actor;
            public ImmutableArray<CapabilityCatalogItem> Capabilities => [];
            public void RequireCurrent() => Assert.Equal(1, owner.Active);
            public ValueTask DisposeAsync() {
                owner.Active--;
                return ValueTask.CompletedTask;
            }
        }
    }
}
