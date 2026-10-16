using Bunit;
using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// The routed CRM / HR pages keep their location in the URL: opening a record is a history entry over the catalog,
// its sections refine that entry, closing goes back to the catalog entry it was opened from, and every location (a
// pasted link, Back, Forward) restores the same state. The hosts run against the real services and PostgreSQL.
public sealed class CrmHrUrlStateTests {
    [Fact]
    public async Task Directory_record_opens_as_an_entry_its_sections_refine_it_and_closing_goes_back() {
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddScoped<IAppNavigationHistory, TestNavigationHistory>());
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var partyId = await CreatePartyAsync(parties, "Location directory record", PartyRoleKind.Employee);
        navigation.NavigateTo("/crm-hr/directory");
        var history = (TestNavigationHistory)harness.Context.Services.GetRequiredService<IAppNavigationHistory>();
        var cut = harness.Context.Render<CrmHrDirectoryPage>();
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForElement("[data-testid='crmhr-directory-catalog']");

        await cut.InvokeAsync(() => view.SelectPartyAsync(partyId));

        cut.WaitForAssertion(() => Assert.True(view.IsEditorDialogOpen));
        Assert.EndsWith($"/crm-hr/directory?partyId={partyId:D}", navigation.Uri, StringComparison.Ordinal);
        Assert.Equal(2, history.Locations.Count);

        await cut.InvokeAsync(() => view.HandleEditorTabChangedAsync((int)DirectorySection.Contacts));

        Assert.EndsWith($"/crm-hr/directory?partyId={partyId:D}&section=contacts", navigation.Uri, StringComparison.Ordinal);
        Assert.Equal((int)DirectorySection.Contacts, view.SelectedEditorTabIndex);
        Assert.Equal(2, history.Locations.Count);

        await cut.InvokeAsync(view.CloseEditorDialogAsync);

        cut.WaitForAssertion(() => Assert.False(view.IsEditorDialogOpen));
        Assert.EndsWith("/crm-hr/directory", navigation.Uri, StringComparison.Ordinal);
        Assert.Equal(1, history.BackCount);
        Assert.Equal(0, history.Index);

        // A pasted or forwarded location opens the record on the section it names.
        navigation.NavigateTo($"/crm-hr/directory?partyId={partyId:D}&section=handling");

