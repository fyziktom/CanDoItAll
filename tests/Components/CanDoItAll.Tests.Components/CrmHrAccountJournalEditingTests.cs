using Bunit;
using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmHrAccountJournalEditingTests {
    [Fact]
    public async Task A_peer_append_refreshes_an_initially_empty_timeline_without_replacing_the_local_journal_draft() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, accountId) = await OpenAsync(harness);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(0, view.AccountActivityPresentation.Accepted?.TotalCount));
        var draft = view.InteractionEditor;
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-interaction-notes']").InputAsync(new ChangeEventArgs { Value = "  My unfinished journal  " }));

        var saved = await harness.Context.Services.GetRequiredService<CrmService>().AddInteractionAsync(accountId,
            new() { Subject = "Peer timeline append" }, "peer");

        Assert.True(saved.IsSuccess);
        cut.WaitForAssertion(() => {
            Assert.Equal(2, view.AccountActivityPresentation.Accepted?.TotalCount);
            Assert.Contains(view.AccountActivityPresentation.Accepted!.Items, item => item.Title == "Peer timeline append");
        });
        Assert.Same(draft, view.InteractionEditor);
        Assert.Equal("  My unfinished journal  ", draft.Notes);
    }

    [Fact]
    public async Task A_known_append_clears_the_same_form_before_a_failed_read_back_and_cannot_be_logged_twice() {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(services => control = CrmHrOwnerReadBackControl.Install(services));
        var (cut, accountId) = await OpenAsync(harness);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        var draft = view.InteractionEditor;
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-interaction-subject']").InputAsync(new ChangeEventArgs { Value = "  Accepted journal entry  " }));
        var context = ComponentTestHarness.FindEditContext(cut, draft);
        control.FailReadIn(nameof(CrmService.GetAccountWorkspaceAsync), "ReloadSelectedAccountAsync");

        await cut.Find("[data-testid='crmhr-interaction-form']").SubmitAsync();

        Assert.Same(draft, view.InteractionEditor);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, draft));
        Assert.Empty(draft.Subject);
        Assert.Contains("could not be refreshed", cut.Find("[data-testid='crmhr-interaction-outcome']").TextContent);
        cut.WaitForAssertion(() => {
            var button = cut.Find("[data-testid='crmhr-interaction-save-button']");
            Assert.True(button.HasAttribute("disabled"), button.OuterHtml);
        });
        await cut.InvokeAsync(view.SaveInteractionAsync);
        await using var read = await harness.Context.Services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        var saved = Assert.Single(await read.Set<InteractionRecord>().ToListAsync());
        Assert.Equal("Accepted journal entry", saved.Subject);
        Assert.True(await read.Set<InteractionPartyLink>().AnyAsync(row => row.InteractionId == saved.Id && row.PartyId == accountId));
    }

    [Fact]
    public async Task Later_unblurred_journal_text_and_invalid_date_stay_in_the_same_form_after_native_acceptance() {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(services => control = CrmHrOwnerReadBackControl.Install(services));
        var (cut, _) = await OpenAsync(harness);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        var draft = view.InteractionEditor;
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-interaction-subject']").InputAsync(new ChangeEventArgs { Value = "Submitted journal" }));
        var context = ComponentTestHarness.FindEditContext(cut, draft);
        control.HoldWriteIn(nameof(CrmService.AddInteractionAsync));
        var save = cut.InvokeAsync(view.SaveInteractionAsync);
        await control.HoldReached.WaitAsync(TimeSpan.FromSeconds(30));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-interaction-notes']").InputAsync(new ChangeEventArgs { Value = "  Later unblurred notes  " }));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-next-action-due-on']").ChangeAsync(new ChangeEventArgs { Value = "invalid-date" }));
        control.Release();
        await save.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Same(draft, view.InteractionEditor);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, draft));
        Assert.Equal("  Later unblurred notes  ", draft.Notes);
        Assert.Contains(context.GetValidationMessages(), message => message.Contains("date", StringComparison.OrdinalIgnoreCase));
        await using var read = await harness.Context.Services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        var saved = Assert.Single(await read.Set<InteractionRecord>().ToListAsync());
        Assert.Equal("Submitted journal", saved.Subject);
        Assert.Empty(saved.Notes);
    }

    private static async Task<(IRenderedComponent<CrmHrCrmPage> Page, Guid AccountId)> OpenAsync(ComponentTestHarness harness) {
        var saved = await harness.Context.Services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new() {
            PartyType = PartyType.Organization, DisplayName = "Native journal account"
        });
        Assert.True(saved.IsSuccess);
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr/crm?accountId={saved.Value:D}&section=interactions");
        var cut = harness.Context.Render<CrmHrCrmPage>();
        cut.WaitForElement("[data-testid='crmhr-interaction-subject']");
        return (cut, saved.Value);
    }
}
