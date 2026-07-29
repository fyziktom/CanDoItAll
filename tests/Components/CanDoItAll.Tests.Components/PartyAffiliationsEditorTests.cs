using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class PartyAffiliationsEditorTests
{
    [Fact]
    public void Selecting_a_primary_clears_the_previous_primary_and_failed_parent_save_keeps_edits()
    {
        var personPartyId = Guid.NewGuid();
        var firstOrganizationId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();
        var firstExpectedUpdatedAtUtc = DateTimeOffset.UtcNow.AddHours(-2);
        var secondExpectedUpdatedAtUtc = DateTimeOffset.UtcNow.AddHours(-1);
        var affiliations = new List<PartyOrganizationAffiliationEditorModel>
        {
            new()
            {
                Id = Guid.NewGuid(),
                PersonPartyId = personPartyId,
                OrganizationPartyId = firstOrganizationId,
                AffiliationKind = PartyOrganizationAffiliationKind.Employee,
                IsPrimary = true,
                JobTitle = "Engineer",
                ExpectedUpdatedAtUtc = firstExpectedUpdatedAtUtc
            },
            new()
            {
                Id = Guid.NewGuid(),
                PersonPartyId = personPartyId,
                OrganizationPartyId = secondOrganizationId,
                AffiliationKind = PartyOrganizationAffiliationKind.Freelancer,
                JobTitle = "Advisor",
                ExpectedUpdatedAtUtc = secondExpectedUpdatedAtUtc
            }
        };
        IReadOnlyList<PartyOrganizationAffiliationEditorModel> published = affiliations;
        using var context = CreateContext(new StubPartyRecordQueryService([]));
        var cut = context.Render<PartyAffiliationsEditor>(parameters => parameters
            .Add(component => component.CurrentPersonPartyId, personPartyId)
            .Add(component => component.Affiliations, affiliations)
            .Add(
                component => component.AffiliationsChanged,
                value => published = value)
            .Add(
                component => component.PartyDisplayNames,
                new Dictionary<Guid, string>
                {
                    [firstOrganizationId] = "Northwind",
                    [secondOrganizationId] = "Contoso"
                }));

        cut.Find("[data-testid='crmhr-affiliation-primary-1']").Change(true);
        cut.Find("[data-testid='crmhr-affiliation-title-1']").Change("Strategic advisor");

        Assert.False(published[0].IsPrimary);
        Assert.True(published[1].IsPrimary);
        Assert.Equal("Strategic advisor", published[1].JobTitle);
        Assert.Equal(firstExpectedUpdatedAtUtc, published[0].ExpectedUpdatedAtUtc);
        Assert.Equal(secondExpectedUpdatedAtUtc, published[1].ExpectedUpdatedAtUtc);
        Assert.True(affiliations[0].IsPrimary);
        Assert.False(affiliations[1].IsPrimary);
        Assert.Equal("Advisor", affiliations[1].JobTitle);

        cut.Render(parameters => parameters
            .Add(component => component.CurrentPersonPartyId, personPartyId)
            .Add(component => component.Affiliations, published)
            .Add(
                component => component.AffiliationsChanged,
                value => published = value)
            .Add(
                component => component.PartyDisplayNames,
                new Dictionary<Guid, string>
                {
                    [firstOrganizationId] = "Northwind",
                    [secondOrganizationId] = "Contoso"
                }));

        Assert.Equal(
            "Strategic advisor",
            cut.Find("[data-testid='crmhr-affiliation-title-1']").GetAttribute("value"));
        Assert.True(cut.Find("[data-testid='crmhr-affiliation-primary-1']").HasAttribute("checked"));
        Assert.False(cut.Find("[data-testid='crmhr-affiliation-primary-0']").HasAttribute("checked"));
    }

    [Fact]
    public void Add_affiliation_uses_the_organization_picker_and_publishes_the_company()
    {
        var personPartyId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var queryService = new StubPartyRecordQueryService(
        [
            new PartyRecordQueryItem(
                organizationId,
                "Fabrikam Services",
                PartyType.Organization,
                PartyLifecycleStatus.Active,
                "FAB",
                "Consulting company",
                [],
                false)
        ]);
        IReadOnlyList<PartyOrganizationAffiliationEditorModel> published = [];
        using var context = CreateContext(queryService);
        var cut = context.Render<PartyAffiliationsEditor>(parameters => parameters
            .Add(component => component.CurrentPersonPartyId, personPartyId)
            .Add(component => component.Affiliations, published)
            .Add(
                component => component.AffiliationsChanged,
                value => published = value));

        cut.Find("[data-testid='crmhr-affiliation-add']").Click();

        var draft = Assert.Single(published);
        Assert.Equal(personPartyId, draft.PersonPartyId);
        Assert.Equal(PartyOrganizationAffiliationKind.ExternalContact, draft.AffiliationKind);
        Assert.True(draft.IsPrimary);
        Assert.Equal(
            PartyRecordScope.Organizations,
            queryService.LastQuery?.Scope);

        cut.WaitForElement($"[data-testid='crmhr-party-option-{organizationId:N}']")
            .Click();
        cut.Find("[data-testid='crmhr-affiliation-picker-confirm']").Click();

        var selected = Assert.Single(published);
        Assert.Equal(organizationId, selected.OrganizationPartyId);
        Assert.Contains("Fabrikam Services", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='crmhr-affiliation-picker']"));
    }

    [Fact]
    public void Cancelling_company_selection_discards_the_new_incomplete_affiliation()
    {
        IReadOnlyList<PartyOrganizationAffiliationEditorModel> published = [];
        using var context = CreateContext(new StubPartyRecordQueryService([]));
        var cut = context.Render<PartyAffiliationsEditor>(parameters => parameters
            .Add(component => component.CurrentPersonPartyId, Guid.NewGuid())
            .Add(component => component.Affiliations, published)
            .Add(
                component => component.AffiliationsChanged,
                value => published = value));

        cut.Find("[data-testid='crmhr-affiliation-add']").Click();
        Assert.Single(published);

        cut.Find("[data-testid='crmhr-affiliation-picker-cancel']").Click();

        Assert.Empty(published);
        Assert.Empty(cut.FindAll("[data-testid='crmhr-affiliation-picker']"));
    }

    private static BunitContext CreateContext(IPartyRecordQueryService queryService)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddSingleton<TooltipService>();
        context.Services.AddSingleton(queryService);
        return context;
    }

    private sealed class StubPartyRecordQueryService(
        IReadOnlyList<PartyRecordQueryItem> items) : IPartyRecordQueryService
    {
        public PartyRecordQuery? LastQuery { get; private set; }

        public Task<PartyRecordQueryItem?> GetAsync(
            Guid partyId,
            bool includeArchived = false,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(items.SingleOrDefault(item => item.Id == partyId));
        }

        public Task<PartyRecordPage> SearchAsync(
            PartyRecordQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            var filtered = items
                .Where(item =>
                    item.Id != query.ExcludedPartyId &&
                    IsInScope(item.PartyType, query.Scope))
                .ToList();
            return Task.FromResult(new PartyRecordPage(
                filtered,
                query.PageIndex,
                query.PageSize,
                filtered.Count));
        }

        private static bool IsInScope(PartyType partyType, PartyRecordScope scope)
        {
            var partyScope = partyType switch
            {
                PartyType.Person => PartyRecordScope.People,
                PartyType.Organization => PartyRecordScope.Organizations,
                PartyType.OrganizationUnit => PartyRecordScope.OrganizationUnits,
                PartyType.AiAgent => PartyRecordScope.AiAgents,
                _ => PartyRecordScope.None
            };
            return (scope & partyScope) != PartyRecordScope.None;
        }
    }
}
