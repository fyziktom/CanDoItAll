using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmPartyCommandIntegrationTests
{
    [Fact]
    public async Task Command_service_creates_safe_parties_and_lists_empty_or_created_affiliations()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var commandService = scope.ServiceProvider
            .GetRequiredService<ICrmPartyCommandService>();

        var suffix = Guid.NewGuid().ToString("N");
        var personResult = await commandService.CreatePartyAsync(
            new CrmPartyCreateCommand(
                PartyType.Person,
                $"Agent-created person {suffix}",
                PartyLifecycleStatus.Draft,
                PreferredName: "Garden coordinator",
                ExternalCode: $"person-{suffix}",
                Tags: ["agent-created", "planning"]),
            "hr-agent:integration-test");
        var organizationResult = await commandService.CreatePartyAsync(
            new CrmPartyCreateCommand(
                PartyType.Organization,
                $"Agent-created organization {suffix}",
                PartyLifecycleStatus.Active,
                ExternalCode: $"organization-{suffix}"),
            "hr-agent:integration-test");

        Assert.True(personResult.IsSuccess);
        Assert.True(organizationResult.IsSuccess);
        Assert.Equal(
            ["agent-created", "planning"],
            personResult.Value!.Tags);

        var initiallyEmpty = await commandService.ListAffiliationsAsync(
            personResult.Value.PartyId);
        Assert.True(initiallyEmpty.IsSuccess);
        Assert.Empty(initiallyEmpty.Value!);

        var upsertResult = await commandService.UpsertAffiliationAsync(
            new CrmPartyAffiliationUpsertCommand(
                null,
                personResult.Value.PartyId,
                organizationResult.Value!.PartyId,
                PartyOrganizationAffiliationKind.ExternalContact,
                IsPrimary: true,
                JobTitle: "Garden project contact"),
            "hr-agent:integration-test");

        Assert.True(upsertResult.IsSuccess);
        Assert.Equal(
            PartyOrganizationAffiliationKind.ExternalContact,
            upsertResult.Value!.AffiliationKind);
        Assert.True(upsertResult.Value.IsPrimary);

        var listed = await commandService.ListAffiliationsAsync(
            personResult.Value.PartyId);
        var affiliation = Assert.Single(listed.Value!);
        Assert.Equal(upsertResult.Value.AffiliationId, affiliation.AffiliationId);
        Assert.Equal(
            organizationResult.Value.PartyId,
            affiliation.OrganizationPartyId);
    }

    [Fact]
    public async Task Command_service_preserves_restricted_affiliation_fields_and_denies_sensitive_endpoints()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var commandService = scope.ServiceProvider
            .GetRequiredService<ICrmPartyCommandService>();
        var directoryService = scope.ServiceProvider
            .GetRequiredService<PartyDirectoryService>();
        var affiliationService = scope.ServiceProvider
            .GetRequiredService<IPartyOrganizationAffiliationService>();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var suffix = Guid.NewGuid().ToString("N");
        var personId = await CreatePartyAsync(
            directoryService,
            PartyType.Person,
            $"Restricted-field person {suffix}");
        var organizationId = await CreatePartyAsync(
            directoryService,
            PartyType.Organization,
            $"Restricted-field organization {suffix}");
        var sensitiveOrganizationId = await CreatePartyAsync(
            directoryService,
            PartyType.Organization,
            $"Sensitive organization {suffix}",
            isSensitive: true);

        var initial = await affiliationService.UpsertAsync(
            new PartyOrganizationAffiliationEditorModel
            {
                PersonPartyId = personId,
                OrganizationPartyId = organizationId,
                AffiliationKind =
                    PartyOrganizationAffiliationKind.Contractor,
                IsPrimary = true,
                JobTitle = "Initial title",
                EmployeeCode = "RESTRICTED-CODE",
                Notes = "Restricted HR note"
            },
            "integration-tests");
        Assert.True(initial.IsSuccess);

        var update = await commandService.UpsertAffiliationAsync(
            new CrmPartyAffiliationUpsertCommand(
                initial.Value!.Id,
                personId,
                organizationId,
                PartyOrganizationAffiliationKind.Contractor,
                IsPrimary: true,
                JobTitle: "Updated safe title",
                ExpectedUpdatedAtUtc: initial.Value.UpdatedAtUtc),
            "hr-agent:integration-test");
        Assert.True(update.IsSuccess);

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync();
        var persisted = await dbContext
            .Set<PartyOrganizationAffiliation>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == initial.Value.Id);
        Assert.Equal("Updated safe title", persisted.JobTitle);
        Assert.Equal("RESTRICTED-CODE", persisted.EmployeeCode);
        Assert.Equal("Restricted HR note", persisted.Notes);

        var denied = await commandService.UpsertAffiliationAsync(
            new CrmPartyAffiliationUpsertCommand(
                null,
                personId,
                sensitiveOrganizationId,
                PartyOrganizationAffiliationKind.ExternalContact,
                IsPrimary: false),
            "hr-agent:integration-test");
        Assert.False(denied.IsSuccess);
        Assert.Contains(
            denied.Errors,
            error => error.Code ==
                     CrmPartyCommandErrorCodes.SensitiveRecordDenied);
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService directoryService,
        PartyType partyType,
        string displayName,
        bool isSensitive = false)
    {
        var result = await directoryService.SavePartyAsync(
            new PartyEditorModel
            {
                PartyType = partyType,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = displayName,
                IsSensitive = isSensitive,
                LastChangedBy = "integration-tests"
            });
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
