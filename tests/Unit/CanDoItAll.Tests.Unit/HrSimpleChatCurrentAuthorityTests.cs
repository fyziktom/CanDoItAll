using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Agents.SimpleChats;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class HrSimpleChatCurrentAuthorityTests {
    [Fact]
    public async Task Project_mutation_revocation_after_approval_prevents_owner_dispatch() {
        var fixture = ProjectFixture();
        var command = HrSimpleChatTestFixture.CreateCommand();
        fixture.Approve(fixture.Codec.PrepareCreate(command));
        fixture.CurrentAuthority = Current(fixture, mutationAllowed: false);

        var failure = await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() =>
            fixture.Service.CreateAsync(fixture.Context, command, CancellationToken.None));

        Assert.Equal("hr-simple-chat.authorization-denied", failure.Code);
        Assert.Equal(0, fixture.Receipts.Calls);
        Assert.Equal(0, fixture.Admissions.InvocationReads);
        Assert.True(fixture.LeaseFactory.Active!.Disposed);
        AssertSavedSource(fixture, Assert.Single(fixture.AuthorityResolver.Requests));
    }

    [Fact]
    public async Task Revoked_project_read_prevents_sensitive_owner_read() {
        var fixture = ProjectFixture();
        var expected = new HrSimpleChatDefinitionVersion(HrSimpleChatTestFixture.DefinitionId, 1, 0);
        fixture.Approve(fixture.Codec.PrepareSettings(expected));
        fixture.CurrentAuthority = Current(fixture, readAllowed: false, mutationAllowed: false);

        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() =>
            fixture.Service.SettingsAsync(fixture.Context, expected, CancellationToken.None));

        Assert.Equal(0, fixture.Definitions.Reads);
        Assert.Equal(0, fixture.Receipts.Calls);
        Assert.True(fixture.LeaseFactory.Active!.Disposed);
    }

    [Fact]
    public async Task Project_read_revoked_during_owner_read_prevents_disclosure() {
        var fixture = ProjectFixture();
        var expected = new HrSimpleChatDefinitionVersion(HrSimpleChatTestFixture.DefinitionId, 1, 0);
        fixture.Approve(fixture.Codec.PrepareSettings(expected));
        fixture.Definitions.ReadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Definitions.ReadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var reading = fixture.Service.SettingsAsync(fixture.Context, expected, CancellationToken.None);
        await fixture.Definitions.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.CurrentAuthority = Current(fixture, readAllowed: false, mutationAllowed: false);
        fixture.Definitions.ReadGate.SetResult();

        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() => reading);

        Assert.Equal(1, fixture.Definitions.Reads);
        Assert.Equal(2, fixture.AuthorityResolver.Requests.Count);
        Assert.True(fixture.LeaseFactory.Active!.Disposed);
    }

    [Fact]
    public async Task Current_authority_preserves_original_source_scope_profile_and_valid_create() {
        var fixture = ProjectFixture();
        var command = HrSimpleChatTestFixture.CreateCommand();
        fixture.Approve(fixture.Codec.PrepareCreate(command));
        fixture.CurrentAuthority = Current(fixture);

        var response = await fixture.Service.CreateAsync(fixture.Context, command, CancellationToken.None);

        Assert.Equal(HrSimpleChatTestFixture.DefinitionId, response.Receipt.DefinitionId);
        Assert.Equal(1, fixture.Receipts.Calls);
        Assert.Equal(2, fixture.AuthorityResolver.Requests.Count);
        Assert.All(fixture.AuthorityResolver.Requests, request => AssertSavedSource(fixture, request));
        Assert.NotEqual(fixture.Authority.AuthorityId, fixture.CurrentAuthority.AuthorityId);
        Assert.True(fixture.LeaseFactory.Active!.Disposed);
    }

    [Fact]
    public async Task Current_capability_ceiling_is_intersected_with_the_original_admission() {
        var fixture = ProjectFixture();
        var command = HrSimpleChatTestFixture.CreateCommand();
        fixture.Approve(fixture.Codec.PrepareCreate(command));
        fixture.CurrentAuthority = Current(fixture, capabilityKeys:
            [HrSimpleChatToolPolicy.Get(HrSimpleChatOperation.Search).CapabilityKey]);

        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() =>
            fixture.Service.CreateAsync(fixture.Context, command, CancellationToken.None));

        Assert.Equal(0, fixture.Receipts.Calls);
    }

    [Fact]
    public async Task Broader_current_authority_cannot_expand_the_original_read_only_admission() {
        var fixture = ProjectFixture(mutationAllowed: false);
        var command = HrSimpleChatTestFixture.CreateCommand();
        fixture.Approve(fixture.Codec.PrepareCreate(command));
        fixture.CurrentAuthority = Current(fixture);

        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() =>
            fixture.Service.CreateAsync(fixture.Context, command, CancellationToken.None));

        Assert.Equal(0, fixture.Receipts.Calls);
        Assert.Empty(fixture.AuthorityResolver.Requests);
    }

    [Fact]
    public async Task Unavailable_canonical_source_is_an_explicit_denial_before_owner_access() {
        var fixture = ProjectFixture();
        fixture.AuthorityResolver.Failure = new AgentExecutionAuthorityMismatchException("The original project is no longer accessible.");

        var failure = await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() =>
            fixture.Service.SearchAsync(fixture.Context, new(), CancellationToken.None));

        Assert.Equal("hr-simple-chat.authorization-denied", failure.Code);
        Assert.Null(fixture.Definitions.ObservedProfile);
        Assert.True(fixture.LeaseFactory.Active!.Disposed);
    }

    [Fact]
    public async Task Missing_canonical_resolver_never_falls_back_to_saved_grants() {
        var fixture = new HrSimpleChatTestFixture(provideAuthorityResolver: false);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.SearchAsync(fixture.Context, new(), CancellationToken.None));

        Assert.Contains("canonical current source authority resolver", failure.Message, StringComparison.Ordinal);
        Assert.Null(fixture.Definitions.ObservedProfile);
        Assert.True(fixture.LeaseFactory.Active!.Disposed);
    }

    [Theory]
    [InlineData("agent")]
    [InlineData("profile")]
    [InlineData("generation")]
    [InlineData("scope")]
    public async Task Current_authority_cannot_rebind_the_admitted_execution(string difference) {
        var fixture = ProjectFixture();
        fixture.CurrentAuthority = Current(fixture, difference: difference);

        await Assert.ThrowsAsync<HrSimpleChatAdministrationException>(() =>
            fixture.Service.SearchAsync(fixture.Context, new(), CancellationToken.None));

        Assert.Null(fixture.Definitions.ObservedProfile);
    }

    private static HrSimpleChatTestFixture ProjectFixture(bool mutationAllowed = true)
        => new(mutationAllowed: mutationAllowed, sourceScope: WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")));

    private static AgentExecutionAuthorityRecord Current(HrSimpleChatTestFixture fixture,
        bool readAllowed = true, bool mutationAllowed = true, IReadOnlyList<string>? capabilityKeys = null,
        string? difference = null)
        => new(AgentExecutionAuthorityId.Create(), difference == "agent" ? Guid.NewGuid() : fixture.Agent.Id,
            difference == "profile" ? Guid.NewGuid() : fixture.Profile.ProfileId,
            new(fixture.Profile.Generation + (difference == "generation" ? 1 : 0)),
            difference == "scope" ? WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")) : fixture.Authority.WorkspaceScope,
            readAllowed, mutationAllowed, "current-project-policy", "current-project-policy-fingerprint",
            HrSimpleChatTestFixture.Now, allowedCapabilityKeys: capabilityKeys);

    private static void AssertSavedSource(HrSimpleChatTestFixture fixture, AgentExecutionAuthorityResolutionRequest request) {
        Assert.Equal(fixture.Agent.Id, request.AgentId);
        Assert.Equal(fixture.Source.SourceKind, request.SourceKind);
        Assert.Equal(fixture.Source.SourceId, request.SourceId);
        Assert.Equal(fixture.Authority.WorkspaceScope, request.ObservedWorkspaceScope);
        Assert.Equal(fixture.Session.Profile.Generation, request.ExpectedDatabaseProfileGeneration);
        Assert.Null(request.UiAccessHint);
    }
}
