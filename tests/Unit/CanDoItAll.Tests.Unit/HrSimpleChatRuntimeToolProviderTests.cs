using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Agents.SimpleChats;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class HrSimpleChatRuntimeToolProviderTests {
    [Fact]
    public async Task New_pack_exposes_seven_independent_capabilities_and_preserves_existing_hr_fourteen() {
        var fixture = new HrSimpleChatTestFixture();
        var tools = await fixture.Provider.CreateToolsAsync(fixture.Context, CancellationToken.None);
        Assert.Equal(7, tools.Count);
        Assert.Equal(14, HrAgentCapabilityKeys.ToolNameToCapabilityKey.Count);
        Assert.Equal(HrSimpleChatToolPolicy.Operations.Select(operation => operation.ToolName), tools.Select(tool => tool.Name));
        Assert.All(fixture.Provider.GetToolMetadata(fixture.Context), metadata => {
            Assert.True(ToolCapabilityRegistry.TryResolve(metadata.ToolName, out var registered));
            Assert.Equal(metadata.RequiresApprovalByDefault, registered.RequiresApprovalByDefault);
        });
        var settings = Assert.Single(fixture.Provider.GetToolMetadata(fixture.Context), metadata =>
            metadata.ToolName == HrSimpleChatToolPolicy.Get(HrSimpleChatOperation.Settings).ToolName);
        Assert.Equal(AgentRuntimeToolOperationKind.Read, settings.OperationKind);
        Assert.True(settings.RequiresApprovalByDefault);
        Assert.All(HrSimpleChatToolPolicy.PrivilegedKeys, key => Assert.Contains(key, ManagedAgentPrivilegedCapabilityKeys.All));
    }

    [Theory]
    [InlineData(AgentRuntimeToolProviderPurpose.GovernedProcessAutomation)]
    [InlineData(AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive)]
    [InlineData(AgentRuntimeToolProviderPurpose.A2AEndpoint)]
    public async Task Pack_never_attaches_outside_interactive_chat(AgentRuntimeToolProviderPurpose purpose) {
        var fixture = new HrSimpleChatTestFixture();
        Assert.Empty(await fixture.Provider.CreateToolsAsync(fixture.Context with { Purpose = purpose }, CancellationToken.None));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Identity_requires_both_managed_id_and_template_key(bool changeId) {
        var fixture = new HrSimpleChatTestFixture();
        var spoof = changeId ? fixture.Agent with { Id = Guid.NewGuid() } : fixture.Agent with { TemplateKey = "spoof" };
        Assert.Empty(await fixture.Provider.CreateToolsAsync(fixture.Context with { Agent = spoof }, CancellationToken.None));
    }

    [Fact]
    public async Task Missing_durable_session_binding_hides_the_pack() {
        var fixture = new HrSimpleChatTestFixture();
        Assert.Empty(await fixture.Provider.CreateToolsAsync(fixture.Context with { AdmittedToolSession = null }, CancellationToken.None));
        Assert.Empty(await fixture.Provider.CreateToolsAsync(fixture.Context with { Governance = null }, CancellationToken.None));
    }

    [Fact]
    public async Task Summary_capability_does_not_grant_settings_or_mutation() {
        var fixture = new HrSimpleChatTestFixture();
        var search = HrSimpleChatToolPolicy.Get(HrSimpleChatOperation.Search);
        var summaryOnly = fixture.Agent with { Capabilities = fixture.Agent.Capabilities.Where(item => item.CapabilityKey == search.CapabilityKey).ToArray() };
        var tool = Assert.Single(await fixture.Provider.CreateToolsAsync(fixture.Context with { Agent = summaryOnly }, CancellationToken.None));
        Assert.Equal(search.ToolName, tool.Name);
    }

    [Fact]
    public void Duplicate_or_mismatched_catalog_assignments_do_not_authorize() {
        var fixture = new HrSimpleChatTestFixture();
        var operation = HrSimpleChatToolPolicy.Get(HrSimpleChatOperation.Create);
        var assignment = fixture.Agent.Capabilities.Single(item => item.CapabilityKey == operation.CapabilityKey);
        var duplicate = fixture.Agent with { Capabilities = [.. fixture.Agent.Capabilities, assignment with { Kind = CapabilityKind.Skill }] };
        Assert.False(HrSimpleChatRuntimeAuthorization.IsAssigned(duplicate, fixture.Capabilities, operation));
        var mismatched = fixture.Capabilities.Select(item => item.Key == operation.CapabilityKey ? item with { Id = Guid.NewGuid() } : item).ToArray();
        Assert.False(HrSimpleChatRuntimeAuthorization.IsAssigned(fixture.Agent, mismatched, operation));
    }

    [Fact]
    public void Composition_rejects_missing_durable_admission_verifier() {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddHrSimpleChatDefinitionTools());
    }

    [Fact]
    public async Task Invocation_rechecks_current_capability_assignment() {
        var fixture = new HrSimpleChatTestFixture();
        fixture.Agent = fixture.Agent with { Capabilities = [] };
        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() => fixture.Service.SearchAsync(fixture.Context, new(), CancellationToken.None));
        Assert.True(fixture.LeaseFactory.Active!.Disposed);
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("generation")]
    [InlineData("fingerprint")]
    public async Task Invocation_requires_exact_persisted_profile_binding(string difference) {
        var fixture = new HrSimpleChatTestFixture();
        var profile = fixture.Session.Profile;
        fixture.Session = fixture.Session with { Profile = new(
            difference == "profile" ? Guid.NewGuid() : profile.ProfileId,
            difference == "fingerprint" ? "different" : profile.Fingerprint,
            difference == "generation" ? new(profile.Generation.Value + 1) : profile.Generation) };
        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() => fixture.Service.SearchAsync(fixture.Context, new(), CancellationToken.None));
        Assert.Null(fixture.Definitions.ObservedProfile);
    }

    [Fact]
    public async Task Autoapproval_flag_cannot_create_without_durable_proposal() {
        var fixture = new HrSimpleChatTestFixture();
        var context = fixture.Context with { SuppressApprovalRequirements = true };
        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() => fixture.Service.CreateAsync(context,
            HrSimpleChatTestFixture.CreateCommand(), CancellationToken.None));
        Assert.Equal(0, fixture.Receipts.Calls);
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("approval")]
    [InlineData("session")]
    [InlineData("decision")]
    public async Task Create_denies_changed_admission_or_approval_before_owner_dispatch(string difference) {
        var fixture = new HrSimpleChatTestFixture();
        var command = HrSimpleChatTestFixture.CreateCommand();
        fixture.Approve(fixture.Codec.PrepareCreate(command));
        var admitted = fixture.Admissions.Invocation!;
        var changed = fixture.Codec.PrepareCreate(command with { SystemPrompt = "different" });
        fixture.Admissions.Invocation = difference switch {
            "payload" => admitted with { Payload = changed },
            "approval" => admitted with { ApprovedDigest = changed.Digest },
            "session" => admitted with { Session = new(Guid.NewGuid(), fixture.Session.Reference.ChatSessionId, fixture.Authority.AuthorityId) },
            "decision" => admitted with { ApprovalStatus = ExecutionApprovalStatus.Pending },
            _ => throw new ArgumentOutOfRangeException(nameof(difference))
        };
        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() => fixture.Service.CreateAsync(fixture.Context, command, CancellationToken.None));
        Assert.Equal(0, fixture.Receipts.Calls);
    }

    [Fact]
    public async Task Search_returns_only_safe_summary_and_owner_profile_is_held() {
        var fixture = new HrSimpleChatTestFixture();
        var result = await fixture.Service.SearchAsync(fixture.Context, new(), CancellationToken.None);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("private system prompt", json);
        Assert.DoesNotContain("ModelParameterConfigurationJson", json);
        Assert.DoesNotContain("SchemaJson", json);
        Assert.Equal(fixture.Profile, fixture.Definitions.ObservedProfile);
        Assert.True(fixture.LeaseFactory.Active!.Disposed);
        Assert.Null(fixture.Scope.Current);
    }

    [Fact]
    public async Task Settings_discloses_only_the_exact_approved_revision() {
        var fixture = new HrSimpleChatTestFixture();
        var expected = new HrSimpleChatDefinitionVersion(HrSimpleChatTestFixture.DefinitionId, 1, 0);
        fixture.Approve(fixture.Codec.PrepareSettings(expected));
        var result = await fixture.Service.SettingsAsync(fixture.Context, expected, CancellationToken.None);
        Assert.Equal("private system prompt", result.Settings.SystemPrompt);
        Assert.Equal(expected, result.Summary.Version);
    }

    [Fact]
    public async Task Blanket_autoapproval_does_not_authorize_sensitive_settings_disclosure() {
        var fixture = new HrSimpleChatTestFixture();
        var context = fixture.Context with { SuppressApprovalRequirements = true };
        var expected = new HrSimpleChatDefinitionVersion(HrSimpleChatTestFixture.DefinitionId, 1, 0);
        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() => fixture.Service.SettingsAsync(context, expected, CancellationToken.None));
        Assert.Equal(0, fixture.Definitions.Reads);
    }

    [Fact]
    public async Task Settings_rejects_a_concurrent_human_revision() {
        var fixture = new HrSimpleChatTestFixture();
        var expected = new HrSimpleChatDefinitionVersion(HrSimpleChatTestFixture.DefinitionId, 1, 0);
        fixture.Approve(fixture.Codec.PrepareSettings(expected));
        fixture.Definitions.Current = HrSimpleChatTestFixture.Details(2, 1);
        var failure = await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() => fixture.Service.SettingsAsync(fixture.Context, expected, CancellationToken.None));
        Assert.Equal("hr-simple-chat.revision-conflict", failure.Code);
        Assert.False(failure.CanRetryWithCorrectedInput);
    }

    [Fact]
    public async Task Settings_does_not_disclose_after_capability_revocation_during_owner_read() {
        var fixture = new HrSimpleChatTestFixture();
        var expected = new HrSimpleChatDefinitionVersion(HrSimpleChatTestFixture.DefinitionId, 1, 0);
        fixture.Approve(fixture.Codec.PrepareSettings(expected));
        fixture.Definitions.ReadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Definitions.ReadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = fixture.Service.SettingsAsync(fixture.Context, expected, CancellationToken.None);
        await fixture.Definitions.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Agent = fixture.Agent with { Capabilities = [] };
        fixture.Definitions.ReadGate.SetResult();
        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() => read);
        Assert.True(fixture.LeaseFactory.Active!.Disposed);
    }

    [Fact]
    public async Task Settings_does_not_disclose_after_profile_switch_during_owner_read() {
        var fixture = new HrSimpleChatTestFixture();
        var expected = new HrSimpleChatDefinitionVersion(HrSimpleChatTestFixture.DefinitionId, 1, 0);
        fixture.Approve(fixture.Codec.PrepareSettings(expected));
        fixture.Definitions.OnRead = () => fixture.LeaseFactory.Active!.Current = false;
        await Assert.ThrowsAsync<LlmChatRuntimeProfileChangedException>(() => fixture.Service.SettingsAsync(fixture.Context, expected, CancellationToken.None));
        Assert.True(fixture.LeaseFactory.Active!.Disposed);
    }

    [Fact]
    public async Task Update_passes_the_approved_payload_and_expected_owner_token() {
        var fixture = new HrSimpleChatTestFixture();
        var expected = new HrSimpleChatDefinitionVersion(HrSimpleChatTestFixture.DefinitionId, 1, 0);
        var request = new HrSimpleChatUpdateRequest(expected, HrSimpleChatTestFixture.CreateCommand() with { SystemPrompt = "reviewed replacement" });
        fixture.Approve(fixture.Codec.PrepareUpdate(request));
        await fixture.Service.UpdateAsync(fixture.Context, request, CancellationToken.None);
        Assert.Equal("reviewed replacement", fixture.Definitions.Update!.SystemPrompt);
        Assert.Equal(expected.ConcurrencyToken, fixture.Definitions.Update.ExpectedConcurrencyToken);
        Assert.Equal(expected.DefinitionId, fixture.Definitions.Update.DefinitionId.Value);
    }

    [Fact]
    public async Task Stale_update_never_rebases_or_writes() {
        var fixture = new HrSimpleChatTestFixture();
        var request = new HrSimpleChatUpdateRequest(new(HrSimpleChatTestFixture.DefinitionId, 1, 0), HrSimpleChatTestFixture.CreateCommand());
        fixture.Approve(fixture.Codec.PrepareUpdate(request));
        fixture.Definitions.Current = HrSimpleChatTestFixture.Details(2, 1);
        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() => fixture.Service.UpdateAsync(fixture.Context, request, CancellationToken.None));
        Assert.Equal(0, fixture.Definitions.Writes);
    }

    [Fact]
    public async Task Status_change_uses_the_exact_approved_target_token_and_status() {
        var fixture = new HrSimpleChatTestFixture();
        var request = new HrSimpleChatStatusRequest(new(HrSimpleChatTestFixture.DefinitionId, 1, 0), LlmChatDefinitionStatus.Archived);
        fixture.Approve(fixture.Codec.PrepareStatus(request));
        var result = await fixture.Service.ChangeStatusAsync(fixture.Context, request, CancellationToken.None);
        Assert.Equal(request.Expected.DefinitionId, fixture.Definitions.Status!.DefinitionId.Value);
        Assert.Equal(request.Expected.ConcurrencyToken, fixture.Definitions.Status.ExpectedConcurrencyToken);
        Assert.Equal(request.Status, fixture.Definitions.Status.Status);
        Assert.DoesNotContain("private system prompt", JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task Stale_status_change_never_rebases_or_writes() {
        var fixture = new HrSimpleChatTestFixture();
        var request = new HrSimpleChatStatusRequest(new(HrSimpleChatTestFixture.DefinitionId, 1, 0), LlmChatDefinitionStatus.Archived);
        fixture.Approve(fixture.Codec.PrepareStatus(request));
        fixture.Definitions.Current = HrSimpleChatTestFixture.Details(2, 1);
        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() => fixture.Service.ChangeStatusAsync(fixture.Context, request, CancellationToken.None));
        Assert.Equal(0, fixture.Definitions.Writes);
    }

    [Fact]
    public async Task Create_uses_the_admitted_business_intent_and_only_the_atomic_owner_writer() {
        var fixture = new HrSimpleChatTestFixture();
        var command = HrSimpleChatTestFixture.CreateCommand();
        var intentId = Guid.NewGuid();
        fixture.Approve(fixture.Codec.PrepareCreate(command), intentId);
        var context = fixture.Context with { RuntimeSessionKey = "changed-transport-key", Tags = new Dictionary<string, string> { ["callId"] = "untrusted" } };
        var result = await fixture.Service.CreateAsync(context, command, CancellationToken.None);
        Assert.Equal(intentId, result.Receipt.IntentId);
        Assert.Equal(intentId, fixture.Receipts.Command!.Key.IntentId.Value);
        Assert.Equal(HrSimpleChatToolPolicy.CreateProducer, fixture.Receipts.Command.Key.Scope.Producer);
        Assert.Equal(HrAgentIdentity.AgentId.ToString("N"), fixture.Receipts.Command.Key.Scope.Actor);
        Assert.Equal($"agent-chat:{fixture.Session.Reference.ChatSessionId:N}", fixture.Receipts.Command.Key.Scope.HistoryNamespace);
        Assert.Equal(fixture.Profile, fixture.Receipts.ObservedProfile);
        Assert.Equal(1, fixture.Receipts.Calls);
    }

    [Fact]
    public async Task Owner_commit_acknowledgement_failure_propagates_without_a_second_dispatch() {
        var fixture = new HrSimpleChatTestFixture();
        var command = HrSimpleChatTestFixture.CreateCommand();
        fixture.Approve(fixture.Codec.PrepareCreate(command));
        var lostAcknowledgement = new ArgumentException("fixture lost acknowledgement");
        fixture.Receipts.Failure = lostAcknowledgement;
        var failure = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CreateAsync(fixture.Context, command, CancellationToken.None));
        Assert.Same(lostAcknowledgement, failure);
        Assert.Equal(1, fixture.Receipts.Calls);
        Assert.True(fixture.LeaseFactory.Active!.Disposed);
    }

    [Fact]
    public async Task Postcommit_profile_change_preserves_committed_effect_evidence() {
        var fixture = new HrSimpleChatTestFixture();
        var command = HrSimpleChatTestFixture.CreateCommand();
        fixture.Approve(fixture.Codec.PrepareCreate(command));
        fixture.Receipts.AfterCommit = () => fixture.LeaseFactory.Active!.Current = false;
        using var effect = AgentToolInvocationEffectScope.Begin();
        await Assert.ThrowsAsync<LlmChatRuntimeProfileChangedException>(() => fixture.Service.CreateAsync(fixture.Context, command, CancellationToken.None));
        Assert.Equal(HrSimpleChatTestFixture.DefinitionId.ToString("D"), effect.CommittedEffect!.SourceId);
        Assert.Equal(1, fixture.Receipts.Calls);
    }

    [Fact]
    public async Task Receipt_lookup_is_scoped_to_the_verified_chat_and_returns_nullable_identity() {
        var fixture = new HrSimpleChatTestFixture();
        var intentId = Guid.NewGuid();
        Assert.Null(await fixture.Service.FindReceiptAsync(fixture.Context, new(intentId), CancellationToken.None));
        Assert.Equal(intentId, fixture.Receipts.LookupKey!.IntentId.Value);
        Assert.Equal($"agent-chat:{fixture.Session.Reference.ChatSessionId:N}", fixture.Receipts.LookupKey.Scope.HistoryNamespace);
        Assert.Equal(0, fixture.Receipts.Calls);
    }
}
