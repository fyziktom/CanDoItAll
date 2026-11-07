using CanDoItAll.Components.BaseLib;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.Modules.CrmHr.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmHrOpportunityConcurrentEditingTests {
    [Fact]
    public async Task Native_unblurred_draft_keeps_its_model_and_context_through_a_peer_change_and_explicit_merge() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (original, service) = await SeedAsync(harness);
        var cut = Open(harness, original);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        WaitReady(cut);
        await cut.InvokeAsync(view.OpenOpportunityEditDialog);
        cut.WaitForElement("#crmhr-opportunity-edit-form");
        var model = view.OpportunityEditor;
        var form = ComponentTestHarness.FindEditContext(cut, model);
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-notes']").InputAsync(new ChangeEventArgs { Value = "  Unblurred local notes  " }));
        var peer = OpportunityEditorDrafts.FromDetail(original);
        peer.Summary = "Peer summary";
        Assert.True((await service.SaveOpportunityAsync(peer)).IsSuccess);
        cut.WaitForElement("[data-testid='crmhr-opportunity-freshness']");
        Assert.Same(model, view.OpportunityEditor);
        Assert.Same(form, ComponentTestHarness.FindEditContext(cut, model));
        Assert.Equal("  Unblurred local notes  ", model.Notes);
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-review-open']").ClickAsync(new()));
        cut.WaitForElement("[data-testid='crmhr-opportunity-review-dialog']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-review-apply']").ClickAsync(new()));

        Assert.Equal("Peer summary", model.Summary);
        Assert.Equal("  Unblurred local notes  ", model.Notes);
        Assert.Equal("", (await service.GetOpportunityAsync(original.Id))!.Notes);
        Assert.Same(form, ComponentTestHarness.FindEditContext(cut, model));
        await cut.InvokeAsync(() => view.SaveOpportunityAsync(model));
        Assert.Equal("Unblurred local notes", (await service.GetOpportunityAsync(original.Id))!.Notes);
        Assert.Equal("Peer summary", (await service.GetOpportunityAsync(original.Id))!.Summary);
    }

    [Fact]
    public async Task A_clean_peer_refresh_keeps_controls_and_disposal_releases_all_subscriptions() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (original, service) = await SeedAsync(harness);
        var cut = Open(harness, original);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        WaitReady(cut);
        await cut.InvokeAsync(view.OpenOpportunityEditDialog);
        var control = cut.FindComponents<NativeValueInput<string>>().Single(component => component.Instance.Value == original.Title).Instance;
        var peer = OpportunityEditorDrafts.FromDetail(original);
        peer.Title = "Clean peer opportunity title";
        Assert.True((await service.SaveOpportunityAsync(peer)).IsSuccess);
        cut.WaitForAssertion(() => Assert.Equal(peer.Title, view.OpportunityEditor.Title));
        Assert.Contains(cut.FindComponents<NativeValueInput<string>>(), component => ReferenceEquals(component.Instance, control));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-opportunity-freshness']"));
        var feed = harness.Context.Services.GetRequiredService<EditingChangeFeed>();
        Assert.Equal(5, feed.ActiveSubscriptions);
        await harness.Context.DisposeRenderedComponentsAsync();
        Assert.Equal(0, feed.ActiveSubscriptions);
        Assert.Equal(0, feed.ActiveTopics);
    }

    [Fact]
    public async Task Closing_a_dirty_native_opportunity_requires_a_current_discard_decision_for_each_opening() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (original, _) = await SeedAsync(harness);
        var cut = Open(harness, original);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        WaitReady(cut);
        for (var opening = 0; opening < 2; opening++) {
            await cut.InvokeAsync(view.OpenOpportunityEditDialog);
            await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-title']").InputAsync(new ChangeEventArgs { Value = "Unsaved pursuit" }));
            var close = cut.InvokeAsync(view.CloseOpportunityEditDialogAsync);
            cut.WaitForElement("[data-testid='crmhr-opportunity-dirty-close']");
            await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-dirty-continue']").ClickAsync(new()));
            await close;
            Assert.True(view.IsOpportunityEditDialogOpen);
            Assert.Equal("Unsaved pursuit", view.OpportunityEditor.Title);
            close = cut.InvokeAsync(view.CloseOpportunityEditDialogAsync);
            cut.WaitForElement("[data-testid='crmhr-opportunity-dirty-close']");
            await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-dirty-discard']").ClickAsync(new()));
            await close;
            Assert.False(view.IsOpportunityEditDialogOpen);
        }
    }

    [Fact]
    public async Task Wizard_steps_keep_the_form_and_all_section_controls_mounted() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var model = new CrmOpportunityEditorModel { AccountPartyId = Guid.NewGuid(), OwnerPartyId = Guid.NewGuid(), Title = "Wizard draft" };
        var cut = harness.Context.Render<OpportunityCreateDialog>(parameters => parameters
            .Add(component => component.IsOpen, true).Add(component => component.InitialModel, model));
        var context = cut.FindComponent<EditForm>().Instance.EditContext;
        var title = cut.FindComponents<NativeValueInput<string>>().Single(component => component.Instance.Value == model.Title).Instance;
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-create-next']").ClickAsync(new()));
        Assert.True(cut.Find("[data-opportunity-section='0']").HasAttribute("hidden"));
        Assert.Contains(cut.FindComponents<NativeValueInput<string>>(), component => ReferenceEquals(title, component.Instance));
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-create-back']").ClickAsync(new()));
        Assert.False(cut.Find("[data-opportunity-section='0']").HasAttribute("hidden"));
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Discard_after_a_known_commit_reopens_current_detail_without_losing_the_created_identity(bool create) {
        var gate = new CommitResponseGate();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.RemoveAll<ICrmOpportunityEditorService>();
            services.AddScoped<ICrmOpportunityEditorService>(provider => new PausedOwner(provider.GetRequiredService<CrmService>(), gate));
        });
        var (original, service) = await SeedAsync(harness);
        var cut = Open(harness, original);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        WaitReady(cut);
        if (create) {
            await cut.InvokeAsync(view.OpenOpportunityCreateDialog);
            await cut.InvokeAsync(() => view.OpportunityEditor.OwnerPartyId = original.OwnerPartyId);
            await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-title']").InputAsync(new ChangeEventArgs { Value = "Known committed created opportunity" }));
            await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-create-next']").ClickAsync(new()));
            await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-create-next']").ClickAsync(new()));
        } else {
            await cut.InvokeAsync(view.OpenOpportunityEditDialog);
        }
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-notes']").InputAsync(new ChangeEventArgs { Value = "Committed before later typing" }));
        var saving = cut.InvokeAsync(() => view.SaveOpportunityAsync(view.OpportunityEditor));
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-notes']").InputAsync(new ChangeEventArgs { Value = "Later local draft to discard" }));
        } finally {
            gate.Release.TrySetResult();
        }
        await saving;
        Assert.Equal("Later local draft to discard", view.OpportunityEditor.Notes);
        Assert.True(create ? view.IsOpportunityCreateDialogOpen : view.IsOpportunityEditDialogOpen);
        var acceptedId = Assert.IsType<CrmOpportunitySaveReceipt>(gate.Accepted).OpportunityId;
        Assert.Equal(acceptedId, view.OpportunityEditor.Id);
        Assert.Equal("Committed before later typing", (await service.GetOpportunityAsync(acceptedId))!.Notes);
        var closing = create ? cut.InvokeAsync(view.CloseOpportunityCreateDialogAsync) : cut.InvokeAsync(view.CloseOpportunityEditDialogAsync);
        cut.WaitForElement("[data-testid='crmhr-opportunity-dirty-close']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-dirty-discard']").ClickAsync(new()));
        await closing;
        cut.WaitForAssertion(() => Assert.Equal(acceptedId, view.SelectedOpportunity?.Id));
        Assert.Equal("Committed before later typing", view.SelectedOpportunity!.Notes);
        Assert.Equal(1, gate.Writes);
        Assert.True(view.IsOpportunityDetailDialogOpen);
    }

    private static IRenderedComponent<CrmHrCrmPage> Open(ComponentTestHarness harness, CrmOpportunityDetailModel opportunity) {
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr/crm?accountId={opportunity.AccountPartyId:D}&opportunityId={opportunity.Id:D}&section=opportunities");
        return harness.Context.Render<CrmHrCrmPage>();
    }

    private static void WaitReady(IRenderedComponent<CrmHrCrmPage> cut) => cut.WaitForAssertion(() =>
        Assert.Equal(AgentChatContextAccessState.Ready, cut.FindComponent<CrmAgentChatContextProvider>().Instance.ContextAccessState));

    private sealed class CommitResponseGate {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CrmOpportunitySaveReceipt? Accepted { get; set; }
        public int Writes { get; set; }
    }
    private sealed class PausedOwner(CrmService native, CommitResponseGate gate) : ICrmOpportunityEditorService {
        public Task<CrmOpportunityDetailModel?> GetOpportunityForEditingAsync(Guid id, CancellationToken cancellationToken = default, int? maximumPartyLinks = null) =>
            native.GetOpportunityForEditingAsync(id, cancellationToken, maximumPartyLinks);
        public async Task<Result<CrmOpportunitySaveReceipt>> SaveOpportunityWithReceiptAsync(CrmOpportunityEditorModel model, CancellationToken cancellationToken = default,
            Action<CrmOpportunitySaveReceipt>? onCommitted = null) {
            gate.Writes++;
            var result = await native.SaveOpportunityWithReceiptAsync(model, cancellationToken, receipt => {
                gate.Accepted = receipt with { Accepted = receipt.Accepted.Snapshot() };
                onCommitted?.Invoke(receipt);
                gate.Entered.TrySetResult();
            });
            if (result.IsSuccess) {
                await gate.Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }

    private static async Task<(CrmOpportunityDetailModel Detail, CrmService Service)> SeedAsync(ComponentTestHarness harness) {
        var directory = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var account = await directory.SavePartyAsync(new() { PartyType = PartyType.Organization, DisplayName = "Opportunity UI account" });
        var owner = await directory.SavePartyAsync(new() { PartyType = PartyType.Person, DisplayName = "Opportunity UI owner" });
        Assert.True(account.IsSuccess);
        Assert.True(owner.IsSuccess);
        var service = harness.Context.Services.GetRequiredService<CrmService>();
        var result = await service.SaveOpportunityAsync(new() { AccountPartyId = account.Value, OwnerPartyId = owner.Value, Title = "Opportunity UI pursuit" });
        Assert.True(result.IsSuccess);
        return (Assert.IsType<CrmOpportunityDetailModel>(await service.GetOpportunityAsync(result.Value)), service);
    }
}
