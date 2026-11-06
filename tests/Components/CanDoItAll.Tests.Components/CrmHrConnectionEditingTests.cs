using Bunit;
using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmHrConnectionEditingTests {
    [Fact]
    public async Task Peer_membership_and_field_changes_are_reviewed_locally_then_saved_without_replacing_the_form() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, account, person) = await OpenAsync(harness);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var rows = view.ConnectedRecordEditors;
        var local = Assert.Single(rows);
        var context = Context(cut, rows);
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-connection-notes-0']").InputAsync(new ChangeEventArgs { Value = "  My local connection note  " }));
        var observed = (await crm.GetAccountWorkspaceAsync(account))!;
        var peer = observed.ConnectedRecords.Select(ConnectionValues.FromOwner).ToList();
        peer[0].Role = CrmAccountConnectionRole.Sponsor;
        peer.Add(new() { Id = Guid.NewGuid(), RelatedPartyId = person, Role = CrmAccountConnectionRole.PrimaryContact });

        Assert.True((await crm.SaveConnectedRecordsAsync(account, peer, "peer", original: observed.ConnectionsOriginal)).IsSuccess);
        cut.WaitForElement("[data-testid='crmhr-connections-freshness']");
        await cut.Find("[data-testid='crmhr-connections-review-open']").ClickAsync(new());
        await cut.Find("[data-testid='crmhr-connections-review-apply']").ClickAsync(new());

        Assert.Same(rows, view.ConnectedRecordEditors);
        Assert.Same(local, rows.Single(row => row.Id == local.Id));
        Assert.Same(context, Context(cut, rows));
        Assert.Equal("  My local connection note  ", local.Notes);
        Assert.Equal(CrmAccountConnectionRole.Sponsor, local.Role);
        Assert.Equal(2, rows.Count);
        Assert.Equal("Original connection", (await crm.GetAccountWorkspaceAsync(account))!.ConnectedRecords.Single(row => row.Id == local.Id).Notes);
        await cut.Find("[data-testid='crmhr-connections-form']").SubmitAsync();

        var saved = (await crm.GetAccountWorkspaceAsync(account))!.ConnectedRecords;
        Assert.Equal(2, saved.Count);
        Assert.Equal("My local connection note", saved.Single(row => row.Id == local.Id).Notes);
    }

    [Fact]
    public async Task Later_raw_notes_and_a_new_row_survive_exact_native_acknowledgement_in_the_same_context() {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(services => control = CrmHrOwnerReadBackControl.Install(services));
        var (cut, account, person) = await OpenAsync(harness);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        var rows = view.ConnectedRecordEditors;
        var first = rows[0];
        var context = Context(cut, rows);
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-connection-notes-0']").InputAsync(new ChangeEventArgs { Value = "Submitted connection" }));
        control.HoldWriteIn(nameof(CrmService.SaveConnectedRecordsAsync));
        var pending = cut.Find("[data-testid='crmhr-connections-form']").SubmitAsync();
        await control.HoldReached.WaitAsync(TimeSpan.FromSeconds(30));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-connection-notes-0']").InputAsync(new ChangeEventArgs { Value = "  Later raw connection note  " }));
        await cut.InvokeAsync(view.AddConnectedRecord);
        rows[^1].RelatedPartyId = person;
        rows[^1].Role = CrmAccountConnectionRole.PrimaryContact;
        control.Release();
        await pending.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Same(rows, view.ConnectedRecordEditors);
        Assert.Same(first, rows[0]);
        Assert.Same(context, Context(cut, rows));
        Assert.Equal("  Later raw connection note  ", first.Notes);
        Assert.Equal(2, rows.Count);
        Assert.Equal("Submitted connection", Assert.Single((await harness.Context.Services.GetRequiredService<CrmService>().GetAccountWorkspaceAsync(account))!.ConnectedRecords).Notes);
    }

    private static EditContext Context(IRenderedComponent<CrmHrCrmPage> cut, List<CrmAccountConnectionEditorModel> rows) =>
        cut.FindComponents<EditForm>().Single(form => ReferenceEquals(form.Instance.EditContext.Model, rows)).Instance.EditContext;

    private static async Task<(IRenderedComponent<CrmHrCrmPage> Page, Guid Account, Guid Person)> OpenAsync(ComponentTestHarness harness) {
        var directory = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var account = await directory.SavePartyAsync(new() { PartyType = PartyType.Organization, DisplayName = "Connection native account" });
        var person = await directory.SavePartyAsync(new() { PartyType = PartyType.Person, DisplayName = "Connection native person" });
        Assert.True(account.IsSuccess);
        Assert.True(person.IsSuccess);
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        Assert.True((await crm.SaveConnectedRecordsAsync(account.Value, [new() { RelatedPartyId = person.Value, Notes = "Original connection" }], "setup",
            original: (await crm.GetAccountWorkspaceAsync(account.Value))!.ConnectionsOriginal)).IsSuccess);
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr/crm?accountId={account.Value:D}&section=connected-records");
        var cut = harness.Context.Render<CrmHrCrmPage>();
        cut.WaitForElement("[data-testid='crmhr-connection-notes-0']");
        return (cut, account.Value, person.Value);
    }
}
