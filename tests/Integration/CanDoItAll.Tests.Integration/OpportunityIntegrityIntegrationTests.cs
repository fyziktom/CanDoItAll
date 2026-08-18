using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class OpportunityIntegrityIntegrationTests
{
    [Fact]
    public async Task Won_recognition_is_immutable_when_the_current_commercial_value_changes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var crmService = scope.ServiceProvider.GetRequiredService<CrmService>();
        var financials = scope.ServiceProvider.GetRequiredService<ICrmFinancialSnapshotQueryService>();
        var accountId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        await SeedPartiesAsync(
            dbContextFactory,
            CreateParty(accountId, PartyType.Organization, "Northwind"),
            CreateParty(ownerId, PartyType.Person, "Olivia Owner"));

        var createResult = await crmService.SaveOpportunityAsync(new CrmOpportunityEditorModel
        {
            AccountPartyId = accountId,
            Title = "Immutable recognition",
            Stage = OpportunityStage.Won,
            OwnerPartyId = ownerId,
            CurrencyCode = " eur ",
            Amount = 125m,
            ProbabilityPercent = 100,
            LastChangedBy = "integration-tests"
        });

        Assert.True(createResult.IsSuccess);
        var created = Assert.IsType<CrmOpportunityDetailModel>(
            await crmService.GetOpportunityAsync(createResult.Value));
        var update = CreateEditor(created);
        update.CurrencyCode = "GBP";
        update.Amount = 900m;

        var updateResult = await crmService.SaveOpportunityAsync(update);

        Assert.True(updateResult.IsSuccess);
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var recognition = Assert.Single(await dbContext.Set<OpportunityStageHistory>()
                .Where(item =>
                    item.OpportunityId == createResult.Value &&
                    item.Stage == OpportunityStage.Won)
                .ToListAsync());
            Assert.Equal(125m, recognition.RecognizedAmount);
            Assert.Equal("EUR", recognition.RecognizedCurrencyCode);
        }

        var snapshot = await financials.GetAsync(accountId);
        Assert.Equal([new CrmCurrencyAmount("EUR", 125m)], snapshot.SoldTotals);
        Assert.Equal(0, snapshot.IncompleteWonOpportunityCount);
    }

    [Fact]
    public async Task Save_rejects_invalid_identity_currency_party_policy_and_stale_edits()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var crmService = scope.ServiceProvider.GetRequiredService<CrmService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var accountId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        await SeedPartiesAsync(
            dbContextFactory,
            CreateParty(accountId, PartyType.Organization, "Northwind"),
            CreateParty(ownerId, PartyType.Person, "Olivia Owner"));

        var invalidCurrency = CreateNewEditor(accountId, ownerId);
        invalidCurrency.CurrencyCode = "US";
        AssertError(
            await crmService.SaveOpportunityAsync(invalidCurrency),
            "crmhr.crm.opportunity-currency-invalid");

        var invalidOwner = CreateNewEditor(accountId, accountId);
        AssertError(
            await crmService.SaveOpportunityAsync(invalidOwner),
            "crmhr.crm.opportunity-owner-type-invalid");

        var invalidDeliveryUnit = CreateNewEditor(accountId, ownerId);
        invalidDeliveryUnit.DeliveryUnitPartyId = ownerId;
        AssertError(
            await crmService.SaveOpportunityAsync(invalidDeliveryUnit),
            "crmhr.crm.opportunity-delivery-unit-type-invalid");

        var missing = CreateNewEditor(accountId, ownerId);
        missing.Id = Guid.NewGuid();
        missing.ExpectedUpdatedAtUtc = DateTimeOffset.UtcNow;
        AssertError(
            await crmService.SaveOpportunityAsync(missing),
            "crmhr.crm.opportunity-missing");

        var createResult = await crmService.SaveOpportunityAsync(CreateNewEditor(accountId, ownerId));
        Assert.True(createResult.IsSuccess);
        var original = Assert.IsType<CrmOpportunityDetailModel>(
            await crmService.GetOpportunityAsync(createResult.Value));
        var missingExpectedTimestamp = CreateEditor(original);
        missingExpectedTimestamp.ExpectedUpdatedAtUtc = null;
        AssertError(
            await crmService.SaveOpportunityAsync(missingExpectedTimestamp),
            "crmhr.crm.opportunity-expected-updated-at-required");
        var firstEdit = CreateEditor(original);
        firstEdit.Title = "First accepted edit";
        var staleEdit = CreateEditor(original);
        staleEdit.Title = "Stale overwrite";

        Assert.True((await crmService.SaveOpportunityAsync(firstEdit)).IsSuccess);
        AssertError(
            await crmService.SaveOpportunityAsync(staleEdit),
            "crmhr.crm.opportunity-concurrency-conflict");

        var current = Assert.IsType<CrmOpportunityDetailModel>(
            await crmService.GetOpportunityAsync(createResult.Value));
        Assert.Equal("First accepted edit", current.Title);

        var wonEditor = CreateNewEditor(accountId, ownerId);
        wonEditor.Title = "Conversion concurrency";
        wonEditor.Stage = OpportunityStage.Won;
        wonEditor.ProbabilityPercent = 100;
        var wonResult = await crmService.SaveOpportunityAsync(wonEditor);
        Assert.True(wonResult.IsSuccess);
        var staleConversionSource = Assert.IsType<CrmOpportunityDetailModel>(
            await crmService.GetOpportunityAsync(wonResult.Value));
        var concurrentEdit = CreateEditor(staleConversionSource);
        concurrentEdit.Notes = "Changed before conversion";
        Assert.True((await crmService.SaveOpportunityAsync(concurrentEdit)).IsSuccess);

        AssertError(
            await crmService.ConvertOpportunityToProjectAsync(
                new CrmOpportunityConversionEditorModel
                {
                    OpportunityId = wonResult.Value,
                    ExpectedUpdatedAtUtc = staleConversionSource.UpdatedAtUtc,
                    ProjectName = "Must not be created",
                    LastChangedBy = "integration-tests"
                }),
            "crmhr.crm.opportunity-conversion-concurrency-conflict");
        Assert.DoesNotContain(
            await projectsService.ListAsync(),
            project => project.Name == "Must not be created");
    }

    [Fact]
    public async Task Opportunity_project_references_use_the_projects_query_boundary()
    {
        var projectId = Guid.NewGuid();
        var project = new ProjectRecordQueryItem(
            projectId,
            "Boundary project",
            ProjectStatus.Active,
            "Delivery",
            "Served by the Projects query boundary.",
            DateTimeOffset.UtcNow);
        var projectQuery = new StubProjectRecordQueryService(project);
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IProjectRecordQueryService>();
                services.AddSingleton<IProjectRecordQueryService>(projectQuery);
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var crmService = scope.ServiceProvider.GetRequiredService<CrmService>();
        var accountId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        await SeedPartiesAsync(
            dbContextFactory,
            CreateParty(accountId, PartyType.Organization, "Northwind"),
            CreateParty(ownerId, PartyType.Person, "Olivia Owner"));
        var editor = CreateNewEditor(accountId, ownerId);
        editor.LinkedProjectId = projectId;

        var saveResult = await crmService.SaveOpportunityAsync(editor);

        Assert.True(saveResult.IsSuccess);
        var detail = Assert.IsType<CrmOpportunityDetailModel>(
            await crmService.GetOpportunityAsync(saveResult.Value));
        Assert.Equal(projectId, detail.LinkedProjectId);
        Assert.Equal("Boundary project", detail.LinkedProjectName);
        Assert.True(projectQuery.GetCallCount > 0);
        Assert.True(projectQuery.GetManyCallCount > 0);

        projectQuery.IsAvailable = false;
        var brokenReference = await Assert.ThrowsAsync<InvalidOperationException>(
            () => crmService.GetOpportunityAsync(saveResult.Value));
        Assert.Contains(projectId.ToString(), brokenReference.Message, StringComparison.Ordinal);

        var missingProject = CreateNewEditor(accountId, ownerId);
        missingProject.LinkedProjectId = Guid.NewGuid();
        AssertError(
            await crmService.SaveOpportunityAsync(missingProject),
            "crmhr.crm.opportunity-linked-project-missing");
    }

    private static CrmOpportunityEditorModel CreateNewEditor(Guid accountId, Guid ownerId)
    {
        return new CrmOpportunityEditorModel
        {
            AccountPartyId = accountId,
            Title = "Integrity test opportunity",
            Stage = OpportunityStage.Proposal,
            OwnerPartyId = ownerId,
            CurrencyCode = "USD",
            Amount = 100m,
            ProbabilityPercent = 60,
            LastChangedBy = "integration-tests"
        };
    }

    private static CrmOpportunityEditorModel CreateEditor(CrmOpportunityDetailModel opportunity)
    {
        return new CrmOpportunityEditorModel
        {
            Id = opportunity.Id,
            ExpectedUpdatedAtUtc = opportunity.UpdatedAtUtc,
            AccountPartyId = opportunity.AccountPartyId,
            Title = opportunity.Title,
            Stage = opportunity.Stage,
            RelationshipStage = opportunity.RelationshipStage,
            OpportunitySource = opportunity.OpportunitySource,
            OwnerPartyId = opportunity.OwnerPartyId,
            DeliveryUnitPartyId = opportunity.DeliveryUnitPartyId,
            CurrencyCode = opportunity.CurrencyCode,
            Amount = opportunity.Amount,
            ProbabilityPercent = opportunity.ProbabilityPercent,
            ExpectedCloseOn = opportunity.ExpectedCloseOn,
            LostReason = opportunity.LostReason,
            CompetitorName = opportunity.CompetitorName,
            PartnerContributionSummary = opportunity.PartnerContributionSummary,
            Summary = opportunity.Summary,
            Notes = opportunity.Notes,
            LinkedProjectId = opportunity.LinkedProjectId,
            Parties = opportunity.Parties
                .Select(item => new CrmOpportunityPartyLinkEditorModel
                {
                    Id = item.Id,
                    PartyId = item.PartyId,
                    Role = item.Role
                })
                .ToList(),
            LastChangedBy = "integration-tests"
        };
    }

    private static Party CreateParty(Guid id, PartyType partyType, string displayName)
    {
        return new Party
        {
            Id = id,
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static async Task SeedPartiesAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        params Party[] parties)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.Set<Party>().AddRange(parties);
        await dbContext.SaveChangesAsync();
    }

    private static void AssertError<T>(Result<T> result, string expectedCode)
    {
        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == expectedCode);
    }

    private sealed class StubProjectRecordQueryService(ProjectRecordQueryItem project)
        : IProjectRecordQueryService
    {
        public int GetCallCount { get; private set; }

        public int GetManyCallCount { get; private set; }

        public bool IsAvailable { get; set; } = true;

        public Task<ProjectRecordQueryItem?> GetAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            return Task.FromResult<ProjectRecordQueryItem?>(
                IsAvailable && projectId == project.Id ? project : null);
        }

        public Task<IReadOnlyList<ProjectRecordQueryItem>> GetManyAsync(
            IReadOnlyCollection<Guid> projectIds,
            CancellationToken cancellationToken = default)
        {
            GetManyCallCount++;
            IReadOnlyList<ProjectRecordQueryItem> matches = IsAvailable && projectIds.Contains(project.Id)
                ? [project]
                : [];
            return Task.FromResult(matches);
        }

        public Task<ProjectRecordPage> SearchAsync(
            ProjectRecordQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
