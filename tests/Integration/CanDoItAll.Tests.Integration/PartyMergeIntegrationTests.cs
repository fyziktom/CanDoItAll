using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class PartyMergeIntegrationTests
{
    [Fact]
    public async Task Merge_party_reassigns_related_history_and_deduplicates_relationships()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var managementService = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var now = DateTimeOffset.UtcNow;

        var parentPartyId = await CreatePartyAsync(
            partyDirectoryService,
            "Northwind Holding",
            "holding@northwind.example",
            PartyType.Organization,
            PartyRoleKind.Partner);
        var retainedPartyId = await CreatePartyAsync(
            partyDirectoryService,
            "Northwind Delivery",
            "shared@northwind.example",
            PartyType.Organization,
            PartyRoleKind.Customer);
        var mergedPartyId = await CreatePartyAsync(
            partyDirectoryService,
            "Northwind Delivery Duplicate",
            "shared@northwind.example",
            PartyType.Organization,
            PartyRoleKind.Customer);

        var retainedRelationshipResult = await managementService.SaveRelationshipsAsync(
            retainedPartyId,
            [
                new PartyRelationshipEditorModel
                {
                    RelatedPartyId = parentPartyId,
                    RelationshipKind = PartyRelationshipKind.ManagedBy,
                    IsOutgoing = true,
                    Notes = "Retained relationship"
                }
            ],
            "integration-tests");
        var mergedRelationshipResult = await managementService.SaveRelationshipsAsync(
            mergedPartyId,
            [
                new PartyRelationshipEditorModel
                {
                    RelatedPartyId = parentPartyId,
                    RelationshipKind = PartyRelationshipKind.ManagedBy,
                    IsOutgoing = true,
                    IsPrimary = true,
                    Notes = "Merged relationship"
                }
            ],
            "integration-tests");

        Assert.True(retainedRelationshipResult.IsSuccess);
        Assert.True(mergedRelationshipResult.IsSuccess);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var interaction = new InteractionRecord
            {
                Subject = "Quarterly account review",
                InteractionType = InteractionType.Meeting,
                OccurredAtUtc = now,
                Summary = "Relationship follow-up.",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            dbContext.Set<InteractionRecord>().Add(interaction);
            dbContext.Set<InteractionPartyLink>().Add(new InteractionPartyLink
            {
                InteractionId = interaction.Id,
                PartyId = mergedPartyId,
                Role = InteractionPartyRole.Contact
            });
            dbContext.Set<Opportunity>().Add(new Opportunity
            {
                Title = "Northwind follow-up",
                Stage = OpportunityStage.Qualified,
                AccountPartyId = mergedPartyId,
                OwnerPartyId = mergedPartyId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            await dbContext.SaveChangesAsync();
        }

        var mergeResult = await managementService.MergePartyAsync(
            retainedPartyId,
            mergedPartyId,
            "integration-tests",
            "Duplicate cleanup");

        Assert.True(mergeResult.IsSuccess);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var remainingParties = await verificationContext.Set<Party>()
            .Where(item => item.Id == retainedPartyId || item.Id == mergedPartyId)
            .ToListAsync();
        var relationships = await verificationContext.Set<PartyRelationship>()
            .Where(item =>
                (item.SourcePartyId == retainedPartyId && item.TargetPartyId == parentPartyId)
                || (item.SourcePartyId == parentPartyId && item.TargetPartyId == retainedPartyId))
            .ToListAsync();
        var interactionLink = await verificationContext.Set<InteractionPartyLink>().SingleAsync();
        var opportunity = await verificationContext.Set<Opportunity>().SingleAsync();
        var contactPoints = await verificationContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == retainedPartyId)
            .ToListAsync();
        var auditEntry = await verificationContext.Set<CrmHrAuditEntry>()
            .SingleOrDefaultAsync(item => item.Action == "MergedDuplicate" && item.EntityId == retainedPartyId);

        Assert.Single(remainingParties, item => item.Id == retainedPartyId);
        Assert.DoesNotContain(remainingParties, item => item.Id == mergedPartyId);
        Assert.Equal(retainedPartyId, interactionLink.PartyId);
        Assert.Equal(retainedPartyId, opportunity.AccountPartyId);
        Assert.Equal(retainedPartyId, opportunity.OwnerPartyId);
        Assert.Single(relationships);
        Assert.True(relationships[0].IsPrimary);
        Assert.Contains("Retained relationship", relationships[0].Notes, StringComparison.Ordinal);
        Assert.Contains("Merged relationship", relationships[0].Notes, StringComparison.Ordinal);
        Assert.Single(contactPoints);
        Assert.NotNull(auditEntry);
    }

    [Fact]
    public async Task Merge_persons_deduplicates_profiles_and_affiliations_and_preserves_assignment_context()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider
            .GetRequiredService<PartyDirectoryService>();
        var managementService = scope.ServiceProvider
            .GetRequiredService<PartyDirectoryManagementService>();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var now = DateTimeOffset.UtcNow;

        var organizationId = await CreatePartyAsync(
            partyDirectoryService,
            "Contoso Services",
            "contact@contoso.example",
            PartyType.Organization,
            PartyRoleKind.Partner);
        var retainedPartyId = await CreatePartyAsync(
            partyDirectoryService,
            "Taylor Morgan",
            "taylor@contoso.example",
            PartyType.Person,
            PartyRoleKind.Employee);
        var mergedPartyId = await CreatePartyAsync(
            partyDirectoryService,
            "Taylor Morgan Duplicate",
            "taylor.duplicate@contoso.example",
            PartyType.Person,
            PartyRoleKind.Freelancer);

        Guid retainedAffiliationId;
        Guid mergedAffiliationId;
        Guid assignmentId;
        await using (var dbContext =
                     await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<WorkforceProfile>().AddRange(
                new WorkforceProfile
                {
                    PartyId = retainedPartyId,
                    WorkforceKind = WorkforceKind.Employee,
                    EmployeeCode = "EMP-101",
                    JobTitle = "Delivery lead",
                    Notes = "Retained profile"
                },
                new WorkforceProfile
                {
                    PartyId = mergedPartyId,
                    WorkforceKind = WorkforceKind.Freelancer,
                    Discipline = "Delivery",
                    Location = "Remote",
                    Notes = "Merged profile"
                });
            var retainedAffiliation = new PartyOrganizationAffiliation
            {
                PersonPartyId = retainedPartyId,
                OrganizationPartyId = organizationId,
                AffiliationKind = PartyOrganizationAffiliationKind.Employee,
                IsPrimary = true,
                JobTitle = "Delivery lead",
                EmployeeCode = "EMP-101",
                LastChangedBy = "integration-tests",
                CreatedAtUtc = now.AddDays(-2),
                UpdatedAtUtc = now.AddDays(-2)
            };
            var mergedAffiliation = new PartyOrganizationAffiliation
            {
                PersonPartyId = mergedPartyId,
                OrganizationPartyId = organizationId,
                AffiliationKind = PartyOrganizationAffiliationKind.Employee,
                IsPrimary = true,
                Notes = "Imported duplicate affiliation",
                LastChangedBy = "integration-tests",
                CreatedAtUtc = now.AddDays(-1),
                UpdatedAtUtc = now.AddDays(-1)
            };
            dbContext.Set<PartyOrganizationAffiliation>().AddRange(
                retainedAffiliation,
                mergedAffiliation);
            var assignmentToPersist = new ProjectPartyAssignment
            {
                ProjectId = Guid.NewGuid(),
                PartyId = mergedPartyId,
                PartyOrganizationAffiliationId = mergedAffiliation.Id,
                AssignmentKind = ProjectPartyAssignmentKind.TeamMember,
                Source = "integration-tests"
            };
            dbContext.Set<ProjectPartyAssignment>().Add(
                assignmentToPersist);
            await dbContext.SaveChangesAsync();
            retainedAffiliationId = retainedAffiliation.Id;
            mergedAffiliationId = mergedAffiliation.Id;
            assignmentId = assignmentToPersist.Id;
        }

        var mergeResult = await managementService.MergePartyAsync(
            retainedPartyId,
            mergedPartyId,
            "integration-tests",
            "Duplicate person cleanup");

        Assert.True(mergeResult.IsSuccess);

        await using var verificationContext =
            await dbContextFactory.CreateDbContextAsync();
        var profile = await verificationContext.Set<WorkforceProfile>()
            .SingleAsync(item => item.PartyId == retainedPartyId);
        var affiliation = await verificationContext
            .Set<PartyOrganizationAffiliation>()
            .SingleAsync(item => item.PersonPartyId == retainedPartyId);
        var assignment = await verificationContext
            .Set<ProjectPartyAssignment>()
            .SingleAsync(item => item.Id == assignmentId);

        Assert.Equal("EMP-101", profile.EmployeeCode);
        Assert.Equal("Delivery", profile.Discipline);
        Assert.Contains("Retained profile", profile.Notes, StringComparison.Ordinal);
        Assert.Contains("Merged profile", profile.Notes, StringComparison.Ordinal);
        Assert.Equal(retainedAffiliationId, affiliation.Id);
        Assert.True(affiliation.IsPrimary);
        Assert.Contains(
            "Imported duplicate affiliation",
            affiliation.Notes,
            StringComparison.Ordinal);
        Assert.Equal(retainedPartyId, assignment.PartyId);
        Assert.Equal(retainedAffiliationId, assignment.PartyOrganizationAffiliationId);
        Assert.NotEqual(mergedAffiliationId, assignment.PartyOrganizationAffiliationId);
    }

    [Fact]
    public async Task Merge_persons_stages_distinct_primary_affiliations_before_endpoint_rewrite()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider
            .GetRequiredService<PartyDirectoryService>();
        var managementService = scope.ServiceProvider
            .GetRequiredService<PartyDirectoryManagementService>();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var now = DateTimeOffset.UtcNow;

        var retainedOrganizationId = await CreatePartyAsync(
            partyDirectoryService,
            "Retained primary organization",
            "retained.organization@example.test",
            PartyType.Organization,
            PartyRoleKind.Partner);
        var mergedOrganizationId = await CreatePartyAsync(
            partyDirectoryService,
            "Merged primary organization",
            "merged.organization@example.test",
            PartyType.Organization,
            PartyRoleKind.Partner);
        var retainedPartyId = await CreatePartyAsync(
            partyDirectoryService,
            "Primary retained person",
            "primary.retained@example.test",
            PartyType.Person,
            PartyRoleKind.Employee);
        var mergedPartyId = await CreatePartyAsync(
            partyDirectoryService,
            "Primary merged person",
            "primary.merged@example.test",
            PartyType.Person,
            PartyRoleKind.Freelancer);

        Guid retainedAffiliationId;
        Guid mergedAffiliationId;
        Guid assignmentId;
        await using (var dbContext =
                     await dbContextFactory.CreateDbContextAsync())
        {
            var retainedAffiliation = new PartyOrganizationAffiliation
            {
                PersonPartyId = retainedPartyId,
                OrganizationPartyId = retainedOrganizationId,
                AffiliationKind = PartyOrganizationAffiliationKind.Employee,
                IsPrimary = true,
                LastChangedBy = "integration-tests",
                CreatedAtUtc = now.AddDays(-2),
                UpdatedAtUtc = now.AddDays(-2)
            };
            var mergedAffiliation = new PartyOrganizationAffiliation
            {
                PersonPartyId = mergedPartyId,
                OrganizationPartyId = mergedOrganizationId,
                AffiliationKind =
                    PartyOrganizationAffiliationKind.ExternalContact,
                IsPrimary = true,
                LastChangedBy = "integration-tests",
                CreatedAtUtc = now.AddDays(-1),
                UpdatedAtUtc = now.AddDays(-1)
            };
            dbContext.Set<PartyOrganizationAffiliation>().AddRange(
                retainedAffiliation,
                mergedAffiliation);
            var assignmentToPersist = new ProjectPartyAssignment
            {
                ProjectId = Guid.NewGuid(),
                PartyId = mergedPartyId,
                PartyOrganizationAffiliationId = mergedAffiliation.Id,
                AssignmentKind = ProjectPartyAssignmentKind.TeamMember,
                Source = "integration-tests"
            };
            dbContext.Set<ProjectPartyAssignment>().Add(
                assignmentToPersist);
            await dbContext.SaveChangesAsync();
            retainedAffiliationId = retainedAffiliation.Id;
            mergedAffiliationId = mergedAffiliation.Id;
            assignmentId = assignmentToPersist.Id;
        }

        var mergeResult = await managementService.MergePartyAsync(
            retainedPartyId,
            mergedPartyId,
            "integration-tests",
            "Duplicate person with distinct primary relationships");

        Assert.True(mergeResult.IsSuccess);

        await using var verificationContext =
            await dbContextFactory.CreateDbContextAsync();
        var affiliations = await verificationContext
            .Set<PartyOrganizationAffiliation>()
            .Where(item => item.PersonPartyId == retainedPartyId)
            .OrderBy(item => item.Id)
            .ToListAsync();
        var assignment = await verificationContext
            .Set<ProjectPartyAssignment>()
            .SingleAsync(item => item.Id == assignmentId);

        Assert.Equal(2, affiliations.Count);
        var primary = Assert.Single(
            affiliations,
            item => item.IsPrimary);
        Assert.Equal(retainedAffiliationId, primary.Id);
        Assert.Contains(
            affiliations,
            item =>
                item.Id == mergedAffiliationId &&
                !item.IsPrimary);
        Assert.Equal(retainedPartyId, assignment.PartyId);
        Assert.Equal(
            mergedAffiliationId,
            assignment.PartyOrganizationAffiliationId);
    }

    [Fact]
    public async Task Merge_party_rejects_cross_type_duplicates()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider
            .GetRequiredService<PartyDirectoryService>();
        var managementService = scope.ServiceProvider
            .GetRequiredService<PartyDirectoryManagementService>();

        var retainedPartyId = await CreatePartyAsync(
            partyDirectoryService,
            "Cross-type person",
            "cross-type.person@example.test",
            PartyType.Person,
            PartyRoleKind.Employee);
        var mergedPartyId = await CreatePartyAsync(
            partyDirectoryService,
            "Cross-type organization",
            "cross-type.organization@example.test",
            PartyType.Organization,
            PartyRoleKind.Customer);

        var mergeResult = await managementService.MergePartyAsync(
            retainedPartyId,
            mergedPartyId,
            "integration-tests",
            "Should not merge");

        Assert.False(mergeResult.IsSuccess);
        Assert.Contains(
            mergeResult.Errors,
            error => error.Code == "crmhr.party.merge-type-mismatch");
    }

    [Fact]
    public async Task Preview_apply_and_export_support_csv_directory_stewardship()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var managementService = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();

        var existingPartyId = await CreatePartyAsync(
            partyDirectoryService,
            "Existing Customer",
            "existing.customer@example.test",
            PartyType.Organization,
            PartyRoleKind.Customer);

        var export = await managementService.ExportPartiesCsvAsync([existingPartyId]);
        var csvContent = """
            DisplayName,PartyType,LifecycleStatus,ExternalCode,LegalName,PreferredName,Summary,Tags,Region,CountryCode,TimeZone,IsSensitive,Roles,ContactPoints,Addresses
            Imported Candidate,Person,Candidate,IMP-001,Imported Candidate LLC,Imported Candidate,Imported from CSV,imported,NA,US,America/Chicago,False,Candidate|Candidate|True,Email|Primary|imported.candidate@example.test|True|True,Work|100 Main Street||Chicago|IL|60601|US|True
            Duplicate Candidate,Person,Candidate,IMP-002,Duplicate Candidate LLC,Duplicate Candidate,Should be blocked,duplicate,NA,US,America/Chicago,False,Candidate|Candidate|True,Email|Primary|existing.customer@example.test|True|True,Work|200 Main Street||Chicago|IL|60601|US|True
            """;

        var previewResult = await managementService.PreviewImportAsync(csvContent);

        Assert.True(previewResult.IsSuccess);
        Assert.NotNull(previewResult.Value);
        Assert.Equal(2, previewResult.Value.Rows.Count);
        Assert.True(previewResult.Value.Rows[0].CanImport);
        Assert.False(previewResult.Value.Rows[1].CanImport);
        Assert.Contains(
            previewResult.Value.Rows[1].Messages,
            message => message.Contains("Potential duplicates detected", StringComparison.OrdinalIgnoreCase));

        var applyResult = await managementService.ApplyImportAsync(previewResult.Value.Rows, "integration-tests");
        var directory = await partyDirectoryService.ListDirectoryAsync();
        var importedParty = Assert.Single(directory, item => item.DisplayName == "Imported Candidate");

        Assert.True(applyResult.IsSuccess);
        Assert.Equal(1, applyResult.Value);
        Assert.Contains("Existing Customer", export, StringComparison.Ordinal);
        Assert.Equal("imported.candidate@example.test", importedParty.PrimaryEmail);
        Assert.DoesNotContain(directory, item => item.DisplayName == "Duplicate Candidate");
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        string email,
        PartyType partyType,
        PartyRoleKind roleKind)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
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
