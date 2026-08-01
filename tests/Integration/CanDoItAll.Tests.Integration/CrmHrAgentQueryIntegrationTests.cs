using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmHrAgentQueryIntegrationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-29T12:00:00Z");

    [Fact]
    public async Task PostgreSql_search_filters_before_projection_for_typed_and_all_kind_queries()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("crmhragentquery");
        var factory = new TestDbContextFactory(database.CreateAppDbContextOptions());
        var visiblePartyId = Guid.Parse("1eaf0201-cbe8-4359-92f4-2bd2c263e8d7");

        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Set<Party>().AddRange(
                new Party
                {
                    Id = visiblePartyId,
                    PartyType = PartyType.Organization,
                    LifecycleStatus = PartyLifecycleStatus.Active,
                    DisplayName = "Development CRM Search Organization",
                    Summary = "Safe organization summary",
                    TagsJson = "[]",
                    CreatedAtUtc = Now,
                    UpdatedAtUtc = Now
                },
                new Party
                {
                    PartyType = PartyType.Organization,
                    LifecycleStatus = PartyLifecycleStatus.Active,
                    DisplayName = "Restricted CRM Search Organization",
                    Summary = "Sensitive organization summary",
                    TagsJson = "[]",
                    IsSensitive = true,
                    CreatedAtUtc = Now,
                    UpdatedAtUtc = Now
                });
            await dbContext.SaveChangesAsync();
        }

        var service = new CrmHrAgentQueryService(factory, new FixedClock(Now));

        var typedResult = await service.SearchAsync(new CrmHrAgentSearchQuery(
            "Development CRM Search Organization",
            CrmHrAgentRecordKind.Party,
            Take: 1));
        var allKindsResult = await service.SearchAsync(new CrmHrAgentSearchQuery(
            "Development CRM Search Organization",
            Take: 10));
        var summaryResult = await service.GetSummaryAsync(new CrmHrAgentItemReference(
            CrmHrAgentRecordKind.Party,
            visiblePartyId));
        var sensitiveResult = await service.SearchAsync(new CrmHrAgentSearchQuery(
            "Restricted CRM Search Organization",
            CrmHrAgentRecordKind.Party,
            Take: 10));

        Assert.True(typedResult.IsSuccess);
        Assert.Equal(visiblePartyId, Assert.Single(typedResult.Value!).Id);
        Assert.True(allKindsResult.IsSuccess);
        Assert.Contains(allKindsResult.Value!, item =>
            item.RecordKind == CrmHrAgentRecordKind.Party &&
            item.Id == visiblePartyId);
        Assert.True(summaryResult.IsSuccess);
        Assert.Equal(visiblePartyId, summaryResult.Value!.Id);
        Assert.Equal("Development CRM Search Organization", summaryResult.Value.DisplayLabel);
        Assert.True(sensitiveResult.IsSuccess);
        Assert.Empty(sensitiveResult.Value!);
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset GetUtcNow() => now;
    }
}
