using Bunit;
using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmHrStewardshipCommandTests {
    [Fact]
    public async Task Merge_rejects_a_peer_change_preserves_its_note_and_requires_a_current_preview() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var (retained, duplicate) = await CreateDuplicatePairAsync(parties);
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr/directory?partyId={retained:D}");
        var cut = harness.Context.Render<CrmHrDirectoryPage>();
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForElement("[data-testid='crmhr-directory-tab-relationships']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-directory-tab-relationships']").ClickAsync(new()));
        cut.WaitForElement($"[data-testid='crmhr-merge-open-{duplicate:N}']");
        await cut.InvokeAsync(() => cut.Find($"[data-testid='crmhr-merge-open-{duplicate:N}']").ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.True(view.Merge.CanConfirm));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-merge-reason']").Input("Retain this local merge note"));
        var current = (await parties.GetPartyAsync(duplicate))!;
        current.Summary = "Peer data changed after preview";
        Assert.True((await parties.SavePartyAsync(current)).IsSuccess);

        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-merge-confirm']").ClickAsync(new()));

        cut.WaitForAssertion(() => Assert.False(view.Merge.CanConfirm));
        Assert.Equal(PartyMergePhase.Ready, view.Merge.Phase);
        Assert.Contains("changed", view.Merge.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Retain this local merge note", cut.Find("[data-testid='crmhr-merge-reason']").GetAttribute("value"));
        Assert.NotNull(await parties.GetPartyAsync(duplicate));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-merge-refresh']").ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.True(view.Merge.CanConfirm));
        Assert.Equal("Retain this local merge note", cut.Find("[data-testid='crmhr-merge-reason']").GetAttribute("value"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-merge-confirm']").ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.True(view.Merge.IsAccepted));
        Assert.Null(await parties.GetPartyAsync(duplicate));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-merge-confirm']"));
    }

    [Fact]
    public async Task A_local_party_draft_is_kept_until_saved_or_discarded_before_business_merge() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var (retained, duplicate) = await CreateDuplicatePairAsync(parties);
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr/directory?partyId={retained:D}");
        var cut = harness.Context.Render<CrmHrDirectoryPage>();
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForElement("[data-testid='crmhr-party-summary']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-party-summary']").Input("Unsaved parent text"));
        var candidate = Assert.Single(await harness.Context.Services.GetRequiredService<PartyDirectoryManagementService>().FindPotentialDuplicatesAsync(retained), item => item.Id == duplicate);

        await cut.InvokeAsync(() => view.OpenMergeDialogAsync(candidate));

        Assert.False(view.IsMergeDialogOpen);
        Assert.Equal("Unsaved parent text", view.Editor.Summary);
        Assert.NotNull(await parties.GetPartyAsync(duplicate));
    }

    [Fact]
    public async Task Closing_csv_requires_an_explicit_decision_and_continuing_keeps_its_input() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo("/crm-hr/directory");
        var cut = harness.Context.Render<CrmHrDirectoryPage>();
        var view = (ICrmHrDirectoryWorkspaceView)cut.Instance;
        cut.WaitForElement("[data-testid='crmhr-directory-import-export-button']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-directory-import-export-button']").ClickAsync(new()));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-import-textarea']").Input("Unfinished local CSV input"));

        var closing = cut.InvokeAsync(view.CloseImportExportDialogAsync);
        cut.WaitForElement("[data-testid='crmhr-import-dirty-continue']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-import-dirty-continue']").ClickAsync(new()));
        await closing;

        Assert.True(view.IsImportExportDialogOpen);
        Assert.Equal("Unfinished local CSV input", cut.Find("[data-testid='crmhr-import-textarea']").GetAttribute("value"));
        var discarding = cut.InvokeAsync(view.CloseImportExportDialogAsync);
        cut.WaitForElement("[data-testid='crmhr-import-dirty-discard']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-import-dirty-discard']").ClickAsync(new()));
        await discarding;
        Assert.False(view.IsImportExportDialogOpen);
    }

    private static async Task<(Guid Retained, Guid Duplicate)> CreateDuplicatePairAsync(PartyDirectoryService parties) {
        var name = $"UI merge pair {Guid.NewGuid():N}";
        var retained = await parties.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Person, DisplayName = name });
        var duplicate = await parties.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Person, DisplayName = name });
        Assert.True(retained.IsSuccess);
        Assert.True(duplicate.IsSuccess);
        return (retained.Value, duplicate.Value);
    }
}
