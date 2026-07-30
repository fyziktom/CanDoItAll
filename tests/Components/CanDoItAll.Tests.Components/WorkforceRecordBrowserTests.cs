using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class WorkforceRecordBrowserTests
{
    [Fact]
    public void Fill_height_is_forwarded_to_the_shared_record_browser()
    {
        var queryService = new StubWorkforceRecordQueryService([]);
        using var context = CreateContext(queryService);
        var cut = context.Render<WorkforceRecordBrowser>(parameters => parameters
            .Add(component => component.FillHeight, true));

        var browser = cut.Find("[data-testid='crmhr-workforce']");

        Assert.Contains(
            "paged-record-browser--fill-height",
            browser.ClassList);
        var browserStyle = browser.GetAttribute("style")!.Replace(" ", string.Empty);
        Assert.Contains(";height:100%", browserStyle);
        Assert.Contains(";min-height:0", browserStyle);
    }

    [Fact]
    public void Affiliation_filters_issue_typed_bounded_queries()
    {
        var record = Record(
            WorkforceRecordClassification.Contractor,
            PartyLifecycleStatus.Active);
        var queryService = new StubWorkforceRecordQueryService([record]);
        using var context = CreateContext(queryService);
        var cut = context.Render<WorkforceRecordBrowser>();

        cut.WaitForElement($"[data-testid='crmhr-workforce-option-{record.PartyId:N}']");
        Assert.Equal(
            [
                "All",
                "Employee",
                "Contractor",
                "Freelancer",
                "External contact",
                "Delivery unit"
            ],
            cut.FindAll("[data-testid='crmhr-workforce-scope-filter'] button")
                .Select(element => element.TextContent.Trim())
                .ToArray());

        cut.Find("[data-testid='crmhr-workforce-filter-contractor']").Click();

        cut.WaitForAssertion(() =>
        {
            var query = Assert.IsType<WorkforceRecordQuery>(queryService.LastQuery);
            Assert.Equal(WorkforceRecordClassification.Contractor, query.Classification);
            Assert.Equal(0, query.PageIndex);
            Assert.Equal(12, query.PageSize);
        });
    }

    [Fact]
    public void Card_keeps_classification_and_lifecycle_independent_and_discloses_other_affiliations()
    {
        var partyId = Guid.NewGuid();
        var primary = Affiliation(
            "Northwind Ltd",
            "Principal consultant",
            PartyOrganizationAffiliationKind.ExternalContact,
            isPrimary: true);
        var otherOne = Affiliation(
            "Contoso",
            "Advisor",
            PartyOrganizationAffiliationKind.ExternalContact);
        var otherTwo = Affiliation(
            "Fabrikam",
            "Board observer",
            PartyOrganizationAffiliationKind.ExternalContact);
        var record = new WorkforceRecordQueryItem(
            partyId,
            "Alex Morgan",
            PartyType.Person,
            PartyLifecycleStatus.Former,
            false,
            "External delivery participant.",
            WorkforceRecordClassification.ExternalContact,
            false,
            DateTimeOffset.UtcNow,
            primary,
            primary.DisplayText,
            [otherOne, otherTwo]);
        var queryService = new StubWorkforceRecordQueryService([record]);
        using var context = CreateContext(queryService);
        var cut = context.Render<WorkforceRecordBrowser>();

        var shell = cut.WaitForElement(
            $"[data-testid='crmhr-workforce-option-{partyId:N}-shell']");
        var classificationCorner = shell.QuerySelector(
            $"[data-testid='crmhr-workforce-option-{partyId:N}-kind-corner']")!;
        Assert.Contains("paged-record-browser__kind-corner", classificationCorner.ClassList);
        Assert.Contains("absolute", classificationCorner.ClassList);
        Assert.Contains("left-3", classificationCorner.ClassList);
        Assert.Contains("top-3", classificationCorner.ClassList);
        Assert.Contains(
            "External contact",
            classificationCorner.TextContent,
            StringComparison.Ordinal);
        Assert.Equal(
            "Former",
            shell.QuerySelector(".paged-record-browser__status span")!.TextContent.Trim());
        Assert.Contains(
            "Northwind Ltd",
            shell.QuerySelector(".paged-record-browser__subtitle-slot")!.TextContent,
            StringComparison.Ordinal);
        Assert.Contains(
            "Principal consultant",
            shell.QuerySelector(".paged-record-browser__subtitle-slot")!.TextContent,
            StringComparison.Ordinal);
        Assert.Contains("No staffable profile", shell.TextContent, StringComparison.Ordinal);

        shell.QuerySelector(
                ".paged-record-browser__subtitle-slot .paged-record-browser__tooltip-target")!
            .TriggerEvent(
                "onmouseenter",
                new MouseEventArgs
                {
                    ClientX = 120,
                    ClientY = 80
                });

        var tooltip = context.Services.GetRequiredService<TooltipService>().Current;
        Assert.NotNull(tooltip);
        Assert.Contains("Contoso", tooltip.Text, StringComparison.Ordinal);
        Assert.Contains("Advisor", tooltip.Text, StringComparison.Ordinal);
        Assert.Contains("Fabrikam", tooltip.Text, StringComparison.Ordinal);
        Assert.Contains("Board observer", tooltip.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Northwind Ltd", tooltip.Text, StringComparison.Ordinal);
    }

    private static BunitContext CreateContext(IWorkforceRecordQueryService queryService)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddSingleton<TooltipService>();
        context.Services.AddSingleton(queryService);
        return context;
    }

    private static WorkforceRecordQueryItem Record(
        WorkforceRecordClassification classification,
        PartyLifecycleStatus lifecycleStatus)
    {
        return new WorkforceRecordQueryItem(
            Guid.NewGuid(),
            "Workforce record",
            classification == WorkforceRecordClassification.DeliveryUnit
                ? PartyType.OrganizationUnit
                : PartyType.Person,
            lifecycleStatus,
            false,
            "Workforce summary",
            classification,
            classification != WorkforceRecordClassification.ExternalContact,
            DateTimeOffset.UtcNow,
            null,
            "No current organization affiliation",
            []);
    }

    private static WorkforceRecordAffiliationSummaryModel Affiliation(
        string organizationName,
        string jobTitle,
        PartyOrganizationAffiliationKind affiliationKind,
        bool isPrimary = false)
    {
        return new WorkforceRecordAffiliationSummaryModel(
            Guid.NewGuid(),
            affiliationKind,
            Guid.NewGuid(),
            organizationName,
            jobTitle,
            isPrimary,
            null,
            null,
            $"{organizationName} — {jobTitle}");
    }

    private sealed class StubWorkforceRecordQueryService(
        IReadOnlyList<WorkforceRecordQueryItem> records) : IWorkforceRecordQueryService
    {
        public WorkforceRecordQuery? LastQuery { get; private set; }

        public Task<WorkforceRecordPage> SearchAsync(
            WorkforceRecordQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            var filtered = records
                .Where(item =>
                    !query.Classification.HasValue ||
                    item.Classification == query.Classification.Value)
                .ToList();
            return Task.FromResult(new WorkforceRecordPage(
                filtered,
                query.PageIndex,
                query.PageSize,
                filtered.Count));
        }
    }
}
