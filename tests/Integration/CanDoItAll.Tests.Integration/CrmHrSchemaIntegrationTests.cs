using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmHrSchemaIntegrationTests
{
    private const string CrmHrIntegrityMigration =
        "20260724114400_ImproveCrmHrRecordSelectionAndRecognitionIntegrity";

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

    [Fact]
    public async Task Crm_hr_integrity_migration_backfills_legacy_rows_without_fabricating_recognition()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("crmhrcontacttagmigration");
        await using var dbContext = new AppDbContext(database.CreateAppDbContextOptions());
        var migrations = dbContext.Database.GetMigrations().ToList();
        var migrationIndex = migrations.IndexOf(CrmHrIntegrityMigration);
        Assert.True(migrationIndex > 0);

        var migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[migrationIndex - 1]);
        var partyId = Guid.NewGuid();
        var contactPointId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var stageHistoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CrmHr_Parties"
                ("Id", "PartyType", "LifecycleStatus", "DisplayName", "LegalName",
                 "PreferredName", "ExternalCode", "Summary", "Notes", "TagsJson",
                 "Region", "CountryCode", "TimeZone", "IsSensitive", "ExtendedDataJson",
                 "LastChangedBy", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES
                ({partyId}, {"Person"}, {"Active"}, {"Existing contact"}, {""},
                 {""}, {""}, {""}, {""}, {"[]"},
                 {""}, {""}, {""}, {false}, {"{}"},
                 {"migration-test"}, {now}, {now});

            INSERT INTO "CrmHr_PartyContactPoints"
                ("Id", "PartyId", "ContactType", "Label", "Value", "NormalizedValue",
                 "IsPrimary", "IsPublic", "Notes")
            VALUES
                ({contactPointId}, {partyId}, {"Email"}, {"Primary"},
                 {"existing@example.test"}, {"existing@example.test"}, {true}, {true}, {""});

            INSERT INTO "CrmHr_Opportunities"
                ("Id", "Title", "Stage", "RelationshipStage", "AccountPartyId",
                 "OwnerPartyId", "DeliveryUnitPartyId", "LinkedProjectId", "CurrencyCode",
                 "Amount", "ProbabilityPercent", "ExpectedCloseDateUtc", "OpportunitySource",
                 "LostReason", "Summary", "Notes", "ExtendedDataJson", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES
                ({opportunityId}, {"Legacy won opportunity"}, {"Won"}, {""}, {partyId},
                 {partyId}, NULL, NULL, {"USD"},
                 {125m}, {100}, NULL, {"Direct"},
                 {""}, {""}, {""}, {"{}"}, {now}, {now});

            INSERT INTO "CrmHr_OpportunityStageHistory"
                ("Id", "OpportunityId", "Stage", "ChangedAtUtc", "ChangedBy", "Notes")
            VALUES
                ({stageHistoryId}, {opportunityId}, {"Won"}, {now}, {"migration-test"}, {""});
            """);

        await migrator.MigrateAsync(CrmHrIntegrityMigration);

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        await using var command = connection.CreateCommand();
        command.CommandText =
            """SELECT "TagsJson" FROM "CrmHr_PartyContactPoints" WHERE "Id" = @contactPointId""";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "contactPointId";
        parameter.Value = contactPointId;
        command.Parameters.Add(parameter);
        var tagsJson = await command.ExecuteScalarAsync();

        Assert.Equal("[]", tagsJson);

        await using var recognitionCommand = connection.CreateCommand();
        recognitionCommand.CommandText =
            """
            SELECT "RecognizedAmount", "RecognizedCurrencyCode"
            FROM "CrmHr_OpportunityStageHistory"
            WHERE "Id" = @stageHistoryId
            """;
        var recognitionParameter = recognitionCommand.CreateParameter();
        recognitionParameter.ParameterName = "stageHistoryId";
        recognitionParameter.Value = stageHistoryId;
        recognitionCommand.Parameters.Add(recognitionParameter);
        await using var reader = await recognitionCommand.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.True(await reader.IsDBNullAsync(0));
        Assert.Equal(string.Empty, reader.GetString(1));
    }
}
