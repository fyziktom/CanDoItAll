using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class ProjectPartyAffiliationPresentationTests
{
    [Fact]
    public void Picker_defaults_to_the_current_primary_affiliation_and_allows_an_explicit_choice()
    {
        var personId = Guid.NewGuid();
        var primary = Affiliation(
            personId,
            "Northwind",
            PartyOrganizationAffiliationKind.Freelancer,
            isPrimary: true);
        var other = Affiliation(
            personId,
            "Contoso",
            PartyOrganizationAffiliationKind.ExternalContact);
        var service = new StubAffiliationService([other, primary]);
        using var context = new BunitContext();
        context.Services.AddSingleton<IPartyOrganizationAffiliationService>(
            service);
        Guid? selectedAffiliationId = null;

        var cut = context.Render<PartyAffiliationPicker>(parameters => parameters
            .Add(component => component.PartyId, personId)
            .Add(
                component => component.SelectedAffiliationIdChanged,
                value => selectedAffiliationId = value)
            .Add(
                component => component.TestIdPrefix,
                "assignment-affiliation"));

        cut.WaitForAssertion(() =>
            Assert.Equal(primary.Id, selectedAffiliationId));
        var select = cut.Find("[data-testid='assignment-affiliation']");
        Assert.Contains("Freelancer / Northwind / Primary", select.TextContent);
        Assert.Contains("External contact / Contoso", select.TextContent);

        select.Change(other.Id.ToString("D"));

        Assert.Equal(other.Id, selectedAffiliationId);
        Assert.Equal(personId, service.RequestedPersonPartyId);
    }

    [Fact]
    public void Assignment_card_discloses_classification_company_and_role()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<TooltipService>();
        var assignment = new ProjectPartyAssignmentDetail(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ProjectPartyAssignmentRole.TeamMember,
            "Alex External",
            "Person",
            ProjectPartyType.Person,
            string.Empty,
            false,
            50m,
            null,
            null,
            "test",
            string.Empty,
            new ProjectPartyAffiliationContext(
                Guid.NewGuid(),
                "External contact",
                "Northwind",
                "Garden designer",
                "Contoso / Advisor"));

        var cut = context.Render<ProjectAssignmentRecordCard>(parameters =>
            parameters
                .Add(component => component.Assignment, assignment)
                .Add(component => component.ProjectName, "Garden plan")
                .Add(component => component.TestIdPrefix, "assignment"));

        var affiliation = cut.Find("[data-testid='assignment-affiliation']");
        Assert.Contains("External contact", affiliation.TextContent);
        Assert.Contains("Northwind", affiliation.TextContent);
        Assert.Contains("Garden designer", affiliation.TextContent);
        Assert.Equal(
            "Contoso / Advisor",
            affiliation.GetAttribute("title"));
    }

    private static PartyOrganizationAffiliationListItemModel Affiliation(
        Guid personPartyId,
        string organizationName,
        PartyOrganizationAffiliationKind kind,
        bool isPrimary = false)
    {
        var now = DateTimeOffset.UtcNow;
        return new PartyOrganizationAffiliationListItemModel(
            Guid.NewGuid(),
            personPartyId,
            "Alex External",
            Guid.NewGuid(),
            organizationName,
            kind,
            isPrimary,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            null,
            string.Empty,
            null,
            null,
            string.Empty,
            "tests",
            now,
            now,
            true);
    }

    private sealed class StubAffiliationService(
        IReadOnlyList<PartyOrganizationAffiliationListItemModel> affiliations)
        : IPartyOrganizationAffiliationService
    {
        public Guid? RequestedPersonPartyId { get; private set; }

        public Task<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>
            ListAsync(
                Guid personPartyId,
                CancellationToken cancellationToken = default)
        {
            RequestedPersonPartyId = personPartyId;
            return Task.FromResult(affiliations);
        }

        public Task<Result<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>>
            ReplaceAsync(
                Guid personPartyId,
                IReadOnlyCollection<PartyOrganizationAffiliationEditorModel>
                    replacement,
                string actor,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<PartyOrganizationAffiliationListItemModel>>
            UpsertAsync(
                PartyOrganizationAffiliationEditorModel affiliation,
                string actor,
                DateTimeOffset? expectedUpdatedAtUtc = null,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
