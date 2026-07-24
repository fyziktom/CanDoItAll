using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmInteractionIntegrationTests
{
    [Fact]
    public async Task Crm_service_persists_profile_stakeholders_interactions_and_search_projection()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var crmService = scope.ServiceProvider.GetRequiredService<CrmService>();
        var searchIndexService = scope.ServiceProvider.GetRequiredService<ISearchIndexService>();

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
        var activity = await crmService.SearchAccountActivityAsync(
            new CrmActivityHistoryQuery(accountId));
        var searchResults = await searchIndexService.SearchAsync("Fabrikam");

        Assert.NotNull(workspace);
        Assert.Equal(CrmAccountRelationshipStage.ActiveCustomer, workspace.Profile.RelationshipStage);
        Assert.Single(workspace.Stakeholders);
        Assert.Equal(1, activity.ActionCount);
        Assert.Equal(1, activity.OverdueActionCount);
        Assert.Contains(
            activity.Items,
            item => item.Title.Contains("Executive steering review", StringComparison.Ordinal));
        Assert.Contains(
            searchResults,
            item => item.Route.Contains($"/crm-hr/crm?accountId={accountId}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Activity_history_pages_account_and_party_rows_without_materializing_the_full_timeline()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var crmService = scope.ServiceProvider.GetRequiredService<CrmService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var accountId = await CreatePartyAsync(
            partyDirectoryService,
            "Paged account",
            PartyType.Organization,
            PartyLifecycleStatus.Prospect,
            PartyRoleKind.Customer,
            "paged-account@example.test");
        var participantId = await CreatePartyAsync(
            partyDirectoryService,
            "Paged participant",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Stakeholder,
            "paged-participant@example.test");
        var now = DateTimeOffset.UtcNow;

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            for (var index = 0; index < 24; index++)
            {
                var occurredAtUtc = now.AddMinutes(index);
                var interactionId = Guid.NewGuid();
                dbContext.Set<InteractionRecord>().Add(new InteractionRecord
                {
                    Id = interactionId,
                    InteractionType = InteractionType.Meeting,
                    Subject = $"Paged interaction {index:D2}",
                    Summary = "History paging test",
                    NextActionText = index % 3 == 0 ? "Follow up" : string.Empty,
                    NextActionDueUtc = index % 3 == 0
                        ? now.AddDays(-1)
                        : null,
                    OccurredAtUtc = occurredAtUtc,
                    CreatedAtUtc = occurredAtUtc,
                    UpdatedAtUtc = occurredAtUtc
                });
                dbContext.Set<InteractionPartyLink>().AddRange(
                    new InteractionPartyLink
                    {
                        InteractionId = interactionId,
                        PartyId = accountId,
                        Role = InteractionPartyRole.Account
                    },
                    new InteractionPartyLink
                    {
                        InteractionId = interactionId,
                        PartyId = participantId,
                        Role = InteractionPartyRole.Attendee
                    });
                dbContext.Set<CrmHrAuditEntry>().AddRange(
                    new CrmHrAuditEntry
                    {
                        EntityType = "CrmAccount",
                        EntityId = accountId,
                        Action = "InteractionLogged",
                        Summary = $"Account audit {index:D2}",
                        Actor = "integration-tests",
                        CreatedAtUtc = occurredAtUtc
                    },
                    new CrmHrAuditEntry
                    {
                        EntityType = nameof(Party),
                        EntityId = participantId,
                        Action = "InteractionLogged",
                        Summary = $"Party audit {index:D2}",
                        Actor = "integration-tests",
                        CreatedAtUtc = occurredAtUtc
                    });
            }

            await dbContext.SaveChangesAsync();
        }

        var accountPage = await crmService.SearchAccountActivityAsync(
            new CrmActivityHistoryQuery(accountId, PageIndex: 2, PageSize: 10));
        var partyPage = await partyDirectoryService.SearchPartyActivityAsync(
            new CrmActivityHistoryQuery(participantId, PageIndex: 4, PageSize: 10));

        Assert.Equal(48, accountPage.TotalCount);
        Assert.Equal(10, accountPage.Items.Count);
        Assert.Equal(8, accountPage.ActionCount);
        Assert.Equal(8, accountPage.OverdueActionCount);
        Assert.Equal(2, accountPage.PageIndex);
        Assert.Equal(5, accountPage.TotalPages);
        Assert.Equal(49, partyPage.TotalCount);
        Assert.Equal(9, partyPage.Items.Count);
        Assert.Equal(4, partyPage.PageIndex);
        Assert.All(accountPage.Items, item => Assert.NotEqual(Guid.Empty, item.Id));
        Assert.All(partyPage.Items, item => Assert.NotEqual(Guid.Empty, item.Id));
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
