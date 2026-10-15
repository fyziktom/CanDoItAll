using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AppComponents;
using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmHrCatalogDialogTests
{
    [Fact]
    public async Task Directory_deep_link_opens_a_controlled_dialog_over_the_bounded_catalog()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var partyId = await CreatePartyAsync(
            partyDirectoryService,
            "Directory dialog proof",
            PartyRoleKind.Employee);

        navigation.NavigateTo($"/crm-hr/directory?partyId={partyId:D}");
        var cut = harness.Context.Render<CrmHrDirectoryPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-directory-catalog']"));
            Assert.Contains(
                "paged-record-browser__results--bounded",
                cut.Find("[data-testid='crmhr-directory-results']").ClassList);
            Assert.NotNull(cut.Find("[data-testid='crmhr-directory-record-dialog']"));
            Assert.Equal(
                "Directory dialog proof",
                cut.Find("[data-testid='crmhr-party-display-name']").GetAttribute("value"));
        });

        cut.Find("[data-testid='crmhr-directory-record-close']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='crmhr-directory-record-dialog']"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-party-display-name']"));
            Assert.EndsWith("/crm-hr/directory", navigation.Uri, StringComparison.Ordinal);
            Assert.NotNull(cut.Find("[data-testid='crmhr-directory-catalog']"));
        });
    }

    [Fact]
    public async Task Workforce_deep_link_opens_a_controlled_dialog_over_the_bounded_catalog()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var hrService = harness.Context.Services.GetRequiredService<HrService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var partyId = await CreatePartyAsync(
            partyDirectoryService,
            "Workforce dialog proof",
            PartyRoleKind.Employee);
        var profileResult = await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = partyId,
            WorkforceKind = WorkforceKind.Employee,
            Status = "Active",
            JobTitle = "Dialog tester",
            CapacityHoursPerWeek = 40m,
            LastChangedBy = "component-tests"
        });
        Assert.True(profileResult.IsSuccess);

        navigation.NavigateTo($"/crm-hr/workforce?partyId={partyId:D}");
        var cut = harness.Context.Render<CrmHrWorkforcePage>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-workforce-catalog']"));
            Assert.Contains(
                "paged-record-browser__results--bounded",
                cut.Find("[data-testid='crmhr-workforce-results']").ClassList);
            Assert.NotNull(cut.Find("[data-testid='crmhr-workforce-record-dialog']"));
            Assert.Contains("Workforce dialog proof", cut.Markup);
        });

        cut.Find("[data-testid='crmhr-workforce-record-close']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='crmhr-workforce-record-dialog']"));
            Assert.EndsWith("/crm-hr/workforce", navigation.Uri, StringComparison.Ordinal);
            Assert.NotNull(cut.Find("[data-testid='crmhr-workforce-catalog']"));
        });
    }

    [Fact]
    public async Task Crm_deep_link_opens_a_controlled_dialog_over_the_bounded_catalog()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var crmService = harness.Context.Services.GetRequiredService<CrmService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var accountId = await CreatePartyAsync(
            partyDirectoryService,
            "CRM dialog proof",
            PartyRoleKind.Customer,
            PartyType.Organization,
            PartyLifecycleStatus.Prospect);
        var profileResult = await crmService.SaveAccountProfileAsync(new CrmAccountProfileEditorModel
        {
            AccountPartyId = accountId,
            RelationshipStage = CrmAccountRelationshipStage.Prospect,
            CommercialNotes = "Controlled dialog proof",
            LastChangedBy = "component-tests"
        });
        Assert.True(profileResult.IsSuccess);

        navigation.NavigateTo($"/crm-hr/crm?accountId={accountId:D}");
        var cut = harness.Context.Render<CrmHrCrmPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-crm-catalog']"));
            Assert.Contains(
                "paged-record-browser__results--bounded",
                cut.Find("[data-testid='crmhr-account-results']").ClassList);
            Assert.NotNull(cut.Find("[data-testid='crmhr-crm-record-dialog']"));
            Assert.Contains("CRM dialog proof", cut.Markup);
        });

        cut.Find("[data-testid='crmhr-crm-record-close']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='crmhr-crm-record-dialog']"));
            Assert.EndsWith("/crm-hr/crm", navigation.Uri, StringComparison.Ordinal);
            Assert.NotNull(cut.Find("[data-testid='crmhr-crm-catalog']"));
        });
    }

    [Fact]
    public async Task Recruiting_deep_link_close_returns_to_the_catalog_and_the_card_reopens_the_record_as_an_entry()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddScoped<IAppNavigationHistory, TestNavigationHistory>());
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var recruitingService = harness.Context.Services.GetRequiredService<RecruitingService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var candidateId = await CreatePartyAsync(
            partyDirectoryService,
            "Recruiting dialog proof",
            PartyRoleKind.Candidate,
            PartyType.Person,
            PartyLifecycleStatus.Candidate);
        var applicationResult = await recruitingService.SaveRecruitmentApplicationAsync(
            new RecruitmentApplicationEditorModel
            {
                PartyId = candidateId,
                DesiredRole = "Platform engineer",
                Stage = RecruitmentStage.Interviewing,
                Decision = RecruitmentDecision.Pending,
                LastChangedBy = "component-tests"
            });
        Assert.True(applicationResult.IsSuccess);

        navigation.NavigateTo(
            $"/crm-hr/recruiting?applicationId={applicationResult.Value:D}");
        var history = (TestNavigationHistory)harness.Context.Services.GetRequiredService<IAppNavigationHistory>();
        var cut = harness.Context.Render<CrmHrRecruitingPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-recruiting-catalog']"));
            Assert.Contains(
                "paged-record-browser__results--bounded",
                cut.Find("[data-testid='crmhr-recruiting-applications-results']").ClassList);
            Assert.NotNull(cut.Find("[data-testid='crmhr-recruiting-record-dialog']"));
            Assert.NotNull(cut.Find("[data-testid='crmhr-recruiting-tab-application']"));
            Assert.Contains("Recruiting dialog proof", cut.Markup);
        });
        var browser = cut
            .FindComponent<PagedRecordBrowser<Guid, RecruitmentApplicationScope>>()
            .Instance;
        var expectedRoute = $"/crm-hr/recruiting?applicationId={applicationResult.Value:D}";
        var applicationCardTestId = $"crmhr-recruiting-application-{applicationResult.Value:N}";

        cut.Find("[data-testid='crmhr-recruiting-role']").Change("Unsaved role change");

        cut.Find("[data-testid='crmhr-recruiting-tab-interviews']").Click();
        cut.WaitForElement("[data-testid='crmhr-recruiting-interview-type']");
        Assert.Empty(cut.FindAll("[data-testid='crmhr-recruiting-role']"));
        Assert.EndsWith(expectedRoute + "&section=interviews", navigation.Uri, StringComparison.Ordinal);

        cut.Find("[data-testid='crmhr-recruiting-tab-assessments']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No bound AgentFramework candidate", cut.Markup);
            Assert.Empty(cut.FindAll("[data-testid='crmhr-recruiting-interview-type']"));
        });

        cut.Find("[data-testid='crmhr-recruiting-tab-development']").Click();
        cut.WaitForElement("[data-testid='crmhr-recruiting-support-save-button']");
        Assert.Empty(cut.FindAll("[data-testid='crmhr-recruiting-interview-type']"));

        cut.Find("[data-testid='crmhr-recruiting-tab-conversion']").Click();
        cut.WaitForElement("[data-testid='crmhr-recruiting-convert-kind']");
        Assert.Empty(cut.FindAll("[data-testid='crmhr-recruiting-support-save-button']"));
        Assert.EndsWith(expectedRoute + "&section=conversion", navigation.Uri, StringComparison.Ordinal);
        // The sections refined the deep link's own entry.
        Assert.Single(history.Locations);

        cut.Find("[data-testid='crmhr-recruiting-record-close']").Click();

        // The deep link had no catalog entry behind it: closing replaces it with the catalog location.
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='crmhr-recruiting-record-dialog']"));
            Assert.EndsWith("/crm-hr/recruiting", navigation.Uri, StringComparison.Ordinal);
            Assert.NotNull(cut.Find("[data-testid='crmhr-recruiting-catalog']"));
            Assert.Same(
                browser,
                cut.FindComponent<PagedRecordBrowser<Guid, RecruitmentApplicationScope>>().Instance);
            Assert.Null(browser.Selection);
            Assert.Equal("false", cut.Find($"[data-testid='{applicationCardTestId}']").GetAttribute("aria-pressed"));
            Assert.DoesNotContain(
                "paged-record-browser__card--selected",
                cut.Find($"[data-testid='{applicationCardTestId}-shell']").ClassList);

            var contextProvider = cut.FindComponent<AgentChatContextSurfaceProvider>();
            Assert.Equal(
                AgentChatContextAccessState.Ready,
                contextProvider.Instance.ContextAccessState);
            Assert.Null(contextProvider.Instance.Surface.Position.PrimarySelection);
            Assert.Empty(contextProvider.Instance.Surface.Position.SelectedEntities);
        });
        Assert.Equal(0, history.BackCount);
        Assert.Single(history.Locations);

        cut.Find($"[data-testid='{applicationCardTestId}']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-recruiting-record-dialog']"));
            Assert.Equal(
                "Platform engineer",
                cut.Find("[data-testid='crmhr-recruiting-role']").GetAttribute("value"));
            Assert.EndsWith(expectedRoute, navigation.Uri, StringComparison.Ordinal);
            Assert.Same(
                browser,
                cut.FindComponent<PagedRecordBrowser<Guid, RecruitmentApplicationScope>>().Instance);
            Assert.Equal(applicationResult.Value, browser.Selection?.Key);

            var applicationCard = cut.Find($"[data-testid='{applicationCardTestId}']");
            Assert.Equal("true", applicationCard.GetAttribute("aria-pressed"));
            Assert.Contains(
                "paged-record-browser__card--selected",
                cut.Find($"[data-testid='{applicationCardTestId}-shell']").ClassList);

            var contextProvider = cut.FindComponent<AgentChatContextSurfaceProvider>();
            Assert.Equal(
                AgentChatContextAccessState.Ready,
                contextProvider.Instance.ContextAccessState);

            var applicationSelection = Assert.IsType<AgentChatContextEntityReference>(
                contextProvider.Instance.Surface.Position.PrimarySelection);
            Assert.Equal("recruitment-application", applicationSelection.Kind);
            Assert.Equal(applicationResult.Value.ToString("D"), applicationSelection.Id);

            var candidateSelection = Assert.Single(
                contextProvider.Instance.Surface.Position.SelectedEntities);
            Assert.Equal("candidate-party", candidateSelection.Kind);
            Assert.Equal(candidateId.ToString("D"), candidateSelection.Id);
            Assert.Equal("Recruiting dialog proof", candidateSelection.DisplayName);
        });
        // The card opened the record as a new entry over the catalog entry.
        Assert.Equal(2, history.Locations.Count);
        Assert.Equal(1, history.Index);
        Assert.EndsWith("/crm-hr/recruiting", history.Locations[0], StringComparison.Ordinal);

        // Closing the record it opened goes back to that catalog entry.
        cut.Find("[data-testid='crmhr-recruiting-record-close']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='crmhr-recruiting-record-dialog']"));
            Assert.EndsWith("/crm-hr/recruiting", navigation.Uri, StringComparison.Ordinal);
            Assert.Null(browser.Selection);
        });
        Assert.Equal(1, history.BackCount);
        Assert.Equal(0, history.Index);
    }

    [Fact]
    public async Task Recruiting_new_application_dialog_opens_as_an_entry_and_closing_goes_back_to_the_base_route()
    {
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddScoped<IAppNavigationHistory, TestNavigationHistory>());
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/crm-hr/recruiting");
        var history = (TestNavigationHistory)harness.Context.Services.GetRequiredService<IAppNavigationHistory>();
        var cut = harness.Context.Render<CrmHrRecruitingPage>();

        cut.WaitForElement("[data-testid='crmhr-recruiting-new-button']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-recruiting-record-dialog']"));
            Assert.NotNull(cut.Find("[data-testid='crmhr-recruiting-tab-application']"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-recruiting-tab-interviews']"));
            Assert.EndsWith("/crm-hr/recruiting?dialog=new-application", navigation.Uri, StringComparison.Ordinal);
        });
        Assert.Equal(2, history.Locations.Count);

        cut.Find("[data-testid='crmhr-recruiting-record-close']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='crmhr-recruiting-record-dialog']"));
            Assert.EndsWith("/crm-hr/recruiting", navigation.Uri, StringComparison.Ordinal);
        });
        Assert.Equal(1, history.BackCount);
        Assert.Equal(0, history.Index);

        cut.Find("[data-testid='crmhr-recruiting-new-button']").Click();
        cut.WaitForElement("[data-testid='crmhr-recruiting-record-dialog']");
        Assert.EndsWith("/crm-hr/recruiting?dialog=new-application", navigation.Uri, StringComparison.Ordinal);
        Assert.Equal(1, history.Index);
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyRoleKind roleKind,
        PartyType partyType = PartyType.Person,
        PartyLifecycleStatus lifecycleStatus = PartyLifecycleStatus.Active)
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
            ]
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
