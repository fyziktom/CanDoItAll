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

public sealed class CrmHrRelationshipConcurrentEditingTests {
    [Fact]
    public async Task Unblurred_relationship_input_and_its_form_survive_a_peer_change_until_explicit_apply_and_separate_save() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (id, service, rows) = await SeedAsync(harness);
        var cut = Open(harness, id);
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForElement("[data-testid='crmhr-relationship-notes-0']");
        var form = cut.FindComponent<EditForm>().Instance.EditContext;
        var draft = view.Editor;
        var row = view.Relationships[0];
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-relationship-notes-0']").InputAsync(new ChangeEventArgs { Value = "  Still typing  " }));
        await cut.InvokeAsync(() => view.Editor.Summary = "Local root summary");
        var peer = RelationshipValues.FromOwner(rows[1]);
        peer.Notes = "Peer relationship note";

        Assert.True((await service.ReplaceAsync(id, [RelationshipValues.FromOwner(rows[0]), peer], PartyRelationshipObservation.FromRows(rows), "relationship-peer")).IsSuccess);
        cut.WaitForElement("[data-testid='crmhr-relationship-freshness']");
        Assert.Empty(cut.FindAll("[data-testid='crmhr-party-freshness']"));
        Assert.Equal("  Still typing  ", view.Relationships[0].Notes);
        Assert.Same(draft, view.Editor);
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-relationship-review-open']").ClickAsync(new()));
        cut.WaitForElement("[data-testid='crmhr-relationship-review-dialog']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-relationship-review-apply']").ClickAsync(new()));

        Assert.Same(row, view.Relationships[0]);
        Assert.Equal("  Still typing  ", view.Relationships[0].Notes);
        Assert.Equal("Peer relationship note", view.Relationships[1].Notes);
        Assert.Equal("Original title", (await service.ListAsync(id))[0].Notes);
        await cut.InvokeAsync(view.SaveAsync);

        Assert.Equal("Still typing", (await service.ListAsync(id))[0].Notes);
        Assert.Equal("Peer relationship note", (await service.ListAsync(id))[1].Notes);
        Assert.Equal("Local root summary", view.Editor.Summary);
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
    }

    [Fact]
    public async Task Clean_relationship_refresh_keeps_controls_and_disposal_releases_all_four_owned_subscriptions() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (id, service, rows) = await SeedAsync(harness);
        var cut = Open(harness, id);
        cut.WaitForElement("[data-testid='crmhr-relationship-notes-0']");
        var control = cut.FindComponents<NativeValueInput<string>>().Single(component => component.Instance.Value == "Original title").Instance;
        var form = cut.FindComponent<EditForm>().Instance.EditContext;
        var peer = RelationshipValues.FromOwner(rows[0]);
        peer.Notes = "Clean peer title";

        Assert.True((await service.ReplaceAsync(id, [peer, RelationshipValues.FromOwner(rows[1])], PartyRelationshipObservation.FromRows(rows), "relationship-peer")).IsSuccess);

        cut.WaitForAssertion(() => Assert.Equal("Clean peer title", cut.FindComponents<NativeValueInput<string>>().Single(component => component.Instance.Value == "Clean peer title").Instance.Value));
        Assert.Same(control, cut.FindComponents<NativeValueInput<string>>().Single(component => component.Instance.Value == "Clean peer title").Instance);
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
        var feed = harness.Context.Services.GetRequiredService<EditingChangeFeed>();
        Assert.Equal(4, feed.ActiveSubscriptions);
        await harness.Context.DisposeRenderedComponentsAsync();
        Assert.Equal(0, feed.ActiveSubscriptions);
        Assert.Equal(0, feed.ActiveTopics);
    }

    [Fact]
    public async Task Closing_a_draft_with_only_relationship_edits_requires_an_explicit_decision_and_never_writes() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (id, service, _) = await SeedAsync(harness);
        var cut = Open(harness, id);
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForElement("[data-testid='crmhr-relationship-notes-0']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-relationship-notes-0']").InputAsync(new ChangeEventArgs { Value = "Unsaved relationship" }));
        var close = cut.InvokeAsync(view.CloseEditorDialogAsync);
        cut.WaitForElement("[data-testid='crmhr-party-dirty-close']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-party-dirty-continue']").ClickAsync(new()));
        await close;
        Assert.True(view.IsEditorDialogOpen);
        Assert.Equal("Unsaved relationship", view.Relationships[0].Notes);
        close = cut.InvokeAsync(view.CloseEditorDialogAsync);
        cut.WaitForElement("[data-testid='crmhr-party-dirty-close']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-party-dirty-discard']").ClickAsync(new()));
        await close;
        Assert.False(view.IsEditorDialogOpen);
        Assert.Equal("Original title", (await service.ListAsync(id))[0].Notes);
    }

    private static IRenderedComponent<CrmHrDirectoryPage> Open(ComponentTestHarness harness, Guid id) {
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr/directory?partyId={id:D}&section=relationships");
        return harness.Context.Render<CrmHrDirectoryPage>();
    }

    private static async Task<(Guid Id, IPartyRelationshipService Service, IReadOnlyList<PartyRelationshipListItemModel> Rows)> SeedAsync(ComponentTestHarness harness) {
        var services = harness.Context.Services;
        var directory = services.GetRequiredService<PartyDirectoryService>();
        var person = await directory.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Person, DisplayName = "Relationship UI person" });
        var first = await directory.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Organization, DisplayName = "Relationship UI first" });
        var second = await directory.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Organization, DisplayName = "Relationship UI second" });
        Assert.True(person.IsSuccess);
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var service = services.GetRequiredService<IPartyRelationshipService>();
        var saved = await service.ReplaceAsync(person.Value, [
            new() { Id = Guid.NewGuid(), RelatedPartyId = first.Value, Notes = "Original title" },
            new() { Id = Guid.NewGuid(), RelatedPartyId = second.Value, Notes = "Second title" }
        ], new([]), "relationship-test");
        Assert.True(saved.IsSuccess);
        return (person.Value, service, saved.Value!.Accepted);
    }
}