        cut.WaitForAssertion(() => {
            Assert.True(view.IsEditorDialogOpen);
            Assert.Equal((int)DirectorySection.Handling, view.SelectedEditorTabIndex);
            Assert.Equal(partyId, view.Editor.Id);
        });
    }

    [Fact]
    public async Task Directory_live_search_refines_the_entry_a_scope_is_its_own_entry_and_back_restores_the_catalog() {
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddScoped<IAppNavigationHistory, TestNavigationHistory>());
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/crm-hr/directory");
        var history = (TestNavigationHistory)harness.Context.Services.GetRequiredService<IAppNavigationHistory>();
        var cut = harness.Context.Render<CrmHrDirectoryPage>();
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='crmhr-directory-search']").HasAttribute("disabled")));

        cut.Find("[data-testid='crmhr-directory-search']").Input("acme");

        cut.WaitForAssertion(() => Assert.EndsWith("/crm-hr/directory?q=acme", navigation.Uri, StringComparison.Ordinal));
        Assert.Single(history.Locations);
        Assert.Equal("200", cut.Find("[data-testid='crmhr-directory-search']").GetAttribute("maxlength"));

        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='party-scope-people']").HasAttribute("disabled")));
        cut.Find("[data-testid='party-scope-people']").Click();

        cut.WaitForAssertion(() => Assert.EndsWith("/crm-hr/directory?q=acme&scope=people", navigation.Uri, StringComparison.Ordinal));
        Assert.Equal(2, history.Locations.Count);
        Assert.Equal("true", cut.Find("[data-testid='party-scope-people']").GetAttribute("aria-pressed"));

        await cut.InvokeAsync(() => history.BackAsync().AsTask());

        cut.WaitForAssertion(() => {
            Assert.EndsWith("/crm-hr/directory?q=acme", navigation.Uri, StringComparison.Ordinal);
            Assert.Equal("true", cut.Find("[data-testid='party-scope-all']").GetAttribute("aria-pressed"));
            Assert.Equal("false", cut.Find("[data-testid='party-scope-people']").GetAttribute("aria-pressed"));
            Assert.Equal("acme", cut.Find("[data-testid='crmhr-directory-search']").GetAttribute("value"));
        });
    }

    [Fact]
    public async Task Workforce_location_opens_its_section_and_an_unknown_section_is_corrected_in_place() {
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddScoped<IAppNavigationHistory, TestNavigationHistory>());
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var hr = harness.Context.Services.GetRequiredService<HrService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var partyId = await CreatePartyAsync(parties, "Location workforce record", PartyRoleKind.Employee);
        var profile = await hr.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel {
            PartyId = partyId,
            WorkforceKind = WorkforceKind.Employee,
            Status = "Active",
            JobTitle = "Location tester",
            CapacityHoursPerWeek = 40m,
            LastChangedBy = "component-tests"
        });
        Assert.True(profile.IsSuccess);
        navigation.NavigateTo($"/crm-hr/workforce?partyId={partyId:D}&section=payroll&utm=campaign");
        var history = (TestNavigationHistory)harness.Context.Services.GetRequiredService<IAppNavigationHistory>();
        var cut = harness.Context.Render<CrmHrWorkforcePage>();
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;

        // The unknown section is not state: the location is corrected in place and keeps what it does not own.
        cut.WaitForAssertion(() => {
            Assert.NotNull(cut.Find("[data-testid='crmhr-workforce-record-dialog']"));
            Assert.EndsWith($"/crm-hr/workforce?partyId={partyId:D}&utm=campaign", navigation.Uri, StringComparison.Ordinal);
        });
        Assert.Equal((int)WorkforceSection.Overview, view.SelectedDetailTabIndex);
        Assert.Single(history.Locations);

        navigation.NavigateTo($"/crm-hr/workforce?partyId={partyId:D}&section=skills");

        cut.WaitForAssertion(() => Assert.Equal((int)WorkforceSection.Skills, view.SelectedDetailTabIndex));
        Assert.NotNull(cut.Find("[data-testid='crmhr-workforce-record-dialog']"));
    }

    [Fact]
    public async Task Crm_saving_a_new_opportunity_opens_it_even_when_the_location_names_another_one() {
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddScoped<IAppNavigationHistory, TestNavigationHistory>());
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var accountId = await CreateAccountAsync(parties, crm, "Location opportunity account");
        var ownerId = await CreatePartyAsync(parties, "Location opportunity owner", PartyRoleKind.AccountManager);
        var named = await crm.SaveOpportunityAsync(CreateOpportunity(accountId, ownerId, "Opportunity the location names"));
        Assert.True(named.IsSuccess, string.Join(" ", named.Errors.Select(error => error.Message)));
        navigation.NavigateTo($"/crm-hr/crm?accountId={accountId:D}&opportunityId={named.Value:D}");
        var history = (TestNavigationHistory)harness.Context.Services.GetRequiredService<IAppNavigationHistory>();
        var cut = harness.Context.Render<CrmHrCrmPage>();
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => {
            Assert.Equal(named.Value, view.SelectedOpportunityId);
            Assert.True(view.IsOpportunityDetailDialogOpen);
        });

        await cut.InvokeAsync(view.OpenOpportunityCreateDialog);
        Assert.True(view.IsOpportunityCreateDialogOpen);
        await cut.InvokeAsync(() => view.SaveOpportunityAsync(CreateOpportunity(accountId, ownerId, "Opportunity created over it")));

        // The created opportunity is shown, never the one the location named before.
        cut.WaitForAssertion(() => {
            Assert.Equal("Opportunity created over it", view.SelectedOpportunity?.Title);
            Assert.True(view.IsOpportunityDetailDialogOpen);
            Assert.False(view.IsOpportunityCreateDialogOpen);
        });
        var createdId = Assert.IsType<Guid>(view.SelectedOpportunityId);
        Assert.NotEqual(named.Value, createdId);
        Assert.EndsWith($"/crm-hr/crm?accountId={accountId:D}&opportunityId={createdId:D}", navigation.Uri, StringComparison.Ordinal);
        Assert.Equal(2, history.Locations.Count);
        Assert.Equal(1, history.Index);

        // It is its own entry: Back shows the opportunity the location named before.
        await cut.InvokeAsync(() => history.BackAsync().AsTask());

        cut.WaitForAssertion(() => {
            Assert.Equal(named.Value, view.SelectedOpportunityId);
            Assert.Equal("Opportunity the location names", view.SelectedOpportunity?.Title);
        });
        Assert.EndsWith($"/crm-hr/crm?accountId={accountId:D}&opportunityId={named.Value:D}", navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Crm_opportunity_opens_over_the_account_section_and_closing_goes_back_to_it() {
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddScoped<IAppNavigationHistory, TestNavigationHistory>());
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var accountId = await CreateAccountAsync(parties, crm, "Location pipeline account");
        var ownerId = await CreatePartyAsync(parties, "Location pipeline owner", PartyRoleKind.AccountManager);
        var opportunity = await crm.SaveOpportunityAsync(CreateOpportunity(accountId, ownerId, "Opportunity on the pipeline"));
        Assert.True(opportunity.IsSuccess, string.Join(" ", opportunity.Errors.Select(error => error.Message)));
        navigation.NavigateTo("/crm-hr/crm");
        var history = (TestNavigationHistory)harness.Context.Services.GetRequiredService<IAppNavigationHistory>();
        var cut = harness.Context.Render<CrmHrCrmPage>();
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        cut.WaitForElement("[data-testid='crmhr-crm-catalog']");

        await cut.InvokeAsync(() => view.SelectAccountAsync(accountId));
        cut.WaitForAssertion(() => Assert.True(view.IsAccountDialogOpen));
        await cut.InvokeAsync(() => view.HandleCrmRecordTabChanged((int)CrmSection.Opportunities));
        Assert.EndsWith($"/crm-hr/crm?accountId={accountId:D}&section=opportunities", navigation.Uri, StringComparison.Ordinal);
        Assert.Equal(2, history.Locations.Count);

        await cut.InvokeAsync(() => view.SelectOpportunityAsync(opportunity.Value));

        cut.WaitForAssertion(() => {
            Assert.Equal(opportunity.Value, view.SelectedOpportunityId);
            Assert.True(view.IsOpportunityDetailDialogOpen);
        });
        Assert.EndsWith($"/crm-hr/crm?accountId={accountId:D}&opportunityId={opportunity.Value:D}", navigation.Uri, StringComparison.Ordinal);
        Assert.Equal(3, history.Locations.Count);

        await cut.InvokeAsync(view.CloseOpportunityDetailDialogAsync);

        cut.WaitForAssertion(() => {
            Assert.False(view.IsOpportunityDetailDialogOpen);
            Assert.True(view.IsAccountDialogOpen);
            Assert.Equal((int)CrmSection.Opportunities, view.SelectedCrmRecordTabIndex);
        });
        Assert.EndsWith($"/crm-hr/crm?accountId={accountId:D}&section=opportunities", navigation.Uri, StringComparison.Ordinal);
        Assert.Equal(1, history.BackCount);
        Assert.Equal(1, history.Index);

        await cut.InvokeAsync(view.CloseAccountDialogAsync);

        cut.WaitForAssertion(() => Assert.False(view.IsAccountDialogOpen));
        Assert.EndsWith("/crm-hr/crm", navigation.Uri, StringComparison.Ordinal);
        Assert.Equal(2, history.BackCount);
        Assert.Equal(0, history.Index);
    }

    private static CrmOpportunityEditorModel CreateOpportunity(Guid accountId, Guid ownerId, string title)
        => new() {
            AccountPartyId = accountId,
            Title = title,
            Stage = OpportunityStage.Qualified,
            OpportunitySource = OpportunitySource.Direct,
            OwnerPartyId = ownerId,
            ProbabilityPercent = 40,
            LastChangedBy = "component-tests"
        };

    private static async Task<Guid> CreateAccountAsync(PartyDirectoryService parties, CrmService crm, string displayName) {
        var accountId = await CreatePartyAsync(parties, displayName, PartyRoleKind.Customer, PartyType.Organization, PartyLifecycleStatus.Prospect);
        var profile = await crm.SaveAccountProfileAsync(new CrmAccountProfileEditorModel {
            AccountPartyId = accountId,
            RelationshipStage = CrmAccountRelationshipStage.Prospect,
            CommercialNotes = $"{displayName} notes",
            LastChangedBy = "component-tests"
        });
        Assert.True(profile.IsSuccess);
        return accountId;
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService parties,
        string displayName,
        PartyRoleKind roleKind,
        PartyType partyType = PartyType.Person,
        PartyLifecycleStatus lifecycleStatus = PartyLifecycleStatus.Active) {
        var result = await parties.SavePartyAsync(new PartyEditorModel {
            PartyType = partyType,
            LifecycleStatus = lifecycleStatus,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "component-tests",
            Roles = [new PartyRoleAssignmentEditorModel { RoleKind = roleKind, Title = roleKind.ToString(), IsPrimary = true }]
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
