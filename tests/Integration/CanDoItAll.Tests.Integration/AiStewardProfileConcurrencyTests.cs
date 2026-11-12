using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class AiStewardProfileConcurrencyTests {
    [Fact]
    public async Task A_stale_business_profile_cannot_replace_a_peer_stewardship_update() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var (owner, partyId) = await CreateAsync(scope.ServiceProvider);
        var first = (await owner.GetAgentWorkspaceAsync(partyId))!.Profile;
        var stale = (await owner.GetAgentWorkspaceAsync(partyId))!.Profile;
        first.Notes = "Accepted peer business notes";
        Assert.True((await owner.SaveAgentProfileAsync(first)).IsSuccess);
        stale.Notes = "Stale replacement business notes";

        var result = await owner.SaveAgentProfileAsync(stale);

        Assert.True(result.IsFailure);
        Assert.Equal("Accepted peer business notes", (await owner.GetAgentWorkspaceAsync(partyId))!.Profile.Notes);
    }

    [Fact]
    public async Task A_business_save_preserves_a_technical_peer_change_instead_of_replaying_its_old_projection() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var (owner, partyId) = await CreateAsync(services);
        var shown = (await owner.GetAgentWorkspaceAsync(partyId))!;
        var factory = services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var technical = factory.GetWorkspaceService(factory.GetOrganizationScope());
        var peer = await technical.GetAgentEditorAsync(shown.TechnicalAgentId!.Value);
        peer.Model = "peer-owned-technical-model";
        await technical.SaveAgentAsync(peer);
        await services.GetRequiredService<IAiTechnicalAgentBridge>().SynchronizeDirectoryProjectionAsync();
        shown.Profile.Notes = "Business-only update after a technical change";

        var result = await owner.SaveAgentProfileAsync(shown.Profile);

        Assert.True(result.IsSuccess);
        Assert.Equal("peer-owned-technical-model", (await technical.GetAgentEditorAsync(shown.TechnicalAgentId.Value)).Model);
        Assert.Equal("Business-only update after a technical change", (await owner.GetAgentWorkspaceAsync(partyId))!.Profile.Notes);
    }

    [Fact]
    public async Task Technical_configuration_cannot_be_replaced_through_the_business_profile_command() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var (owner, partyId) = await CreateAsync(scope.ServiceProvider);
        var shown = (await owner.GetAgentWorkspaceAsync(partyId))!;
        var before = shown.Profile.DefaultModel;
        shown.Profile.DefaultModel = "technical-change-from-business-command";

        var result = await owner.SaveAgentProfileAsync(shown.Profile);

        Assert.True(result.IsFailure);
        Assert.Equal(before, (await owner.GetAgentWorkspaceAsync(partyId))!.Profile.DefaultModel);
    }

    [Fact]
    public async Task An_existing_business_profile_requires_its_shown_original_before_any_replacement() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var (owner, partyId) = await CreateAsync(scope.ServiceProvider);
        var shown = (await owner.GetAgentWorkspaceAsync(partyId))!.Profile;

        var result = await owner.SaveAgentProfileAsync(new() { Id = shown.Id, PartyId = partyId, Notes = "Unobserved replacement" });

        Assert.True(result.IsFailure);
        Assert.NotEqual("Unobserved replacement", (await owner.GetAgentWorkspaceAsync(partyId))!.Profile.Notes);
    }

    [Fact]
    public async Task A_business_profile_cannot_ignore_a_different_row_identity() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var (owner, partyId) = await CreateAsync(scope.ServiceProvider);
        var shown = (await owner.GetAgentWorkspaceAsync(partyId))!.Profile;
        var actualId = shown.Id;
        shown.Id = Guid.NewGuid();
        shown.Notes = "Wrong profile identity";

        var result = await owner.SaveAgentProfileAsync(shown);

        Assert.True(result.IsFailure);
        var saved = (await owner.GetAgentWorkspaceAsync(partyId))!.Profile;
        Assert.Equal(actualId, saved.Id);
        Assert.NotEqual("Wrong profile identity", saved.Notes);
    }

    [Fact]
    public async Task The_direct_technical_bridge_rejects_replacement_of_an_existing_agent() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var (owner, partyId) = await CreateAsync(services);
        var shown = (await owner.GetAgentWorkspaceAsync(partyId))!;
        var before = shown.Profile.DefaultModel;
        shown.Profile.DefaultModel = "Bypassed technical replacement";

        var result = await services.GetRequiredService<IAiTechnicalAgentBridge>().SaveAsync(shown.Profile);

        Assert.True(result.IsFailure);
        Assert.Equal(before, (await owner.GetAgentWorkspaceAsync(partyId))!.Profile.DefaultModel);
    }

    [Fact]
    public async Task Two_native_technical_creations_for_one_unbound_party_cannot_replace_each_others_binding() {
        await using var application = await TestApplication.CreateAsync();
        await using var first = application.Services.CreateAsyncScope();
        await using var second = application.Services.CreateAsyncScope();
        var created = await first.ServiceProvider.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new() {
            PartyType = PartyType.AiAgent, DisplayName = "Single technical creation"
        });
        Assert.True(created.IsSuccess);

        var results = await Task.WhenAll(
            first.ServiceProvider.GetRequiredService<IAiTechnicalAgentBridge>().SaveAsync(new() { PartyId = created.Value }),
            second.ServiceProvider.GetRequiredService<IAiTechnicalAgentBridge>().SaveAsync(new() { PartyId = created.Value }));

        var accepted = Assert.Single(results, result => result.IsSuccess).Value!;
        Assert.Single(results, result => result.IsFailure);
        Assert.Equal(created.Value, accepted.CreatedBusinessProfile!.PartyId);
        Assert.Null(accepted.CreatedBusinessProfile.ProfileId);
        var factory = first.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var catalog = await factory.GetWorkspaceService(factory.GetOrganizationScope()).LoadCatalogSnapshotAsync();
        Assert.Equal(accepted.TechnicalAgentId, Assert.Single(catalog.Snapshot.Catalog.Agents, agent => agent.Name == "Single technical creation").Id);
        var summary = await first.ServiceProvider.GetRequiredService<IAiTechnicalAgentBridge>().GetDirectorySummariesAsync([created.Value]);
        Assert.Equal(accepted.TechnicalAgentId, summary[created.Value].TechnicalAgentId);
    }

    [Theory]
    [InlineData(BusinessField.Owner)]
    [InlineData(BusinessField.Validation)]
    [InlineData(BusinessField.ReviewDate)]
    public async Task Every_business_field_participates_in_the_shown_original(BusinessField field) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var (_, partyId) = await CreateAsync(services);
        var owner = services.GetRequiredService<IAiStewardProfileEditing>();
        var stale = (await owner.ReadAsync(partyId))!;
        var peer = (await owner.ReadAsync(partyId))!;
        switch (field) {
            case BusinessField.Owner:
                var person = await services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new() { DisplayName = "Native steward", PartyType = PartyType.Person });
                Assert.True(person.IsSuccess);
                peer.OwnerPartyId = person.Value;
                break;
            case BusinessField.Validation:
                peer.ValidationStatus = AiValidationStatus.Approved;
                break;
            case BusinessField.ReviewDate:
                peer.LastReviewedOn = new(2026, 11, 2);
                break;
        }
        Assert.True((await owner.SaveAsync(peer)).IsSuccess);
        stale.Notes = "Stale notes after another field changed";

        var result = await owner.SaveAsync(stale);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == AiStewardEditingErrors.Conflict);
        var current = (await owner.ReadAsync(partyId))!;
        Assert.Equal(peer.OwnerPartyId, current.OwnerPartyId);
        Assert.Equal(peer.ValidationStatus, current.ValidationStatus);
        Assert.Equal(peer.LastReviewedOn, current.LastReviewedOn);
        Assert.NotEqual(stale.Notes, current.Notes);
    }

    [Fact]
    public async Task The_independent_business_owner_creates_from_a_missing_observation_and_rejects_its_replay() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new() {
            DisplayName = "Business-only AI party", PartyType = PartyType.AiAgent
        });
        Assert.True(created.IsSuccess);
        var owner = new AiStewardProfileEditingService(services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>(),
            services.GetRequiredService<IClock>(), services.GetRequiredService<CrmHrChanges>(),
            services.GetRequiredService<IActivityStream>(), services.GetRequiredService<AiAgentSearchProjection>());
        var missing = (await owner.ReadAsync(created.Value))!;
        Assert.Null(missing.Original!.ProfileId);
        missing.Notes = "  Accepted business-only notes  ";
        AiStewardProfileSaveReceipt? accepted = null;

        Assert.True((await owner.SaveAsync(missing, onCommitted: receipt => accepted = receipt)).IsSuccess);
        var replay = await owner.SaveAsync(missing);

        Assert.True(replay.IsFailure);
        Assert.NotNull(accepted);
        Assert.Equal("Accepted business-only notes", accepted.Accepted.Notes);
        Assert.Equal(accepted.Accepted.Original, (await owner.ReadAsync(created.Value))!.Original);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_actual_commit_boundary_distinguishes_revoked_authority_from_a_failed_accepted_callback(bool afterCommit) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var (_, partyId) = await CreateAsync(services);
        var owner = services.GetRequiredService<IAiStewardProfileEditing>();
        var draft = (await owner.ReadAsync(partyId))!;
        var original = draft.Original;
        draft.Notes = "  Submitted stewardship  ";
        var checks = 0;
        AiStewardProfileSaveReceipt? accepted = null;
        using var admission = services.GetRequiredService<CrmHrChanges>().EnterUiWrite(_ => {
            if (!afterCommit && ++checks == 3) {
                throw new UnauthorizedAccessException("Revoked after the actual SQL flush.");
            }
            return ValueTask.CompletedTask;
        });

        var error = await Record.ExceptionAsync(() => owner.SaveAsync(draft, onCommitted: receipt => {
            accepted = receipt;
            throw new DbUpdateConcurrencyException("Accepted callback failed after physical commit.");
        }));

        var current = (await owner.ReadAsync(partyId))!;
        if (afterCommit) {
            Assert.IsType<DbUpdateConcurrencyException>(error);
            Assert.NotNull(accepted);
            Assert.Equal("Submitted stewardship", accepted.Accepted.Notes);
            Assert.Equal(accepted.Accepted.Original, current.Original);
        } else {
            Assert.IsType<UnauthorizedAccessException>(error);
            Assert.Equal(3, checks);
            Assert.Null(accepted);
            Assert.Equal(original, current.Original);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Party_type_changes_cannot_invalidate_a_saved_AI_profile_or_its_steward(bool steward) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var (_, partyId) = await CreateAsync(services);
        var parties = services.GetRequiredService<PartyDirectoryService>();
        if (steward) {
            var person = await parties.SavePartyAsync(new() { DisplayName = "Required steward", PartyType = PartyType.Person });
            Assert.True(person.IsSuccess);
            var owner = services.GetRequiredService<IAiStewardProfileEditing>();
            var profile = (await owner.ReadAsync(partyId))!;
            profile.OwnerPartyId = person.Value;
            Assert.True((await owner.SaveAsync(profile)).IsSuccess);
            partyId = person.Value;
        }
        var draft = (await parties.GetPartyAsync(partyId))!;
        var original = draft.PartyType;
        draft.PartyType = PartyType.Organization;

        var result = await parties.SavePartyAsync(draft);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == AiStewardEditingErrors.PartyTypeConflict);
        Assert.Equal(original, (await parties.GetPartyAsync(partyId))!.PartyType);
    }

    public enum BusinessField { Owner, Validation, ReviewDate }

    private static async Task<(AiAgentService Owner, Guid PartyId)> CreateAsync(IServiceProvider services) {
        var owner = services.GetRequiredService<AiAgentService>();
        var created = await owner.CreateAgentAsync("Concurrent AI steward", summary: "Original stewardship notes");
        Assert.True(created.IsSuccess, string.Join(" ", created.Errors.Select(error => error.Message)));
        Assert.NotNull(await owner.GetAgentWorkspaceAsync(created.Value));
        return (owner, created.Value);
    }
}
