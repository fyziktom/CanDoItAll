using System.Collections.Concurrent;
using System.Data.Common;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CanDoItAll.Tests.Integration;

public sealed class PartyOrganizationAffiliationIntegrationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Affiliation_service_validates_endpoints_normalizes_primary_and_preserves_history()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("partyaffiliationservice");
        var factory = new TestDbContextFactory(database.CreateAppDbContextOptions());
        var personId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Set<Party>().AddRange(
                CreateParty(personId, PartyType.Person, "Ari Person"),
                CreateParty(organizationId, PartyType.Organization, "Alpha Company"),
                CreateParty(secondOrganizationId, PartyType.Organization, "Beta Company"),
                CreateParty(unitId, PartyType.OrganizationUnit, "Delivery North"),
                CreateParty(managerId, PartyType.Person, "Morgan Manager"));
            await dbContext.SaveChangesAsync();
        }

        var service = new PartyOrganizationAffiliationService(
            factory,
            new FixedClock(Now));
        var invalidEndpoint = await service.UpsertAsync(
            new PartyOrganizationAffiliationEditorModel
            {
                PersonPartyId = personId,
                OrganizationPartyId = unitId,
                AffiliationKind = PartyOrganizationAffiliationKind.Employee
            },
            "integration-tests");
        Assert.False(invalidEndpoint.IsSuccess);
        Assert.Contains(
            invalidEndpoint.Errors,
            error => error.Code == "crmhr.affiliation.organization-invalid");

        var firstSave = await service.UpsertAsync(
            new PartyOrganizationAffiliationEditorModel
            {
                PersonPartyId = personId,
                OrganizationPartyId = organizationId,
                OrganizationUnitPartyId = unitId,
                ManagerPartyId = managerId,
                AffiliationKind = PartyOrganizationAffiliationKind.Employee,
                IsPrimary = true,
                JobTitle = "Engineer",
                EmployeeCode = "SENSITIVE-CODE",
                ValidFrom = new DateOnly(2025, 1, 1)
            },
            "integration-tests");
        Assert.True(firstSave.IsSuccess);
        Assert.True(firstSave.Value?.IsPrimary is true);

        var secondSave = await service.UpsertAsync(
            new PartyOrganizationAffiliationEditorModel
            {
                PersonPartyId = personId,
                OrganizationPartyId = secondOrganizationId,
                AffiliationKind = PartyOrganizationAffiliationKind.Contractor,
                IsPrimary = true,
                JobTitle = "Delivery lead",
                ValidFrom = new DateOnly(2026, 1, 1)
            },
            "integration-tests");
        Assert.True(secondSave.IsSuccess);

        var affiliations = await service.ListAsync(personId);
        Assert.Equal(2, affiliations.Count);
        var currentPrimary = Assert.Single(affiliations, item => item.IsPrimary);
        Assert.Equal(secondSave.Value!.Id, currentPrimary.Id);
        Assert.False(affiliations.Single(item => item.Id == firstSave.Value?.Id).IsPrimary);

        var duplicate = await service.UpsertAsync(
            new PartyOrganizationAffiliationEditorModel
            {
                PersonPartyId = personId,
                OrganizationPartyId = secondOrganizationId,
                AffiliationKind = PartyOrganizationAffiliationKind.Contractor,
                ValidFrom = new DateOnly(2026, 1, 1)
            },
            "integration-tests");
        Assert.False(duplicate.IsSuccess);
        Assert.Contains(
            duplicate.Errors,
            error => error.Code == "crmhr.affiliation.duplicate");

        var missingVersionEditors = affiliations
            .Select(ToEditor)
            .ToList();
        missingVersionEditors[0].ExpectedUpdatedAtUtc = null;
        var missingVersion = await service.ReplaceAsync(
            personId,
            missingVersionEditors,
            "integration-tests");
        Assert.False(missingVersion.IsSuccess);
        Assert.Contains(
            missingVersion.Errors,
            error => error.Code ==
                     "crmhr.affiliation.concurrency-version-required");

        var staleVersionEditors = affiliations
            .Select(ToEditor)
            .ToList();
        staleVersionEditors[0].ExpectedUpdatedAtUtc =
            staleVersionEditors[0].ExpectedUpdatedAtUtc?.AddMinutes(-1);
        var staleReplacement = await service.ReplaceAsync(
            personId,
            staleVersionEditors,
            "integration-tests");
        Assert.False(staleReplacement.IsSuccess);
        Assert.Contains(
            staleReplacement.Errors,
            error => error.Code == "crmhr.affiliation.concurrency-conflict");

        var removal = await service.ReplaceAsync(
            personId,
            [
                ToEditor(currentPrimary)
            ],
            "integration-tests");
        Assert.False(removal.IsSuccess);
        Assert.Contains(
            removal.Errors,
            error => error.Code == "crmhr.affiliation.historical-removal-not-allowed");

        var staleUpdate = await service.UpsertAsync(
            ToEditor(currentPrimary),
            "integration-tests",
            currentPrimary.UpdatedAtUtc.AddMinutes(-1));
        Assert.False(staleUpdate.IsSuccess);
        Assert.Contains(
            staleUpdate.Errors,
            error => error.Code == "crmhr.affiliation.concurrency-conflict");

        await using var verificationContext = factory.CreateDbContext();
        var audits = await verificationContext.Set<CrmHrAuditEntry>()
            .AsNoTracking()
            .Where(entry =>
                entry.EntityType == nameof(PartyOrganizationAffiliation) &&
                entry.EntityId == personId)
            .OrderBy(entry => entry.CreatedAtUtc)
            .ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.All(audits, audit => Assert.Equal("integration-tests", audit.Actor));
        Assert.DoesNotContain(
            audits,
            audit => audit.DetailJson.Contains("SENSITIVE-CODE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Affiliation_upsert_rejects_person_limit_before_writing()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("partyaffiliationlimit");
        var factory = new TestDbContextFactory(database.CreateAppDbContextOptions());
        var personId = Guid.NewGuid();
        var organizationIds = Enumerable
            .Range(
                0,
                PartyOrganizationAffiliationLimits.MaximumAffiliationsPerPerson + 1)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Set<Party>().Add(
                CreateParty(personId, PartyType.Person, "Bounded Person"));
            dbContext.Set<Party>().AddRange(
                organizationIds.Select((id, index) =>
                    CreateParty(id, PartyType.Organization, $"Company {index:000}")));
            dbContext.Set<PartyOrganizationAffiliation>().AddRange(
                organizationIds
                    .Take(
                        PartyOrganizationAffiliationLimits
                            .MaximumAffiliationsPerPerson)
                    .Select(organizationId =>
                        CreateAffiliation(personId, organizationId)));
            await dbContext.SaveChangesAsync();
        }

        var service = new PartyOrganizationAffiliationService(
            factory,
            new FixedClock(Now));
        var result = await service.UpsertAsync(
            new PartyOrganizationAffiliationEditorModel
            {
                PersonPartyId = personId,
                OrganizationPartyId = organizationIds[^1],
                AffiliationKind =
                    PartyOrganizationAffiliationKind.ExternalContact
            },
            "integration-tests");

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            error => error.Code == "crmhr.affiliation.limit-exceeded");
        await using var verificationContext = factory.CreateDbContext();
        Assert.Equal(
            PartyOrganizationAffiliationLimits.MaximumAffiliationsPerPerson,
            await verificationContext.Set<PartyOrganizationAffiliation>()
                .AsNoTracking()
                .CountAsync(item => item.PersonPartyId == personId));
    }

    [Fact]
    public async Task PostgreSql_model_enforces_profile_primary_duplicate_and_assignment_constraints()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("partyaffiliationconstraints");
        var options = database.CreateAppDbContextOptions();
        var personId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await using (var dbContext = new AppDbContext(options))
        {
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Set<Party>().AddRange(
                CreateParty(personId, PartyType.Person, "Constraint Person"),
                CreateParty(organizationId, PartyType.Organization, "Constraint Company"));
            dbContext.Set<Project>().Add(new Project
            {
                Id = projectId,
                Name = "Affiliation constraint project",
                Slug = $"affiliation-constraint-{projectId:N}",
                Description = "Verifies the assignment affiliation foreign key.",
                Objective = "Reject a missing affiliation reference.",
                Status = ProjectStatus.Active,
                CurrentPhase = "Validation",
                CreatedAtUtc = Now,
                UpdatedAtUtc = Now
            });
            await dbContext.SaveChangesAsync();
        }

        await using (var dbContext = new AppDbContext(options))
        {
            dbContext.Set<WorkforceProfile>().AddRange(
                new WorkforceProfile
                {
                    PartyId = personId,
                    WorkforceKind = WorkforceKind.Employee
                },
                new WorkforceProfile
                {
                    PartyId = personId,
                    WorkforceKind = WorkforceKind.Contractor
                });
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                dbContext.SaveChangesAsync());
        }

        await using (var dbContext = new AppDbContext(options))
        {
            dbContext.Set<PartyOrganizationAffiliation>().AddRange(
                CreateAffiliation(personId, organizationId, isPrimary: true),
                CreateAffiliation(
                    personId,
                    organizationId,
                    isPrimary: true,
                    kind: PartyOrganizationAffiliationKind.Contractor));
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                dbContext.SaveChangesAsync());
        }

        await using (var dbContext = new AppDbContext(options))
        {
            dbContext.Set<PartyOrganizationAffiliation>().AddRange(
                CreateAffiliation(personId, organizationId),
                CreateAffiliation(personId, organizationId));
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                dbContext.SaveChangesAsync());
        }

        await using (var dbContext = new AppDbContext(options))
        {
            dbContext.Set<ProjectPartyAssignment>().Add(new ProjectPartyAssignment
            {
                ProjectId = projectId,
                PartyId = personId,
                PartyOrganizationAffiliationId = Guid.NewGuid(),
                AssignmentKind = ProjectPartyAssignmentKind.TeamMember
            });
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                dbContext.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Migration_exposes_duplicate_profiles_without_deleting_them()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("partyaffiliationmigration");
        var options = database.CreateAppDbContextOptions();
        var duplicatePartyId = Guid.NewGuid();

        await using (var baselineContext = new AppDbContext(options))
        {
            await baselineContext.Database.MigrateAsync(
                "20260728161028_InitialPostgreSqlBaseline");
            baselineContext.Set<WorkforceProfile>().AddRange(
                new WorkforceProfile
                {
                    PartyId = duplicatePartyId,
                    WorkforceKind = WorkforceKind.Employee
                },
                new WorkforceProfile
                {
                    PartyId = duplicatePartyId,
                    WorkforceKind = WorkforceKind.Contractor
                });
            await baselineContext.SaveChangesAsync();
        }

        await using (var migrationContext = new AppDbContext(options))
        {
            await Assert.ThrowsAnyAsync<DbException>(() =>
                migrationContext.Database.MigrateAsync());
        }

        await using var verificationContext = new AppDbContext(options);
        Assert.Equal(
            2,
            await verificationContext.Set<WorkforceProfile>()
                .AsNoTracking()
                .CountAsync(profile => profile.PartyId == duplicatePartyId));
        Assert.DoesNotContain(
            "20260729095105_AddPartyOrganizationAffiliations",
            await verificationContext.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Migration_seeds_only_unambiguous_profile_and_membership_evidence()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("partyaffiliationseed");
        var options = database.CreateAppDbContextOptions();
        var directPersonId = Guid.NewGuid();
        var unitPersonId = Guid.NewGuid();
        var membershipPersonId = Guid.NewGuid();
        var ambiguousMembershipPersonId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();
        var organizationUnitId = Guid.NewGuid();

        await using (var baselineContext = new AppDbContext(options))
        {
            await baselineContext.Database.MigrateAsync(
                "20260728161028_InitialPostgreSqlBaseline");
            baselineContext.Set<Party>().AddRange(
                CreateParty(directPersonId, PartyType.Person, "Direct Person"),
                CreateParty(unitPersonId, PartyType.Person, "Unit Person"),
                CreateParty(
                    membershipPersonId,
                    PartyType.Person,
                    "Membership Person"),
                CreateParty(
                    ambiguousMembershipPersonId,
                    PartyType.Person,
                    "Ambiguous Membership Person"),
                CreateParty(organizationId, PartyType.Organization, "Direct Company"),
                CreateParty(
                    secondOrganizationId,
                    PartyType.Organization,
                    "Second Company"),
                CreateParty(
                    organizationUnitId,
                    PartyType.OrganizationUnit,
                    "Unresolved Unit"));
            baselineContext.Set<WorkforceProfile>().AddRange(
                new WorkforceProfile
                {
                    PartyId = directPersonId,
                    HomeUnitPartyId = organizationId,
                    WorkforceKind = WorkforceKind.Employee,
                    JobTitle = "Engineer",
                    EmployeeCode = "EMP-42"
                },
                new WorkforceProfile
                {
                    PartyId = unitPersonId,
                    HomeUnitPartyId = organizationUnitId,
                    WorkforceKind = WorkforceKind.Contractor,
                    JobTitle = "Consultant"
                });
            baselineContext.Set<PartyRelationship>().AddRange(
                new PartyRelationship
                {
                    SourcePartyId = organizationUnitId,
                    TargetPartyId = organizationId,
                    RelationshipKind = PartyRelationshipKind.PartOf
                },
                new PartyRelationship
                {
                    SourcePartyId = membershipPersonId,
                    TargetPartyId = organizationId,
                    RelationshipKind = PartyRelationshipKind.MemberOf
                },
                new PartyRelationship
                {
                    SourcePartyId = ambiguousMembershipPersonId,
                    TargetPartyId = organizationId,
                    RelationshipKind = PartyRelationshipKind.MemberOf
                },
                new PartyRelationship
                {
                    SourcePartyId = ambiguousMembershipPersonId,
                    TargetPartyId = secondOrganizationId,
                    RelationshipKind = PartyRelationshipKind.MemberOf
                });
            baselineContext.Set<PartyRoleAssignment>().AddRange(
                new PartyRoleAssignment
                {
                    PartyId = membershipPersonId,
                    RoleKind = PartyRoleKind.Freelancer,
                    Title = "Independent specialist"
                },
                new PartyRoleAssignment
                {
                    PartyId = ambiguousMembershipPersonId,
                    RoleKind = PartyRoleKind.Employee,
                    Title = "Ambiguous employee"
                });
            await baselineContext.SaveChangesAsync();
        }

        await using (var migrationContext = new AppDbContext(options))
        {
            await migrationContext.Database.MigrateAsync();
        }

        await using var verificationContext = new AppDbContext(options);
        var seeded = await verificationContext
            .Set<PartyOrganizationAffiliation>()
            .AsNoTracking()
            .OrderBy(item => item.PersonPartyId)
            .ToListAsync();
        Assert.Equal(3, seeded.Count);

        var direct = seeded.Single(item => item.PersonPartyId == directPersonId);
        Assert.Equal(organizationId, direct.OrganizationPartyId);
        Assert.Equal(PartyOrganizationAffiliationKind.Employee, direct.AffiliationKind);
        Assert.True(direct.IsPrimary);
        Assert.Equal("Engineer", direct.JobTitle);
        Assert.Equal("EMP-42", direct.EmployeeCode);

        var resolvedUnit = seeded.Single(item => item.PersonPartyId == unitPersonId);
        Assert.Equal(organizationId, resolvedUnit.OrganizationPartyId);
        Assert.Equal(organizationUnitId, resolvedUnit.OrganizationUnitPartyId);
        Assert.Equal(
            PartyOrganizationAffiliationKind.Contractor,
            resolvedUnit.AffiliationKind);

        var membership = seeded.Single(
            item => item.PersonPartyId == membershipPersonId);
        Assert.Equal(
            PartyOrganizationAffiliationKind.Freelancer,
            membership.AffiliationKind);
        Assert.Equal("Independent specialist", membership.JobTitle);
        Assert.DoesNotContain(
            seeded,
            item => item.PersonPartyId == ambiguousMembershipPersonId);
    }

    [Fact]
    public async Task Workforce_query_pages_at_source_classifies_safely_and_stays_within_query_budget()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("workforcerecordquery");
        var interceptor = new QueryCommandInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>(
                database.CreateAppDbContextOptions())
            .AddInterceptors(interceptor)
            .Options;
        var factory = new TestDbContextFactory(options);
        var contractorId = Guid.NewGuid();
        var freelancerId = Guid.NewGuid();
        var historicalId = Guid.NewGuid();
        var externalId = Guid.NewGuid();
        var deliveryUnitId = Guid.NewGuid();
        var alphaOrganizationId = Guid.NewGuid();
        var betaOrganizationId = Guid.NewGuid();

        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Set<Party>().AddRange(
                CreateParty(contractorId, PartyType.Person, "Alpha Contractor"),
                CreateParty(freelancerId, PartyType.Person, "Beta Freelancer"),
                CreateParty(historicalId, PartyType.Person, "Gamma Historical"),
                CreateParty(externalId, PartyType.Person, "Delta External"),
                CreateParty(deliveryUnitId, PartyType.OrganizationUnit, "Echo Delivery"),
                CreateParty(alphaOrganizationId, PartyType.Organization, "Alpha Company"),
                CreateParty(betaOrganizationId, PartyType.Organization, "Beta Company"));
            dbContext.Set<WorkforceProfile>().Add(new WorkforceProfile
            {
                PartyId = contractorId,
                WorkforceKind = WorkforceKind.Employee,
                JobTitle = "Legacy employee"
            });
            dbContext.Set<PartyOrganizationAffiliation>().AddRange(
                new PartyOrganizationAffiliation
                {
                    PersonPartyId = contractorId,
                    OrganizationPartyId = alphaOrganizationId,
                    AffiliationKind = PartyOrganizationAffiliationKind.Contractor,
                    IsPrimary = true,
                    JobTitle = "Contractor lead",
                    CreatedAtUtc = Now.AddDays(-2),
                    UpdatedAtUtc = Now.AddDays(-2)
                },
                new PartyOrganizationAffiliation
                {
                    PersonPartyId = contractorId,
                    OrganizationPartyId = betaOrganizationId,
                    AffiliationKind = PartyOrganizationAffiliationKind.ExternalContact,
                    JobTitle = "Adviser",
                    CreatedAtUtc = Now.AddDays(-1),
                    UpdatedAtUtc = Now.AddDays(-1)
                },
                new PartyOrganizationAffiliation
                {
                    PersonPartyId = freelancerId,
                    OrganizationPartyId = betaOrganizationId,
                    AffiliationKind = PartyOrganizationAffiliationKind.Freelancer,
                    JobTitle = "Designer",
                    CreatedAtUtc = Now.AddDays(-1),
                    UpdatedAtUtc = Now.AddDays(-1)
                },
                new PartyOrganizationAffiliation
                {
                    PersonPartyId = historicalId,
                    OrganizationPartyId = alphaOrganizationId,
                    AffiliationKind = PartyOrganizationAffiliationKind.Employee,
                    ValidToUtc = new DateTimeOffset(
                        2026,
                        7,
                        28,
                        0,
                        0,
                        0,
                        TimeSpan.Zero),
                    CreatedAtUtc = Now.AddYears(-1),
                    UpdatedAtUtc = Now.AddDays(-1)
                });
            dbContext.Set<PartyRelationship>().Add(new PartyRelationship
            {
                SourcePartyId = externalId,
                TargetPartyId = alphaOrganizationId,
                RelationshipKind = PartyRelationshipKind.Represents,
                IsPrimary = true
            });
            await dbContext.SaveChangesAsync();
        }

        var service = new WorkforceRecordQueryService(
            factory,
            new FixedClock(Now));
        interceptor.Clear();

        var page = await service.SearchAsync(new WorkforceRecordQuery(PageSize: 10));

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(5, page.Items.Count);
        var contractor = page.Items.Single(item => item.PartyId == contractorId);
        Assert.Equal(WorkforceRecordClassification.Contractor, contractor.Classification);
        Assert.True(contractor.HasWorkforceProfile);
        Assert.Equal("Alpha Company — Contractor lead", contractor.PrimaryAffiliationText);
        Assert.Single(contractor.OtherCurrentAffiliations);
        Assert.Equal(
            "Beta Company — Adviser",
            contractor.OtherCurrentAffiliations[0].DisplayText);

        var freelancer = page.Items.Single(item => item.PartyId == freelancerId);
        Assert.Equal(WorkforceRecordClassification.Freelancer, freelancer.Classification);
        Assert.True(freelancer.PrimaryAffiliation?.IsPrimary is false);
        Assert.Equal(
            WorkforceRecordClassification.ExternalContact,
            page.Items.Single(item => item.PartyId == historicalId).Classification);
        var external = page.Items.Single(item => item.PartyId == externalId);
        Assert.Equal(WorkforceRecordClassification.ExternalContact, external.Classification);
        Assert.Equal("Related to Alpha Company", external.PrimaryAffiliationText);
        Assert.Equal(
            WorkforceRecordClassification.DeliveryUnit,
            page.Items.Single(item => item.PartyId == deliveryUnitId).Classification);

        Assert.Equal(5, interceptor.Commands.Count);
        Assert.Contains(
            interceptor.Commands,
            command =>
                command.CommandText.Contains("CrmHr_Parties", StringComparison.Ordinal) &&
                command.CommandText.Contains("LIMIT", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("OFFSET", StringComparison.OrdinalIgnoreCase));

        interceptor.Clear();
        var filtered = await service.SearchAsync(new WorkforceRecordQuery(
            Classification: WorkforceRecordClassification.Contractor,
            PageSize: 10));
        var filteredItem = Assert.Single(filtered.Items);
        Assert.Equal(contractorId, filteredItem.PartyId);
        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal(5, interceptor.Commands.Count);

        interceptor.Clear();
        var affiliationSearch = await service.SearchAsync(new WorkforceRecordQuery(
            SearchText: "Contractor lead",
            PageSize: 10));
        var searchMatch = Assert.Single(affiliationSearch.Items);
        Assert.Equal(contractorId, searchMatch.PartyId);
        Assert.Equal(1, affiliationSearch.TotalCount);
        Assert.Equal(5, interceptor.Commands.Count);
    }

    private static PartyOrganizationAffiliation CreateAffiliation(
        Guid personId,
        Guid organizationId,
        bool isPrimary = false,
        PartyOrganizationAffiliationKind kind =
            PartyOrganizationAffiliationKind.Employee)
        => new()
        {
            PersonPartyId = personId,
            OrganizationPartyId = organizationId,
            AffiliationKind = kind,
            IsPrimary = isPrimary,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };

    private static Party CreateParty(
        Guid id,
        PartyType partyType,
        string displayName)
        => new()
        {
            Id = id,
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            CreatedAtUtc = Now.AddYears(-1),
            UpdatedAtUtc = Now
        };

    private static PartyOrganizationAffiliationEditorModel ToEditor(
        PartyOrganizationAffiliationListItemModel item)
        => new()
        {
            Id = item.Id,
            PersonPartyId = item.PersonPartyId,
            OrganizationPartyId = item.OrganizationPartyId,
            AffiliationKind = item.AffiliationKind,
            IsPrimary = item.IsPrimary,
            JobTitle = item.JobTitle,
            EmployeeCode = item.EmployeeCode,
            OrganizationUnitPartyId = item.OrganizationUnitPartyId,
            ManagerPartyId = item.ManagerPartyId,
            ValidFrom = item.ValidFrom,
            ValidTo = item.ValidTo,
            Notes = item.Notes,
            ExpectedUpdatedAtUtc = item.UpdatedAtUtc
        };

    private sealed record CapturedCommand(string CommandText);

    private sealed class QueryCommandInterceptor : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<CapturedCommand> commands = new();

        public IReadOnlyList<CapturedCommand> Commands => commands.ToArray();

        public void Clear()
        {
            while (commands.TryDequeue(out _))
            {
            }
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            commands.Enqueue(new CapturedCommand(command.CommandText));
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset GetUtcNow() => now;
    }
}
