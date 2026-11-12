using Bunit;
using CanDoItAll.Collaboration.UI;
using CanDoItAll.Collaboration.UiSandbox;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Collaboration;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Collaboration;

public sealed class CollaborationSurfaceTests : BunitContext {
    public CollaborationSurfaceTests() {
        Services.AddCanDoItAllBaseLib();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData(CollaborationScenario.Loading, "loading")]
    [InlineData(CollaborationScenario.Empty, "ready")]
    [InlineData(CollaborationScenario.FailedLoad, "failed")]
    [InlineData(CollaborationScenario.StaleRefresh, "stale")]
    [InlineData(CollaborationScenario.MissingThread, "ready")]
    [InlineData(CollaborationScenario.LongTranscript, "ready")]
    public void Real_children_render_reachable_scenario_states(CollaborationScenario scenario, string phase) {
        using var view = new CollaborationScenarioWorkspace(scenario, () => { });
        var cut = Render<CollaborationWorkspaceSurface>(parameters => parameters.Add(item => item.View, view));
        Assert.Equal(phase, cut.Find("[data-testid=collaboration-workspace]").GetAttribute("data-phase"));
        Assert.Single(cut.FindComponents<ListDetailShell>());
        Assert.Single(cut.FindComponents<Tabs>());
        if (scenario == CollaborationScenario.MissingThread) {
            Assert.Contains("Thread not found", cut.Markup);
        }
        if (scenario == CollaborationScenario.LongTranscript) {
            Assert.Equal(60, cut.FindAll("[data-testid=collaboration-thread-message-item]").Count);
            Assert.Empty(cut.FindAll("script"));
            Assert.Contains("&lt;script&gt;", cut.Markup);
        }
        if (scenario is CollaborationScenario.Loading or CollaborationScenario.FailedLoad) {
            Assert.DoesNotContain("Inbox is empty", cut.Markup);
        }
    }

    [Fact]
    public async Task Both_forms_validate_required_and_bounded_fields_through_real_edit_forms() {
        using var view = new CollaborationScenarioWorkspace(CollaborationScenario.InvalidDraft, () => { });
        var cut = Render<CollaborationWorkspaceSurface>(parameters => parameters.Add(item => item.View, view));
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-create-form]").SubmitAsync());
        Assert.Contains("The Subject field is required.", cut.Markup);
        Assert.Contains("The MessageBody field is required.", cut.Markup);
        await cut.InvokeAsync(() => {
            cut.Find("[data-testid=collaboration-thread-subject]").Change(new string('a', 241));
            cut.Find("[data-testid=collaboration-thread-context-label]").Change(new string('b', 201));
            cut.Find("[data-testid=collaboration-thread-context-route]").Change(new string('c', 501));
        });
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-create-form]").SubmitAsync());
        Assert.True(cut.FindAll(".validation-message").Count >= 4);
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-reply-form]").SubmitAsync());
        Assert.Contains("The MessageBody field is required.", cut.Find("[data-testid=collaboration-reply-form]").TextContent);
        Assert.Equal(3, view.Workspace!.Threads.Count);
    }

    [Fact]
    public async Task Create_validation_and_edit_context_survive_section_unmount_and_return() {
        var cut = Render<CanDoItAll.Collaboration.UiSandbox.Components.Home>();
        var surface = cut.FindComponent<CollaborationWorkspaceSurface>();
        var view = surface.Instance.View;
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-create-form]").SubmitAsync());
        var draft = view.NewThread;
        var messages = draft.Context.GetValidationMessages().ToArray();
        Assert.NotEmpty(messages);
        await cut.InvokeAsync(() => view.SetSectionAsync(CollaborationSection.Threads));
        await cut.InvokeAsync(() => view.SetSectionAsync(CollaborationSection.Inbox));
        Assert.Same(draft, view.NewThread);
        Assert.Equal(messages, draft.Context.GetValidationMessages());
        Assert.Contains("The Subject field is required.", cut.Markup);
    }

    [Fact]
    public async Task Admitted_save_disables_only_its_form_and_enter_plus_click_cannot_duplicate_it() {
        var cut = Render<CanDoItAll.Collaboration.UiSandbox.Components.Home>();
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-scenario]").Change(nameof(CollaborationScenario.AdmittedSave)));
        var view = cut.FindComponent<CollaborationWorkspaceSurface>().Instance.View;
        var before = view.Workspace!.Threads.Count;
        var submit = cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-create-form]").SubmitAsync());
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=collaboration-create-form] fieldset").HasAttribute("disabled")));
        Assert.False(cut.Find("[data-testid=collaboration-reply-form] fieldset").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-create-form]").SubmitAsync());
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-complete-pending]").ClickAsync(new MouseEventArgs()));
        await submit;
        Assert.Equal(before + 1, view.Workspace!.Threads.Count);
    }

    [Fact]
    public async Task Committed_warning_refresh_reconciles_once_and_owner_refusal_preserves_text() {
        var cut = Render<CanDoItAll.Collaboration.UiSandbox.Components.Home>();
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-scenario]").Change(nameof(CollaborationScenario.SavedWithRefreshWarning)));
        var view = cut.FindComponent<CollaborationWorkspaceSurface>().Instance.View;
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-create-form]").SubmitAsync());
        Assert.Contains("Saved successfully", cut.Markup);
        Assert.NotNull(view.NewThread.SavedThreadId);
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-refresh]").ClickAsync(new MouseEventArgs()));
        Assert.Equal(4, view.Workspace!.Threads.Count);
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-scenario]").Change(nameof(CollaborationScenario.OwnerRefusal)));
        view = cut.FindComponent<CollaborationWorkspaceSurface>().Instance.View;
        var draft = view.NewThread;
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-create-form]").SubmitAsync());
        Assert.Same(draft, view.NewThread);
        Assert.Equal("Draft collaboration item", draft.Model.Subject);
        Assert.Contains("owner refused", cut.Markup);
    }
}
