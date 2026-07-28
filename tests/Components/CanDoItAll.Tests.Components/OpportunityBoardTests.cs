using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class OpportunityBoardTests
{
    [Fact]
    public async Task Board_labels_each_currency_without_rendering_a_cross_currency_total()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var accountId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var opportunities = new[]
        {
            CreatePipelineItem(accountId, ownerId, "USD expansion", "USD", 1250m),
            CreatePipelineItem(accountId, ownerId, "EUR renewal", "EUR", 900m)
        };

        var cut = harness.Context.Render<OpportunityBoard>(parameters => parameters
            .Add(component => component.Opportunities, opportunities));

        Assert.Contains($"USD {1250m:N2}", cut.Markup);
        Assert.Contains($"EUR {900m:N2}", cut.Markup);
        Assert.DoesNotContain($"{2150m:N0}", cut.Markup);
        Assert.DoesNotContain("Total", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pipeline_pages_and_filters_through_the_query_service()
    {
        var queryService = new RecordingOpportunityPipelineQueryService();
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<IOpportunityPipelineQueryService>(queryService));
        var accountId = Guid.NewGuid();

        var cut = harness.Context.Render<OpportunityPipeline>(parameters => parameters
            .Add(component => component.AccountPartyId, accountId)
            .Add(component => component.PageSize, 2));

        cut.WaitForAssertion(() =>
        {
            var query = Assert.Single(queryService.Queries);
            Assert.Equal(accountId, query.AccountPartyId);
            Assert.Equal(0, query.PageIndex);
            Assert.Equal(2, query.PageSize);
        });

        cut.Find("[data-testid='crmhr-opportunity-next']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, queryService.Queries.Count);
            Assert.Equal(1, queryService.Queries[^1].PageIndex);
        });

        cut.Find("[data-testid='crmhr-opportunity-stage-filter']")
            .Change(OpportunityStage.Proposal.ToString());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(OpportunityStage.Proposal, queryService.Queries[^1].Stage);
            Assert.Equal(0, queryService.Queries[^1].PageIndex);
        });
    }

    [Fact]
    public async Task Create_dialog_cancel_keeps_the_caller_model_unchanged()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var linkedProjectId = Guid.NewGuid();
        var initialModel = new CrmOpportunityEditorModel
        {
            AccountPartyId = Guid.NewGuid(),
            Title = "Original pursuit",
            OwnerPartyId = Guid.NewGuid(),
            LinkedProjectId = linkedProjectId
        };
        var closeCount = 0;

        var cut = harness.Context.Render<OpportunityCreateDialog>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.InitialModel, initialModel)
            .Add(component => component.Close, () => closeCount++));

        cut.Find("[data-testid='crmhr-opportunity-title']").Change("Unsaved change");
        cut.Find("[data-testid='crmhr-opportunity-project-clear']").Click();
        cut.Find("[data-testid='crmhr-opportunity-create-cancel']").Click();

        Assert.Equal("Original pursuit", initialModel.Title);
        Assert.Equal(linkedProjectId, initialModel.LinkedProjectId);
        Assert.Equal(1, closeCount);
    }

    [Fact]
    public async Task Cancelling_a_new_link_picker_removes_the_empty_draft_link()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var initialModel = new CrmOpportunityEditorModel
        {
            AccountPartyId = Guid.NewGuid(),
            Title = "Partner pursuit",
            OwnerPartyId = Guid.NewGuid()
        };
        var cut = harness.Context.Render<OpportunityCreateDialog>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.InitialModel, initialModel));

        cut.Find("[data-testid='crmhr-opportunity-create-next']").Click();
        cut.Find("[data-testid='crmhr-opportunity-party-add']").Click();

        var editor = cut.FindComponent<OpportunityEditor>();
        Assert.Single(editor.Instance.Model.Parties);

        var picker = cut.FindComponent<PartyRecordPickerDialog>();
        await cut.InvokeAsync(() => picker.Instance.OnClose.InvokeAsync());

        Assert.Empty(editor.Instance.Model.Parties);
    }

    [Fact]
    public async Task Create_dialog_submits_the_linked_project_id()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var linkedProjectId = Guid.NewGuid();
        var initialModel = new CrmOpportunityEditorModel
        {
            AccountPartyId = Guid.NewGuid(),
            Title = "Project-backed pursuit",
            OwnerPartyId = Guid.NewGuid(),
            LinkedProjectId = linkedProjectId
        };
        CrmOpportunityEditorModel? submittedModel = null;

        var cut = harness.Context.Render<OpportunityCreateDialog>(parameters => parameters
            .Add(component => component.IsOpen, true)
            .Add(component => component.InitialModel, initialModel)
            .Add(component => component.Save, model => submittedModel = model));

        cut.Find("[data-testid='crmhr-opportunity-create-next']").Click();
        cut.Find("[data-testid='crmhr-opportunity-create-next']").Click();
        cut.Find("#crmhr-opportunity-create-form").Submit();

        Assert.NotNull(submittedModel);
        Assert.Equal(linkedProjectId, submittedModel.LinkedProjectId);
    }

    [Fact]
    public async Task Home_page_surfaces_open_pipeline_preview()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var crmService = harness.Context.Services.GetRequiredService<CrmService>();

        var accountId = await CreatePartyAsync(
            partyDirectoryService,
            "Fabrikam Retainer",
            PartyType.Organization,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Customer,
            "crm@fabrikam.example");
        var ownerId = await CreatePartyAsync(
            partyDirectoryService,
            "Nina Owner",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AccountManager,
            "nina.owner@example.test");

        var saveResult = await crmService.SaveOpportunityAsync(new CrmOpportunityEditorModel
        {
            AccountPartyId = accountId,
            Title = "Renewal expansion",
            Stage = OpportunityStage.Proposal,
            OpportunitySource = OpportunitySource.Renewal,
            OwnerPartyId = ownerId,
            CurrencyCode = "USD",
            Amount = 45000m,
            ProbabilityPercent = 65,
            ExpectedCloseOn = new DateOnly(2026, 6, 20),
            Summary = "Renewal and extension of the advisory retainer.",
            LastChangedBy = "component-tests"
        });

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.Render<CrmHrHomePage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Open pipeline", cut.Markup);
            Assert.Contains("Renewal expansion", cut.Markup);
            Assert.Contains("Fabrikam Retainer", cut.Markup);
            Assert.Contains("Nina Owner", cut.Markup);
        });
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
            LastChangedBy = "component-tests",
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

    private static OpportunityPipelineItem CreatePipelineItem(
        Guid accountId,
        Guid ownerId,
        string title,
        string currencyCode,
        decimal amount)
    {
        return new OpportunityPipelineItem(
            Guid.NewGuid(),
            title,
            OpportunityStage.Proposal,
            OpportunitySource.Direct,
            accountId,
            "Fabrikam",
            ownerId,
            "Nina Owner",
            null,
            string.Empty,
            currencyCode,
            amount,
            50,
            new DateOnly(2026, 8, 31),
            DateTimeOffset.UtcNow);
    }

    private sealed class RecordingOpportunityPipelineQueryService : IOpportunityPipelineQueryService
    {
        public List<OpportunityPipelineQuery> Queries { get; } = [];

        public Task<OpportunityPipelinePage> SearchAsync(
            OpportunityPipelineQuery query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            IReadOnlyList<OpportunityPipelineItem> items =
            [
                CreatePipelineItem(
                    query.AccountPartyId,
                    Guid.NewGuid(),
                    $"Page {query.PageIndex + 1}",
                    "USD",
                    100m)
            ];
            return Task.FromResult(new OpportunityPipelinePage(
                items,
                query.PageIndex,
                query.PageSize,
                3));
        }
    }
}
