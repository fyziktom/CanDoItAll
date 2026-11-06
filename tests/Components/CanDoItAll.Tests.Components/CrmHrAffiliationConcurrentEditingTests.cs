using CanDoItAll.Components.BaseLib;
using Bunit;
using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmHrAffiliationConcurrentEditingTests {
    [Fact]
    public async Task Unblurred_affiliation_input_and_its_form_survive_a_peer_change_until_explicit_apply_and_separate_save() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (id, service, rows) = await SeedAsync(harness);
        var cut = Open(harness, id);
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForElement("[data-testid='crmhr-affiliation-title-0']");
        var form = cut.FindComponent<EditForm>().Instance.EditContext;
        var draft = view.Editor;
        var row = view.AffiliationEditors[0];
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-affiliation-title-0']").InputAsync(new ChangeEventArgs { Value = "  Still typing  " }));
        await cut.InvokeAsync(() => view.Editor.Summary = "Local root summary");
        var peer = AffiliationValues.FromOwner(rows[1]);
        peer.Notes = "Peer affiliation note";

        Assert.True((await service.UpsertAsync(peer, "affiliation-peer")).IsSuccess);
        cut.WaitForElement("[data-testid='crmhr-affiliation-freshness']");
        Assert.Empty(cut.FindAll("[data-testid='crmhr-party-freshness']"));
        Assert.Equal("  Still typing  ", view.AffiliationEditors[0].JobTitle);
        Assert.Same(draft, view.Editor);
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-affiliation-review-open']").ClickAsync(new()));
        cut.WaitForElement("[data-testid='crmhr-affiliation-review-dialog']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-affiliation-review-apply']").ClickAsync(new()));

        Assert.Same(row, view.AffiliationEditors[0]);
        Assert.Equal("  Still typing  ", view.AffiliationEditors[0].JobTitle);
        Assert.Equal("Peer affiliation note", view.AffiliationEditors[1].Notes);
        Assert.Equal("Original title", (await service.ListAsync(id))[0].JobTitle);
        await cut.InvokeAsync(view.SaveAsync);

        Assert.Equal("Still typing", (await service.ListAsync(id))[0].JobTitle);
        Assert.Equal("Peer affiliation note", (await service.ListAsync(id))[1].Notes);
        Assert.Equal("Local root summary", view.Editor.Summary);
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
    }

    [Fact]
    public async Task Clean_affiliation_refresh_keeps_controls_and_disposal_releases_all_four_owned_subscriptions() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (id, service, rows) = await SeedAsync(harness);
        var cut = Open(harness, id);
        cut.WaitForElement("[data-testid='crmhr-affiliation-title-0']");
        var control = cut.FindComponents<NativeValueInput<string>>().Single(component => component.Instance.Value == "Original title").Instance;
        var form = cut.FindComponent<EditForm>().Instance.EditContext;
        var peer = AffiliationValues.FromOwner(rows[0]);
        peer.JobTitle = "Clean peer title";

        Assert.True((await service.UpsertAsync(peer, "affiliation-peer")).IsSuccess);

        cut.WaitForAssertion(() => Assert.Equal("Clean peer title", cut.Find("[data-testid='crmhr-affiliation-title-0']").GetAttribute("value")));
        Assert.Same(control, cut.FindComponents<NativeValueInput<string>>().Single(component => component.Instance.Value == "Clean peer title").Instance);
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
        var feed = harness.Context.Services.GetRequiredService<EditingChangeFeed>();
        Assert.Equal(4, feed.ActiveSubscriptions);
        await harness.Context.DisposeRenderedComponentsAsync();
        Assert.Equal(0, feed.ActiveSubscriptions);
        Assert.Equal(0, feed.ActiveTopics);
    }

    [Fact]
    public async Task Closing_a_draft_with_only_affiliation_edits_requires_an_explicit_decision_and_never_writes() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (id, service, _) = await SeedAsync(harness);
        var cut = Open(harness, id);
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForElement("[data-testid='crmhr-affiliation-title-0']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-affiliation-title-0']").InputAsync(new ChangeEventArgs { Value = "Unsaved affiliation" }));
        var close = cut.InvokeAsync(view.CloseEditorDialogAsync);
        cut.WaitForElement("[data-testid='crmhr-party-dirty-close']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-party-dirty-continue']").ClickAsync(new()));
        await close;
        Assert.True(view.IsEditorDialogOpen);
        Assert.Equal("Unsaved affiliation", view.AffiliationEditors[0].JobTitle);
        close = cut.InvokeAsync(view.CloseEditorDialogAsync);
        cut.WaitForElement("[data-testid='crmhr-party-dirty-close']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-party-dirty-discard']").ClickAsync(new()));
        await close;
        Assert.False(view.IsEditorDialogOpen);
        Assert.Equal("Original title", (await service.ListAsync(id))[0].JobTitle);
    }

    private static IRenderedComponent<CrmHrDirectoryPage> Open(ComponentTestHarness harness, Guid id) {
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr/directory?partyId={id:D}&section=relationships");
        return harness.Context.Render<CrmHrDirectoryPage>();
    }

    private static async Task<(Guid Id, IPartyOrganizationAffiliationService Service, IReadOnlyList<PartyOrganizationAffiliationListItemModel> Rows)> SeedAsync(ComponentTestHarness harness) {
        var services = harness.Context.Services;
        var directory = services.GetRequiredService<PartyDirectoryService>();
        var person = await directory.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Person, DisplayName = "Affiliation UI person" });
        var organization = await directory.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Organization, DisplayName = "Affiliation UI organization" });
        Assert.True(person.IsSuccess);
        Assert.True(organization.IsSuccess);
        var service = services.GetRequiredService<IPartyOrganizationAffiliationService>();
        var saved = await service.ReplaceAsync(person.Value, [
            new() { Id = Guid.NewGuid(), IsNew = true, PersonPartyId = person.Value, OrganizationPartyId = organization.Value, JobTitle = "Original title", IsPrimary = true },
            new() { Id = Guid.NewGuid(), IsNew = true, PersonPartyId = person.Value, OrganizationPartyId = organization.Value, AffiliationKind = PartyOrganizationAffiliationKind.Contractor, JobTitle = "Second title" }
        ], "affiliation-test");
        Assert.True(saved.IsSuccess);
        return (person.Value, service, saved.Value!);
    }
}
