using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CanDoItAll.Tests.Integration.Persistence;

public sealed class RecordQueryIntegrationTests
{
    [Fact]
    public async Task Party_query_applies_stable_source_paging_scope_and_conjunctive_tags()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("partyrecordquery");
        var interceptor = new QueryCommandInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>(database.CreateAppDbContextOptions())
            .AddInterceptors(interceptor)
            .Options;
        var factory = new TestDbContextFactory(options);

        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Set<Party>().AddRange(CreateParties());
            await dbContext.SaveChangesAsync();
        }

        var service = new PartyRecordQueryService(factory);
        interceptor.Clear();

        var lastPage = await service.SearchAsync(new PartyRecordQuery(
            Scope: PartyRecordScope.People,
            PageIndex: 25,
            PageSize: 40));
        var repeatedLastPage = await service.SearchAsync(new PartyRecordQuery(
            Scope: PartyRecordScope.People,
            PageIndex: 25,
            PageSize: 40));
        var peopleIncludingArchived = await service.SearchAsync(new PartyRecordQuery(
            Scope: PartyRecordScope.People,
            PageSize: 10,
            IncludeArchived: true));
        var archivedPartyId = DeterministicGuid(1_501);
        var hiddenArchivedParty = await service.GetAsync(archivedPartyId);
        var visibleArchivedParty = await service.GetAsync(
            archivedPartyId,
            includeArchived: true);
        var tagged = await service.SearchAsync(new PartyRecordQuery(
            SearchText: "special",
            Tags: ["delivery", "priority"],
            Scope: PartyRecordScope.All,
            PageSize: 10));

        Assert.Equal(1_001, lastPage.TotalCount);
        Assert.Single(lastPage.Items);
        Assert.Equal("Person 1000", lastPage.Items[0].DisplayName);
        Assert.Equivalent(lastPage.Items, repeatedLastPage.Items, strict: true);
        Assert.Equal(1_002, peopleIncludingArchived.TotalCount);
        Assert.Null(hiddenArchivedParty);
        Assert.Equal("Archived Person", visibleArchivedParty?.DisplayName);
        var taggedItem = Assert.Single(tagged.Items);
        Assert.Equal("Person 0500 special", taggedItem.DisplayName);
        Assert.Equal(PartyType.Person, taggedItem.PartyType);

        var pageCommands = interceptor.Commands
            .Where(command =>
                command.CommandText.Contains("CrmHr_Parties", StringComparison.Ordinal) &&
                command.CommandText.Contains("LIMIT", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("OFFSET", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Equal(4, pageCommands.Length);
        Assert.All(pageCommands, command =>
        {
            Assert.Contains("ORDER BY", command.CommandText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("OFFSET", command.CommandText, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Party_query_applies_sensitive_redaction_before_search_and_tag_predicates()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("partyrecordqueryprivacy");
        var options = database.CreateAppDbContextOptions();
        var factory = new TestDbContextFactory(options);
        var sensitivePartyId = DeterministicGuid(1_600);

        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Set<Party>().Add(new Party
            {
                Id = sensitivePartyId,
                PartyType = PartyType.Person,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = "Sensitive Directory Person",
                ExternalCode = "hidden-external-code",
                Summary = "hidden-summary-needle",
                TagsJson = JsonSerializer.Serialize(new[] { "hidden-tag" }),
                IsSensitive = true,
                CreatedAtUtc = DateTimeOffset.UnixEpoch,
                UpdatedAtUtc = DateTimeOffset.UnixEpoch
            });
            await dbContext.SaveChangesAsync();
        }

        var service = new PartyRecordQueryService(factory);

        var summarySearch = await service.SearchAsync(new PartyRecordQuery(
            SearchText: "hidden-summary-needle",
            Scope: PartyRecordScope.People));
        var externalCodeSearch = await service.SearchAsync(new PartyRecordQuery(
            SearchText: "hidden-external-code",
            Scope: PartyRecordScope.People));
        var tagSearch = await service.SearchAsync(new PartyRecordQuery(
            Tags: ["hidden-tag"],
            Scope: PartyRecordScope.People));
        var visibleNameSearch = await service.SearchAsync(new PartyRecordQuery(
            SearchText: "Sensitive Directory Person",
            Scope: PartyRecordScope.People));
        var directLookup = await service.GetAsync(sensitivePartyId);

        Assert.Empty(summarySearch.Items);
        Assert.Empty(externalCodeSearch.Items);
        Assert.Empty(tagSearch.Items);
        var visibleItem = Assert.Single(visibleNameSearch.Items);
        Assert.Equal(sensitivePartyId, visibleItem.Id);
        Assert.Empty(visibleItem.ExternalCode);
        Assert.Empty(visibleItem.Summary);
        Assert.Empty(visibleItem.Tags);
        Assert.NotNull(directLookup);
        Assert.Empty(directLookup.ExternalCode);
        Assert.Empty(directLookup.Summary);
        Assert.Empty(directLookup.Tags);
    }

    [Fact]
    public async Task Party_query_pages_only_workforce_candidates_when_workforce_population_is_requested()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("partyrecordqueryworkforce");
        var interceptor = new QueryCommandInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>(database.CreateAppDbContextOptions())
            .AddInterceptors(interceptor)
            .Options;
        var factory = new TestDbContextFactory(options);
        var personId = DeterministicGuid(1_700);
        var unitId = DeterministicGuid(1_701);
        var profiledOrganizationId = DeterministicGuid(1_702);
        var deliveryOrganizationId = DeterministicGuid(1_703);
        var unrelatedOrganizationId = DeterministicGuid(1_704);

        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Set<Party>().AddRange(
                CreateParty(personId, PartyType.Person, "Workforce person"),
                CreateParty(unitId, PartyType.OrganizationUnit, "Workforce unit"),
                CreateParty(profiledOrganizationId, PartyType.Organization, "Workforce profiled organization"),
                CreateParty(deliveryOrganizationId, PartyType.Organization, "Workforce role organization"),
                CreateParty(unrelatedOrganizationId, PartyType.Organization, "Unrelated organization"));
            dbContext.Set<WorkforceProfile>().Add(new WorkforceProfile
            {
                PartyId = profiledOrganizationId,
                WorkforceKind = WorkforceKind.DeliveryUnit,
                Status = "Active"
            });
            dbContext.Set<PartyRoleAssignment>().Add(new PartyRoleAssignment
            {
                PartyId = deliveryOrganizationId,
                RoleKind = PartyRoleKind.DeliveryUnit,
                Title = "Delivery unit",
                IsPrimary = true
            });
            await dbContext.SaveChangesAsync();
        }

        var service = new PartyRecordQueryService(factory);
        interceptor.Clear();

        var result = await service.SearchAsync(new PartyRecordQuery(
            Scope: PartyRecordScope.All,
            PageSize: 10,
            Population: PartyRecordPopulation.Workforce));

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(
            [
                personId,
                profiledOrganizationId,
                deliveryOrganizationId,
                unitId
            ],
            result.Items.Select(item => item.Id));
        Assert.DoesNotContain(result.Items, item => item.Id == unrelatedOrganizationId);
        Assert.Contains(
            interceptor.Commands,
            command =>
                command.CommandText.Contains("CrmHr_WorkforceProfiles", StringComparison.Ordinal) &&
                command.CommandText.Contains("CrmHr_PartyRoles", StringComparison.Ordinal) &&
                command.CommandText.Contains("LIMIT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Project_query_searches_and_pages_in_the_projects_boundary()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("projectrecordquery");
        var interceptor = new QueryCommandInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>(database.CreateAppDbContextOptions())
            .AddInterceptors(interceptor)
            .Options;
        var factory = new TestDbContextFactory(options);

        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Set<Project>().AddRange(
                Enumerable.Range(0, 65)
                    .Select(index => new Project
                    {
                        Id = DeterministicGuid(2_000 + index),
                        Name = $"Project {index:D3}",
                        Slug = $"project-{index:D3}",
                        Description = index == 62 ? "Needle delivery program" : "Program",
                        Objective = "Delivery",
                        Status = index % 2 == 0 ? ProjectStatus.Active : ProjectStatus.Completed,
                        CurrentPhase = "Execution",
                        CreatedAtUtc = DateTimeOffset.UnixEpoch,
                        UpdatedAtUtc = DateTimeOffset.UnixEpoch.AddDays(index)
                    }));
            await dbContext.SaveChangesAsync();
        }

        var service = new ProjectRecordQueryService(factory);
        interceptor.Clear();

        var result = await service.SearchAsync(new ProjectRecordQuery(
            SearchText: "needle",
            Scope: ProjectRecordScope.Active,
            PageSize: 12));
        var direct = await service.GetAsync(DeterministicGuid(2_062));
        var selected = await service.GetManyAsync(
            [DeterministicGuid(2_061), DeterministicGuid(2_062), Guid.NewGuid()]);

        var item = Assert.Single(result.Items);
        Assert.Equal("Project 062", item.Name);
        Assert.Equal(ProjectStatus.Active, item.Status);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Project 062", direct?.Name);
        Assert.Equal(["Project 061", "Project 062"], selected.Select(project => project.Name));
        Assert.Contains(
            interceptor.Commands,
            command =>
                command.CommandText.Contains("Projects_Projects", StringComparison.Ordinal) &&
                command.CommandText.Contains("LIMIT", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("OFFSET", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Opportunity_query_scopes_filters_and_pages_before_materialization()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("opportunitypipelinequery");
        var interceptor = new QueryCommandInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>(database.CreateAppDbContextOptions())
            .AddInterceptors(interceptor)
            .Options;
        var factory = new TestDbContextFactory(options);
        var accountId = DeterministicGuid(3_000);
        var otherAccountId = DeterministicGuid(3_001);
        var ownerId = DeterministicGuid(3_002);
        var otherOwnerId = DeterministicGuid(3_003);
        var partnerId = DeterministicGuid(3_004);

        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Set<Party>().AddRange(
                CreateParty(accountId, PartyType.Organization, "Account A"),
                CreateParty(otherAccountId, PartyType.Organization, "Account B"),
                CreateParty(ownerId, PartyType.Person, "Owner A"),
                CreateParty(otherOwnerId, PartyType.Person, "Owner B"),
                CreateParty(partnerId, PartyType.Organization, "Partner"));

            var opportunities = Enumerable.Range(0, 65)
                .Select(index => new Opportunity
                {
                    Id = DeterministicGuid(4_000 + index),
                    AccountPartyId = accountId,
                    OwnerPartyId = index == 62 ? ownerId : otherOwnerId,
                    Title = index == 62 ? "Needle proposal" : $"Opportunity {index:D3}",
                    Summary = index == 62 ? "Priority partner pursuit" : "Pipeline record",
                    Stage = index == 62 ? OpportunityStage.Proposal : OpportunityStage.Identified,
                    OpportunitySource = index == 62 ? OpportunitySource.Partner : OpportunitySource.Direct,
                    CurrencyCode = index % 2 == 0 ? "USD" : "EUR",
                    Amount = 1_000m + index,
                    CreatedAtUtc = DateTimeOffset.UnixEpoch,
                    UpdatedAtUtc = DateTimeOffset.UnixEpoch.AddDays(index)
                })
                .ToList();
            dbContext.Set<Opportunity>().AddRange(opportunities);
            dbContext.Set<Opportunity>().Add(new Opportunity
            {
                Id = DeterministicGuid(5_000),
                AccountPartyId = otherAccountId,
                OwnerPartyId = ownerId,
                Title = "Needle proposal for other account",
                Summary = "Must remain out of scope",
                Stage = OpportunityStage.Proposal,
                OpportunitySource = OpportunitySource.Partner,
                CurrencyCode = "USD",
                Amount = 99_999m,
                CreatedAtUtc = DateTimeOffset.UnixEpoch,
                UpdatedAtUtc = DateTimeOffset.UnixEpoch
            });
            dbContext.Set<OpportunityPartyLink>().Add(new OpportunityPartyLink
            {
                Id = DeterministicGuid(6_000),
                OpportunityId = opportunities[62].Id,
                PartyId = partnerId,
                Role = OpportunityPartyRole.Partner
            });
            await dbContext.SaveChangesAsync();
        }

        var service = new OpportunityPipelineQueryService(factory);
        interceptor.Clear();

        var secondPage = await service.SearchAsync(new OpportunityPipelineQuery(
            accountId,
            PageIndex: 1,
            PageSize: 24));
        var filtered = await service.SearchAsync(new OpportunityPipelineQuery(
            accountId,
            SearchText: "needle",
            Stage: OpportunityStage.Proposal,
            OwnerPartyId: ownerId,
            PartnerPartyId: partnerId,
            Source: OpportunitySource.Partner,
            PageSize: 10));

        Assert.Equal(65, secondPage.TotalCount);
        Assert.Equal(24, secondPage.Items.Count);
        Assert.All(secondPage.Items, item => Assert.Equal(accountId, item.AccountPartyId));
        var match = Assert.Single(filtered.Items);
        Assert.Equal("Needle proposal", match.Title);
        Assert.Equal("USD", match.CurrencyCode);
        Assert.Equal(1, filtered.TotalCount);
        Assert.Contains(
            interceptor.Commands,
            command =>
                command.CommandText.Contains("CrmHr_Opportunities", StringComparison.Ordinal) &&
                command.CommandText.Contains("LIMIT", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("OFFSET", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<Party> CreateParties()
    {
        var parties = Enumerable.Range(0, 1_001)
            .Select(index => new Party
            {
                Id = DeterministicGuid(index),
                PartyType = PartyType.Person,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = $"Person {index:D4}",
                ExternalCode = $"P-{index:D4}",
                Summary = "Paged person",
                TagsJson = JsonSerializer.Serialize(new[] { "directory" }),
                CreatedAtUtc = DateTimeOffset.UnixEpoch,
                UpdatedAtUtc = DateTimeOffset.UnixEpoch
            })
            .ToList();
        parties[500].DisplayName = "Person 0500 special";
        parties[500].TagsJson = JsonSerializer.Serialize(new[] { "delivery", "priority" });
        parties.Add(new Party
        {
            Id = DeterministicGuid(1_500),
            PartyType = PartyType.Organization,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = "Special Organization",
            TagsJson = JsonSerializer.Serialize(new[] { "delivery" }),
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch
        });
        parties.Add(new Party
        {
            Id = DeterministicGuid(1_501),
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Archived,
            DisplayName = "Archived Person",
            TagsJson = JsonSerializer.Serialize(new[] { "archive" }),
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch
        });
        return parties;
    }

    private static Party CreateParty(
        Guid id,
        PartyType partyType,
        string displayName)
    {
        return new Party
        {
            Id = id,
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch
        };
    }

    private static Guid DeterministicGuid(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        return new Guid(bytes);
    }

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
        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(options);
        }
    }
}
