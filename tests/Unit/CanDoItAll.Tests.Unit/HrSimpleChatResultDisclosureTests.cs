using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Agents.SimpleChats;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class HrSimpleChatResultDisclosureTests {
    [Fact]
    public async Task Original_receipt_disclosure_requires_current_read_and_receipt_access_without_new_mutation_authority() {
        var fixture = CreateFixture();
        var disclosure = await CreateAsync(fixture);
        fixture.CurrentAuthority = Current(fixture, read: true, mutation: false);
        var acquire = Callback(fixture, HrSimpleChatOperation.Create);
        await using (var authorization = await acquire(disclosure, default)) {
            Assert.False(fixture.LeaseFactory.Active!.Disposed);
            Assert.Equal(fixture.Profile, fixture.Receipts.ObservedProfile);
        }
        Assert.True(fixture.LeaseFactory.Active!.Disposed);
        Assert.Equal(1, fixture.Receipts.Lookups);
        Assert.Equal(1, fixture.Receipts.Calls);
        Assert.All(fixture.AuthorityResolver.Requests, request => {
            Assert.Equal(fixture.Source.SourceKind, request.SourceKind);
            Assert.Equal(fixture.Source.SourceId, request.SourceId);
            Assert.Equal(fixture.Authority.WorkspaceScope, request.ObservedWorkspaceScope);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Repeated_disclosure_rechecks_revoked_read_or_receipt_capability_and_preserves_original_evidence(bool revokeRead) {
        var fixture = CreateFixture();
        var disclosure = await CreateAsync(fixture);
        var receipt = fixture.Receipts.SavedReceipt;
        var acquire = Callback(fixture, HrSimpleChatOperation.Create);
        await using (await acquire(disclosure, default)) { }
        var originalActor = fixture.Agent;
        if (revokeRead) {
            fixture.CurrentAuthority = Current(fixture, read: false, mutation: false);
        } else {
            var key = HrSimpleChatToolPolicy.Get(HrSimpleChatOperation.Receipt).CapabilityKey;
            fixture.Agent = fixture.Agent with { Capabilities = fixture.Agent.Capabilities.Where(item => item.CapabilityKey != key).ToArray() };
        }
        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(async () => await acquire(disclosure, default));
        Assert.Equal(1, fixture.Receipts.Lookups);
        Assert.Same(receipt, fixture.Receipts.SavedReceipt);
        fixture.Agent = originalActor;
        fixture.CurrentAuthority = Current(fixture, read: true, mutation: false);
        await using (await acquire(disclosure, default)) { }
        Assert.Equal(2, fixture.Receipts.Lookups);
        Assert.Equal(1, fixture.Receipts.Calls);
        Assert.Equal(0, fixture.Definitions.Writes);
    }

    [Fact]
    public async Task Cached_settings_preserve_the_approved_historical_revision_after_a_later_edit() {
        var fixture = CreateFixture();
        var disclosure = await ReadSettingsAsync(fixture);
        var originalJson = disclosure.Result.GetRawText();
        fixture.Definitions.Current = HrSimpleChatTestFixture.Details(2, 1);
        var acquire = Callback(fixture, HrSimpleChatOperation.Settings);

        await using (await acquire(disclosure, default)) { }

        Assert.Equal(originalJson, disclosure.Result.GetRawText());
        var saved = disclosure.Result.Deserialize<HrSimpleChatDefinitionSettings>(HrSimpleChatProposalCodec.SerializerOptions)!;
        Assert.Equal(1, saved.Summary.Version.Revision);
        Assert.Equal(0, saved.Summary.Version.ConcurrencyToken);
        Assert.Equal("private system prompt", saved.Settings.SystemPrompt);
        Assert.Equal(1, fixture.Definitions.Reads);
        Assert.Equal(1, fixture.Definitions.HistoricalReads);
        Assert.Equal(fixture.Profile, fixture.Definitions.ObservedProfile);
        Assert.Equal(0, fixture.Definitions.Writes);
    }

    [Theory]
    [InlineData(SettingsChange.DefinitionId)]
    [InlineData(SettingsChange.Revision)]
    [InlineData(SettingsChange.ConcurrencyToken)]
    [InlineData(SettingsChange.SystemPrompt)]
    [InlineData(SettingsChange.ModelSettings)]
    [InlineData(SettingsChange.Name)]
    [InlineData(SettingsChange.TagsMismatch)]
    [InlineData(SettingsChange.MissingRevision)]
    [InlineData(SettingsChange.MissingDefinition)]
    public async Task Cached_settings_refuse_mismatched_saved_identity_or_missing_owner_history(SettingsChange change) {
        var fixture = CreateFixture();
        var disclosure = await ReadSettingsAsync(fixture);
        var saved = disclosure.Result.Deserialize<HrSimpleChatDefinitionSettings>(HrSimpleChatProposalCodec.SerializerOptions)!;
        var version = saved.Summary.Version;
        saved = change switch {
            SettingsChange.DefinitionId => saved with { Summary = saved.Summary with { Version = new(Guid.NewGuid(), version.Revision, version.ConcurrencyToken) } },
            SettingsChange.Revision => saved with { Summary = saved.Summary with { Version = new(version.DefinitionId, 2, version.ConcurrencyToken) } },
            SettingsChange.ConcurrencyToken => saved with { Summary = saved.Summary with { Version = new(version.DefinitionId, version.Revision, 1) } },
            SettingsChange.SystemPrompt => saved with { Settings = saved.Settings with { SystemPrompt = "Unapproved newer prompt" } },
            SettingsChange.ModelSettings => saved with { Settings = saved.Settings with { Settings = saved.Settings.Settings with { Temperature = 0.7 } } },
            SettingsChange.Name => saved with { Summary = saved.Summary with { Name = "Unapproved name" } },
            SettingsChange.TagsMismatch => saved with { Summary = saved.Summary with { Tags = ["different"] } },
            _ => saved
        };
        fixture.Definitions.MissingDefinition = change == SettingsChange.MissingDefinition;
        if (change == SettingsChange.MissingRevision) {
            fixture.Definitions.HistoricalRevision = null;
        }
        disclosure = disclosure with { Result = JsonSerializer.SerializeToElement(saved, HrSimpleChatProposalCodec.SerializerOptions) };
        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(async () => await Callback(fixture, HrSimpleChatOperation.Settings)(disclosure, default));
        Assert.Equal(0, fixture.Definitions.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Historical_settings_recheck_current_authority_before_and_after_the_owner_read(bool revokeDuringRead) {
        var fixture = CreateFixture();
        var disclosure = await ReadSettingsAsync(fixture);
        fixture.Definitions.Current = HrSimpleChatTestFixture.Details(2, 1);
        if (revokeDuringRead) {
            fixture.Definitions.OnRead = () => fixture.CurrentAuthority = Current(fixture, read: false, mutation: false);
        } else {
            fixture.CurrentAuthority = Current(fixture, read: false, mutation: false);
        }
        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(async () => await Callback(fixture, HrSimpleChatOperation.Settings)(disclosure, default));
        Assert.Equal(revokeDuringRead ? 1 : 0, fixture.Definitions.HistoricalReads);
        Assert.True(fixture.LeaseFactory.Active!.Disposed);
        Assert.Equal(0, fixture.Definitions.Writes);
    }

    public enum SettingsChange { DefinitionId, Revision, ConcurrencyToken, SystemPrompt, ModelSettings, Name, TagsMismatch, MissingRevision, MissingDefinition }

    private static async Task<AgentToolResultDisclosure> ReadSettingsAsync(HrSimpleChatTestFixture fixture) {
        var expected = new HrSimpleChatDefinitionVersion(HrSimpleChatTestFixture.DefinitionId, 1, 0);
        var payload = fixture.Codec.PrepareSettings(expected);
        fixture.Approve(payload);
        var result = await fixture.Service.SettingsAsync(fixture.Context, expected, default);
        return new(fixture.Admissions.Invocation!.IntentId, payload, AgentToolEffectState.None,
            JsonSerializer.SerializeToElement(result, HrSimpleChatProposalCodec.SerializerOptions));
    }

    [Fact]
    public async Task Saved_result_cannot_rebind_the_original_business_intent() {
        var fixture = CreateFixture();
        var disclosure = await CreateAsync(fixture);
        var acquire = Callback(fixture, HrSimpleChatOperation.Create);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await acquire(disclosure with { IntentId = new(Guid.NewGuid()) }, default));
        Assert.Equal(0, fixture.Receipts.Lookups);
        Assert.Equal(1, fixture.Receipts.Calls);
    }

    [Fact]
    public async Task Missing_owner_receipt_denies_disclosure_without_deleting_the_saved_identity() {
        var fixture = CreateFixture();
        var disclosure = await CreateAsync(fixture);
        var acquire = Callback(fixture, HrSimpleChatOperation.Create);
        fixture.Receipts.SavedReceipt = null;
        var denied = await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(async () => await acquire(disclosure, default));
        Assert.Equal("hr-simple-chat.receipt-unavailable", denied.Code);
        Assert.Equal(AgentToolEffectState.Committed, disclosure.EffectState);
        Assert.Equal(1, fixture.Receipts.Calls);
    }

    [Fact]
    public async Task Profile_switch_during_current_settings_read_blocks_cached_disclosure() {
        var fixture = CreateFixture();
        var disclosure = await ReadSettingsAsync(fixture);
        fixture.Definitions.OnRead = () => fixture.LeaseFactory.Active!.Current = false;
        await Assert.ThrowsAsync<LlmChatRuntimeProfileChangedException>(async () =>
            await Callback(fixture, HrSimpleChatOperation.Settings)(disclosure, default));
        Assert.True(fixture.LeaseFactory.Active!.Disposed);
    }

    private static HrSimpleChatTestFixture CreateFixture()
        => new(sourceScope: WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")));

    private static AgentExecutionAuthorityRecord Current(HrSimpleChatTestFixture fixture, bool read, bool mutation)
        => new(AgentExecutionAuthorityId.Create(), fixture.Agent.Id, fixture.Profile.ProfileId,
            new(fixture.Profile.Generation), fixture.Authority.WorkspaceScope, read, mutation,
            "current-policy", "current-fingerprint", HrSimpleChatTestFixture.Now);

    private static async Task<AgentToolResultDisclosure> CreateAsync(HrSimpleChatTestFixture fixture) {
        var command = HrSimpleChatTestFixture.CreateCommand();
        var payload = fixture.Codec.PrepareCreate(command);
        var intent = Guid.NewGuid();
        fixture.Approve(payload, intent);
        var result = await fixture.Service.CreateAsync(fixture.Context, command, default);
        return new(new(intent), payload, AgentToolEffectState.Committed,
            JsonSerializer.SerializeToElement(result, HrSimpleChatProposalCodec.SerializerOptions));
    }

    private static Func<AgentToolResultDisclosure, CancellationToken, ValueTask<IAsyncDisposable?>> Callback(
        HrSimpleChatTestFixture fixture, HrSimpleChatOperation operation)
        => fixture.Provider.GetToolMetadata(fixture.Context)
            .Single(item => item.ToolName == HrSimpleChatToolPolicy.Get(operation).ToolName).AuthorizeResultDisclosureAsync!;
}
