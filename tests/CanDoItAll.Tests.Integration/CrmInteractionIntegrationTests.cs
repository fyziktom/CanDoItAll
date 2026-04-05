using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Activity;
using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmInteractionIntegrationTests
{
    [Fact]
    public async Task Crm_service_persists_profile_stakeholders_interactions_and_search_activity_projection()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var crmService = scope.ServiceProvider.GetRequiredService<CrmService>();
        var searchIndexService = scope.ServiceProvider.GetRequiredService<ISearchIndexService>();
        var activityService = scope.ServiceProvider.GetRequiredService<ActivityService>();

        var accountId = await CreatePartyAsync(
            partyDirectoryService,
            "Fabrikam Delivery",
            PartyType.Organization,
            PartyLifecycleStatus.Prospect,
            PartyRoleKind.Customer,
            "crm@fabrikam.example");
        var sponsorId = await CreatePartyAsync(
            partyDirectoryService,
            "Nina Sponsor",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Stakeholder,
            "nina.sponsor@example.test");

        var profileResult = await crmService.SaveAccountProfileAsync(new CrmAccountProfileEditorModel
        {
            AccountPartyId = accountId,
            RelationshipStage = CrmAccountRelationshipStage.ActiveCustomer,
            CommercialNotes = "Expansion depends on approval of the pilot budget.",
            ConstraintNotes = "Procurement review still open.",
            TimingRiskNotes = "Board sign-off may slip into next month.",
            LastChangedBy = "integration-tests"
        });
        var stakeholderResult = await crmService.SaveStakeholdersAsync(
            accountId,
            [
                new CrmAccountStakeholderEditorModel
                {
                    RelatedPartyId = sponsorId,
                    Role = CrmAccountStakeholderRole.Sponsor,
                    IsPrimary = true,
                    Notes = "Executive sponsor"
                }
            ],
            "integration-tests");
        var interactionResult = await crmService.AddInteractionAsync(
            accountId,
            new CrmInteractionEditorModel
            {
                InteractionType = InteractionType.Meeting,
                Subject = "Executive steering review",
                Summary = "Confirmed commercial path and follow-up.",
                Notes = "Sponsor requested revised scope by Friday.",
                NextActionText = "Send revised scope",
                NextActionOwnerPartyId = sponsorId,
                NextActionDueOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
                ParticipantPartyIds = [sponsorId]
            },
            "integration-tests");

        Assert.True(profileResult.IsSuccess);
        Assert.True(stakeholderResult.IsSuccess);
        Assert.True(interactionResult.IsSuccess);

        var workspace = await crmService.GetAccountWorkspaceAsync(accountId);
        var searchResults = await searchIndexService.SearchAsync("Fabrikam");
        var activityTimeline = await activityService.ListRecentAsync();

        Assert.NotNull(workspace);
        Assert.Equal(CrmAccountRelationshipStage.ActiveCustomer, workspace.Profile.RelationshipStage);
        Assert.Single(workspace.Stakeholders);
        Assert.Single(workspace.OverdueNextActions);
        Assert.Contains(
            workspace.ActivityTimeline,
            item => item.Title.Contains("Executive steering review", StringComparison.Ordinal));
        Assert.Contains(
            searchResults,
            item => item.Route.Contains($"/crm-hr/crm?accountId={accountId}", StringComparison.Ordinal));
        Assert.Contains(
            activityTimeline,
            item => item.Title.Contains("Updated account profile for Fabrikam Delivery", StringComparison.Ordinal));
        Assert.Contains(
            activityTimeline,
            item => item.Title.Contains("Logged Meeting for Fabrikam Delivery", StringComparison.Ordinal));
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType,
        PartyLifecycleStatus lifecycleStatus,
        PartyRoleKind roleKind,
        string email)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = lifecycleStatus,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = roleKind,
                    Title = roleKind.ToString(),
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = email,
                    NormalizedValue = email.ToLowerInvariant(),
                    IsPrimary = true,
                    IsPublic = true
                }
            ]
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
