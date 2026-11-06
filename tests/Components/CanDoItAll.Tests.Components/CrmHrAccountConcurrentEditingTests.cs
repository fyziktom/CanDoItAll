using Bunit;
using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmHrAccountConcurrentEditingTests {
    [Fact]
    public async Task A_peer_creation_keeps_unblurred_commercial_input_and_expected_absence_until_explicit_apply_and_save() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var id = await CreateAccountAsync(harness);
        var cut = Open(harness, id);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(id, view.ProfileEditor.AccountPartyId));
        var draft = view.ProfileEditor;
        var form = cut.FindComponent<EditForm>().Instance.EditContext;
        Assert.Null(draft.Original!.ProfileId);
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-account-commercial-notes']").InputAsync(new ChangeEventArgs { Value = "Unblurred local commercial work" }));
        var peer = (await crm.GetAccountWorkspaceAsync(id))!.Profile;
        peer.ConstraintNotes = "Peer constraint";
        Assert.True((await crm.SaveAccountProfileAsync(peer)).IsSuccess);
        cut.WaitForElement("[data-testid='crmhr-account-freshness']");
        Assert.Equal("Unblurred local commercial work", draft.CommercialNotes);
        Assert.Empty(draft.ConstraintNotes);
        Assert.Null(draft.Original.ProfileId);
        Assert.True(cut.Find("[data-testid='crmhr-account-save-button']").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-account-review-open']").ClickAsync(new()));
        cut.WaitForElement("[data-testid='crmhr-account-review-dialog']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-account-review-apply']").ClickAsync(new()));

        Assert.Same(draft, view.ProfileEditor);
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Peer constraint", draft.ConstraintNotes);
        Assert.Empty((await crm.GetAccountProfileAsync(id))!.CommercialNotes);
        await cut.InvokeAsync(view.SaveAccountProfileAsync);
        Assert.Equal("Unblurred local commercial work", (await crm.GetAccountProfileAsync(id))!.CommercialNotes);
        Assert.Equal("Peer constraint", view.ProfileEditor.ConstraintNotes);
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
    }

    [Fact]
    public async Task Third_writer_invalidates_the_comparison_without_replacing_custom_review_work() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var id = await CreateAccountAsync(harness);
        var cut = Open(harness, id);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(id, view.ProfileEditor.AccountPartyId));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-account-commercial-notes']").InputAsync(new ChangeEventArgs { Value = "Local work" }));
        var peer = (await crm.GetAccountProfileAsync(id))!;
        peer.CommercialNotes = "Second writer";
        Assert.True((await crm.SaveAccountProfileAsync(peer)).IsSuccess);
        cut.WaitForElement("[data-testid='crmhr-account-review-open']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-account-review-open']").ClickAsync(new()));
        await cut.InvokeAsync(() => cut.Find("textarea[aria-label='Merged commercial notes']").InputAsync(new ChangeEventArgs { Value = "Custom comparison text" }));
        peer = (await crm.GetAccountProfileAsync(id))!;
        peer.CommercialNotes = "Third writer";
        Assert.True((await crm.SaveAccountProfileAsync(peer)).IsSuccess);

        cut.WaitForElement("[data-testid='crmhr-account-review-stale']");
        Assert.True(cut.Find("[data-testid='crmhr-account-review-apply']").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-account-review-refresh']").ClickAsync(new()));

        cut.WaitForAssertion(() => Assert.Equal("Custom comparison text", cut.Find("textarea[aria-label='Merged commercial notes']").GetAttribute("value")));
        Assert.Equal("Local work", view.ProfileEditor.CommercialNotes);
    }

    [Fact]
    public async Task Clean_profile_updates_keep_the_root_form_and_disposal_releases_the_owned_observations() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var id = await CreateAccountAsync(harness);
        var feed = harness.Context.Services.GetRequiredService<EditingChangeFeed>();
        var subscriptionsBefore = feed.ActiveSubscriptions;
        var cut = Open(harness, id);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(id, view.ProfileEditor.AccountPartyId));
        var draft = view.ProfileEditor;
        var form = cut.FindComponent<EditForm>().Instance.EditContext;
        var peer = (await crm.GetAccountProfileAsync(id))!;
        peer.ConstraintNotes = "Clean peer update";
        Assert.True((await crm.SaveAccountProfileAsync(peer)).IsSuccess);

        cut.WaitForAssertion(() => Assert.Equal(peer.ConstraintNotes, view.ProfileEditor.ConstraintNotes));

        Assert.Same(draft, view.ProfileEditor);
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.True(feed.ActiveSubscriptions > subscriptionsBefore);
        await harness.Context.DisposeRenderedComponentsAsync();
        Assert.Equal(subscriptionsBefore, feed.ActiveSubscriptions);
        Assert.Equal(0, feed.ActiveTopics);
    }

    [Fact]
    public async Task Closing_a_dirty_profile_requires_a_decision_and_does_not_write_it() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var id = await CreateAccountAsync(harness);
        var cut = Open(harness, id);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(id, view.ProfileEditor.AccountPartyId));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-account-commercial-notes']").InputAsync(new ChangeEventArgs { Value = "Unsubmitted profile" }));
        var closing = cut.InvokeAsync(view.CloseAccountDialogAsync);
        cut.WaitForElement("[data-testid='crmhr-account-dirty-close']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-account-dirty-continue']").ClickAsync(new()));
        await closing;
        Assert.True(view.IsAccountDialogOpen);
        Assert.Equal("Unsubmitted profile", view.ProfileEditor.CommercialNotes);
        closing = cut.InvokeAsync(view.CloseAccountDialogAsync);
        cut.WaitForElement("[data-testid='crmhr-account-dirty-close']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-account-dirty-discard']").ClickAsync(new()));
        await closing;

        Assert.False(view.IsAccountDialogOpen);
        Assert.Empty((await crm.GetAccountProfileAsync(id))!.CommercialNotes);
    }

    private static IRenderedComponent<CrmHrCrmPage> Open(ComponentTestHarness harness, Guid id) {
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr/crm?accountId={id:D}");
        return harness.Context.Render<CrmHrCrmPage>();
    }

    private static async Task<Guid> CreateAccountAsync(ComponentTestHarness harness) {
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var result = await parties.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Organization, DisplayName = "Concurrent account profile" });
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
