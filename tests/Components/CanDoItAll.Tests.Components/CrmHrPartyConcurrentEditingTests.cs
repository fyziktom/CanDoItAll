using CanDoItAll.Components.BaseLib;
using AngleSharp.Html.Dom;
using Bunit;
using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.CrmHr.UI.Pickers;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmHrPartyConcurrentEditingTests {
    [Fact]
    public async Task A_clean_peer_deletion_keeps_the_surviving_confidential_note_control_bound_to_its_original_row() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var created = await parties.SavePartyAsync(new PartyEditorModel {
            DisplayName = "Confidential row identity", IsSensitive = true,
            ConfidentialNotes = [new() { NoteText = "First note" }, new() { NoteText = "Surviving second note" }]
        });
        Assert.True(created.IsSuccess);
        var cut = Open(harness, created.Value);
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(2, view.Editor.ConfidentialNotes.Count));
        var input = cut.FindComponents<NativeValueInput<string>>().Single(component => component.Instance.Value == "Surviving second note").Instance;
        var survivor = view.Editor.ConfidentialNotes.Single(note => note.NoteText == "Surviving second note");
        var peer = (await parties.GetPartyAsync(created.Value))!;
        peer.ConfidentialNotes.RemoveAll(note => note.NoteText == "First note");

        Assert.True((await parties.SavePartyAsync(peer)).IsSuccess);

        cut.WaitForAssertion(() => Assert.Single(view.Editor.ConfidentialNotes));
        Assert.Same(survivor, Assert.Single(view.Editor.ConfidentialNotes));
        Assert.Same(input, cut.FindComponents<NativeValueInput<string>>().Single(component => component.Instance.Value == "Surviving second note").Instance);
    }

    [Fact]
    public async Task Unblurred_text_and_pending_tag_input_retain_the_same_dirty_form_during_peer_changes() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var id = await CreatePartyAsync(parties);
        var cut = Open(harness, id);
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(id, view.Editor.Id));
        var context = cut.FindComponent<EditForm>().Instance.EditContext;
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-party-summary']").InputAsync(new ChangeEventArgs { Value = "Still typing without blur" }));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-party-tags']").InputAsync(new ChangeEventArgs { Value = "unfinished-tag" }));
        var peer = (await parties.GetPartyAsync(id))!;
        peer.DisplayName = "Peer change while typing";
        Assert.True((await parties.SavePartyAsync(peer)).IsSuccess);

        cut.WaitForElement("[data-testid='crmhr-party-freshness']");

        Assert.Equal("Still typing without blur", view.Editor.Summary);
        Assert.Equal("unfinished-tag", ((IHtmlInputElement)cut.Find("[data-testid='crmhr-party-tags']")).Value);
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Concurrent UI party", view.Editor.DisplayName);
        Assert.True(cut.Find("[data-testid='crmhr-party-save-button']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Quiet_catalog_refresh_keeps_the_filter_and_open_target_when_a_peer_removes_the_target_from_results() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var id = await CreatePartyAsync(parties);
        var cut = Open(harness, id);
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(id, view.Editor.Id));
        var browser = cut.FindComponent<PartyRecordBrowser>().Instance;
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-directory-search']").InputAsync(new ChangeEventArgs { Value = "Concurrent" }));
        await cut.InvokeAsync(() => view.Editor.Summary = "Local summary outside the filter");
        var peer = (await parties.GetPartyAsync(id))!;
        peer.DisplayName = "Renamed by peer";
        Assert.True((await parties.SavePartyAsync(peer)).IsSuccess);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-directory-empty']")));

        Assert.Same(browser, cut.FindComponent<PartyRecordBrowser>().Instance);
        Assert.Equal("Concurrent", cut.Find("[data-testid='crmhr-directory-search']").GetAttribute("value"));
        Assert.Equal(id, view.Editor.Id);
        Assert.Equal("Local summary outside the filter", view.Editor.Summary);
        Assert.True(view.IsEditorDialogOpen);
    }

    [Fact]
    public async Task Dirty_draft_review_and_apply_keep_the_form_alive_and_save_remains_separate() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var id = await CreatePartyAsync(parties);
        var cut = Open(harness, id);
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(id, view.Editor.Id));
        var draft = view.Editor;
        var form = cut.FindComponent<EditForm>().Instance.EditContext;
        await cut.InvokeAsync(() => draft.Summary = "Local summary");
        var originalStamp = draft.UpdatedAtUtc;
        var peer = (await parties.GetPartyAsync(id))!;
        peer.DisplayName = "Peer name";
        Assert.True((await parties.SavePartyAsync(peer)).IsSuccess);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-party-freshness']")));
        Assert.Equal(originalStamp, draft.UpdatedAtUtc);
        Assert.Equal("Local summary", draft.Summary);
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-party-review-open']").ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-party-review-dialog']")));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-party-review-apply']").ClickAsync(new()));

        Assert.Same(draft, view.Editor);
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal(peer.DisplayName, draft.DisplayName);
        Assert.Equal("Local summary", draft.Summary);
        Assert.Equal("Original summary", (await parties.GetPartyAsync(id))!.Summary);
        await cut.InvokeAsync(view.SaveAsync);
        Assert.Equal("Local summary", (await parties.GetPartyAsync(id))!.Summary);
        Assert.Equal(peer.DisplayName, view.Editor.DisplayName);
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
    }

    [Fact]
    public async Task A_third_writer_invalidates_the_open_comparison_and_refresh_keeps_its_custom_input() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var id = await CreatePartyAsync(parties);
        var cut = Open(harness, id);
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(id, view.Editor.Id));
        await cut.InvokeAsync(() => view.Editor.DisplayName = "Local name");
        var peer = (await parties.GetPartyAsync(id))!;
        peer.DisplayName = "Second writer";
        Assert.True((await parties.SavePartyAsync(peer)).IsSuccess);
        cut.WaitForElement("[data-testid='crmhr-party-review-open']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-party-review-open']").ClickAsync(new()));
        await cut.InvokeAsync(() => cut.Find("input[aria-label='Merged Display name']").InputAsync(new ChangeEventArgs { Value = "Work inside review" }));
        peer = (await parties.GetPartyAsync(id))!;
        peer.DisplayName = "Third writer";
        Assert.True((await parties.SavePartyAsync(peer)).IsSuccess);

        cut.WaitForAssertion(() => {
            Assert.NotNull(cut.Find("[data-testid='crmhr-party-review-stale']"));
            Assert.True(cut.Find("[data-testid='crmhr-party-review-apply']").HasAttribute("disabled"));
        });
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-party-review-refresh']").ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.Equal("Work inside review", ((IHtmlInputElement)cut.Find("input[aria-label='Merged Display name']")).Value));
        Assert.Equal("Local name", view.Editor.DisplayName);
    }

    [Fact]
    public async Task Dirty_close_can_be_cancelled_or_explicitly_discarded_without_writing() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var id = await CreatePartyAsync(parties);
        var cut = Open(harness, id);
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(id, view.Editor.Id));
        await cut.InvokeAsync(() => view.Editor.Summary = "Unsaved summary");
        var close = cut.InvokeAsync(view.CloseEditorDialogAsync);
        cut.WaitForElement("[data-testid='crmhr-party-dirty-close']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-party-dirty-continue']").ClickAsync(new()));
        await close;
        Assert.True(view.IsEditorDialogOpen);
        Assert.Equal("Unsaved summary", view.Editor.Summary);
        close = cut.InvokeAsync(view.CloseEditorDialogAsync);
        cut.WaitForElement("[data-testid='crmhr-party-dirty-close']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-party-dirty-discard']").ClickAsync(new()));
        await close;

        Assert.False(view.IsEditorDialogOpen);
        Assert.Equal("Original summary", (await parties.GetPartyAsync(id))!.Summary);
    }

    [Fact]
    public async Task Clean_peer_changes_refresh_in_place_and_disposal_releases_the_owned_observation() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var id = await CreatePartyAsync(parties);
        var feed = harness.Context.Services.GetRequiredService<EditingChangeFeed>();
        var subscriptionsBefore = feed.ActiveSubscriptions;
        var cut = Open(harness, id);
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(id, view.Editor.Id));
        var draft = view.Editor;
        var context = cut.FindComponent<EditForm>().Instance.EditContext;
        var peer = (await parties.GetPartyAsync(id))!;
        peer.DisplayName = "Refreshed clean party";
        Assert.True((await parties.SavePartyAsync(peer)).IsSuccess);
        cut.WaitForAssertion(() => Assert.Equal(peer.DisplayName, view.Editor.DisplayName));
        Assert.Same(draft, view.Editor);
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.True(feed.ActiveSubscriptions > subscriptionsBefore);

        await harness.Context.DisposeRenderedComponentsAsync();

        Assert.Equal(subscriptionsBefore, feed.ActiveSubscriptions);
        Assert.Equal(0, feed.ActiveTopics);
    }

    private static IRenderedComponent<CrmHrDirectoryPage> Open(ComponentTestHarness harness, Guid id) {
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr/directory?partyId={id:D}");
        return harness.Context.Render<CrmHrDirectoryPage>();
    }

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService parties) {
        var result = await parties.SavePartyAsync(new PartyEditorModel {
            PartyType = PartyType.Organization,
            DisplayName = "Concurrent UI party",
            Summary = "Original summary",
            Roles = [new() { RoleKind = PartyRoleKind.Customer, Title = "Customer", IsPrimary = true }]
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
