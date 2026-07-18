using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class CrmHrPrivacyBoundaryTests
{
    [Fact]
    public async Task Directory_page_saves_confidential_notes_separately_from_operational_notes()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        var saveResult = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = "Rhea Sensitive",
            Summary = "Operational context remains broadly visible.",
            Notes = "Operational note for staffing coordination.",
            IsSensitive = true,
            LastChangedBy = "component-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = PartyRoleKind.Employee,
                    Title = "Employee",
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = "rhea.sensitive@example.test",
                    NormalizedValue = "rhea.sensitive@example.test",
                    IsPrimary = true,
                    IsPublic = true
                }
            ],
            ConfidentialNotes =
            [
                new PartyConfidentialNoteEditorModel
                {
                    Category = PartyConfidentialNoteCategories.Compensation,
                    NoteText = "Salary band and medical accommodation are protected.",
                    CreatedBy = "component-tests"
                }
            ]
        });
        Assert.True(saveResult.IsSuccess);

        navigation.NavigateTo($"/crm-hr/directory?partyId={saveResult.Value}");
        var cut = harness.Context.RenderComponent<CrmHrDirectoryPage>();
        cut.WaitForAssertion(() => Assert.Equal(
            "Rhea Sensitive",
            cut.Find("[data-testid='crmhr-party-display-name']").GetAttribute("value")));
        cut.WaitForElement("[data-testid='crmhr-directory-tab-handling']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Hidden from global search", cut.Markup);
            Assert.Contains("Salary band and medical accommodation are protected.", cut.Markup);
            Assert.Contains("Operational note for staffing coordination.", cut.Markup);
        });

        var loadedParty = await partyDirectoryService.GetPartyAsync(saveResult.Value);

        Assert.NotNull(loadedParty);
        Assert.Equal("Operational note for staffing coordination.", loadedParty.Notes);
        var confidentialNote = Assert.Single(loadedParty.ConfidentialNotes);
        Assert.Equal(PartyConfidentialNoteCategories.Compensation, confidentialNote.Category);
        Assert.Equal("Salary band and medical accommodation are protected.", confidentialNote.NoteText);
        Assert.DoesNotContain("Salary band and medical accommodation are protected.", loadedParty.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Home_and_workforce_routes_surface_sensitive_handling_and_history()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var hrService = harness.Context.Services.GetRequiredService<HrService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        var partyId = await CreateSensitivePartyAsync(partyDirectoryService, "Lena Private");
        var workforceSave = await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = partyId,
            WorkforceKind = WorkforceKind.Employee,
            Status = "Active",
            JobTitle = "People Operations Partner",
            Discipline = "Operations",
            CapacityHoursPerWeek = 40m,
            Notes = "Sensitive workforce proof.",
            LastChangedBy = "component-tests"
        });
        Assert.True(workforceSave.IsSuccess);

        var homeCut = harness.Context.RenderComponent<CrmHrHomePage>();
        homeCut.WaitForAssertion(() =>
        {
            Assert.Contains("Sensitive directory records", homeCut.Markup);
            Assert.Contains("Lena Private", homeCut.Markup);
        });

        navigation.NavigateTo($"/crm-hr/workforce?partyId={partyId}");
        var workforceCut = harness.Context.RenderComponent<CrmHrWorkforcePage>();
        workforceCut.WaitForAssertion(() =>
        {
            Assert.Contains("Hidden from global search", workforceCut.Markup);
            Assert.Contains("component-tests", workforceCut.Markup);
            Assert.DoesNotContain("Saved workforce profile for 'Lena Private'.", workforceCut.Markup);
        });

        workforceCut.Find("[data-testid='crmhr-workforce-tab-history']").Click();
        workforceCut.WaitForAssertion(() =>
        {
            Assert.Contains("Saved workforce profile for 'Lena Private'.", workforceCut.Markup);
        });
    }

    private static async Task<Guid> CreateSensitivePartyAsync(PartyDirectoryService partyDirectoryService, string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            Notes = "Operational note",
            IsSensitive = true,
            LastChangedBy = "component-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = PartyRoleKind.Employee,
                    Title = "Employee",
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = $"{displayName.Replace(" ", ".", StringComparison.Ordinal).ToLowerInvariant()}@example.test",
                    NormalizedValue = $"{displayName.Replace(" ", ".", StringComparison.Ordinal).ToLowerInvariant()}@example.test",
                    IsPrimary = true,
                    IsPublic = true
                }
            ],
            ConfidentialNotes =
            [
                new PartyConfidentialNoteEditorModel
                {
                    Category = PartyConfidentialNoteCategories.HumanResources,
                    NoteText = "Protected note",
                    CreatedBy = "component-tests"
                }
            ]
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
