using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmHrSchemaIntegrationTests
{
    [Fact]
    public async Task Test_application_bootstrap_creates_crm_hr_schema_and_seed_data()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        var lookupOptions = await dbContext.Set<CrmHrLookupOption>()
            .OrderBy(option => option.CatalogKind)
            .ThenBy(option => option.DisplayOrder)
            .ToListAsync();
        var parties = await dbContext.Set<Party>().ToListAsync();

        Assert.Contains(
            appliedMigrations,
            migrationId => migrationId.Contains("InitialPostgreSqlBaseline", StringComparison.Ordinal));
        Assert.Empty(parties);
        Assert.Contains(
            lookupOptions,
            option => option.CatalogKind == LookupCatalogKind.OpportunityStage
                && option.Key == nameof(OpportunityStage.Qualified));
        Assert.Contains(
            lookupOptions,
            option => option.CatalogKind == LookupCatalogKind.AssignmentKind
                && option.Key == nameof(ProjectPartyAssignmentKind.DeliveryUnit));
    }

    [Fact]
    public async Task Party_directory_service_round_trips_party_aggregate()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();

        var saveResult = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Organization,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = "Northwind Delivery",
            LegalName = "Northwind Delivery LLC",
            PreferredName = "Northwind",
            ExternalCode = "NW-001",
            Summary = "Primary delivery partner.",
            Notes = "Handles implementation work.",
            Tags = ["partner", "delivery"],
            Region = "EMEA",
            CountryCode = "DE",
            TimeZone = "Europe/Berlin",
            ExtendedDataJson = """
                {
                  "erpId": "erp-2048",
                  "rating": "gold"
                }
                """,
            LastChangedBy = "integration-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = PartyRoleKind.Partner,
                    Title = "Delivery partner",
                    IsPrimary = true
                },
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = PartyRoleKind.Vendor,
                    Title = "Vendor"
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary",
                    Value = "hello@northwind.example",
                    NormalizedValue = "hello@northwind.example",
                    IsPrimary = true,
                    IsPublic = true,
                    Tags = ["billing", "preferred"]
                }
            ],
            Addresses =
            [
                new PartyAddressEditorModel
                {
                    AddressType = "Office",
                    Line1 = "Alexanderplatz 1",
                    City = "Berlin",
                    Region = "Berlin",
                    PostalCode = "10178",
                    CountryCode = "DE",
                    IsPrimary = true
                }
            ]
        });

        Assert.True(saveResult.IsSuccess);

        var party = await partyDirectoryService.GetPartyAsync(saveResult.Value);
        var summaries = await partyDirectoryService.ListPartiesAsync();

        Assert.NotNull(party);
        Assert.Equal("Northwind Delivery", party.DisplayName);
        Assert.Equal(PartyType.Organization, party.PartyType);
        Assert.Equal(PartyLifecycleStatus.Active, party.LifecycleStatus);
        Assert.Equal(["partner", "delivery"], party.Tags);
        Assert.Equal("integration-tests", party.LastChangedBy);
        var contactPoint = Assert.Single(party.ContactPoints);
        Assert.Equal(["billing", "preferred"], contactPoint.Tags);
        Assert.Single(party.Addresses);
        Assert.Equal(2, party.Roles.Count);
        Assert.Contains(
            summaries,
            item => item.Id == saveResult.Value
                && item.DisplayName == "Northwind Delivery"
                && item.PartyType == PartyType.Organization);
    }

    [Fact]
    public async Task Party_directory_service_rejects_invalid_extended_data_json()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();

        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            DisplayName = "Broken Metadata",
            ExtendedDataJson = "{ invalid json }"
        });

        Assert.True(result.IsFailure);
        Assert.Contains(
            result.Errors,
            error => error.Code == "crmhr.party.extended-data-invalid");
    }

}
