using Bunit;
using CanDoItAll.Application.UI.Profiles;
using CanDoItAll.Application.UI.Shell;
using CanDoItAll.Application.UiSandbox;
using CanDoItAll.Application.UiSandbox.Components;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ApplicationUI;

public sealed class ProfileSurfaceTests : BunitContext {
    public ProfileSurfaceTests() {
        Services.AddCanDoItAllBaseLib();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData(ProfileScenario.Startup, "Running now", true)]
    [InlineData(ProfileScenario.Active, "Running now", true)]
    [InlineData(ProfileScenario.Selected, "Activate for restart", false)]
    [InlineData(ProfileScenario.Pending, "Pending restart", true)]
    [InlineData(ProfileScenario.Locked, "Running now", true)]
    [InlineData(ProfileScenario.Busy, "Activate for restart", true)]
    public void Native_state_meanings_are_distinct_in_the_real_dialog(ProfileScenario scenario, string label, bool disabled) {
        var cut = Render<DatabaseProfileDialogSurface>(parameters => parameters.Add(component => component.View, ProfileScenarios.Create(scenario)));
        var activate = cut.Find("[data-testid='database-startup-switch']");
        Assert.Contains(label, activate.TextContent);
        Assert.Equal(disabled, activate.HasAttribute("disabled"));
        Assert.Equal(scenario == ProfileScenario.Startup, cut.FindAll("[data-testid='database-startup-continue']").Count == 1);
        Assert.Equal(scenario == ProfileScenario.Locked, cut.FindAll("[data-testid='database-dialog-locked-message']").Count == 1);
        Assert.Equal(4, cut.FindComponents<CopyButton>().Count);
        Assert.All(cut.FindComponents<CopyButton>(), copy => Assert.DoesNotContain("Password", copy.Instance.Value, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Captured_activation_and_close_callbacks_keep_the_rendered_origin_after_replacement() {
        var first = ProfileScenarios.Create(ProfileScenario.Selected);
        DatabaseProfileIntent? activation = null;
        Guid? closed = null;
        var cut = Render<DatabaseProfileDialogSurface>(parameters => parameters
            .Add(component => component.View, first)
            .Add(component => component.SwitchDatabaseProfile, intent => activation = intent)
            .Add(component => component.CloseDatabaseDialog, origin => closed = origin));
        var activate = cut.FindComponents<Button>().Single(button => button.Instance.Text == "Activate for restart").Instance.Click;
        var close = cut.FindComponent<Dialog>().Instance.OnClose;
        cut.Render(parameters => parameters.Add(component => component.View, ProfileScenarios.Create(ProfileScenario.Active)));
        await cut.InvokeAsync(() => activate.InvokeAsync(new MouseEventArgs()));
        await cut.InvokeAsync(() => close.InvokeAsync());
        Assert.Equal(new DatabaseProfileIntent(first.OpeningId, ProfileScenarios.SecondaryId), activation);
        Assert.Equal(first.OpeningId, closed);
    }

    [Fact]
    public async Task Real_profile_row_and_safe_copy_controls_emit_only_the_rendered_values() {
        var view = ProfileScenarios.Create(ProfileScenario.Active);
        DatabaseProfileIntent? selection = null;
        var cut = Render<DatabaseProfileDialogSurface>(parameters => parameters
            .Add(component => component.View, view)
            .Add(component => component.SelectDatabaseProfile, intent => selection = intent));
        var row = cut.FindComponents<SelectionListItem>().Single(item => item.Instance.Title == "Review workspace");
        await row.InvokeAsync(() => row.Instance.OnSelect.InvokeAsync());
        Assert.Equal(new DatabaseProfileIntent(view.OpeningId, ProfileScenarios.SecondaryId), selection);
        Assert.Equal("scenario-host / primary", cut.FindComponents<CopyButton>()
            .Single(copy => copy.Instance.AriaLabel == "Copy active database target").Instance.Value);
    }

    [Fact]
    public void Error_and_empty_states_keep_management_actions() {
        var cut = Render<DatabaseProfileDialogSurface>(parameters => parameters.Add(component => component.View, ProfileScenarios.Create(ProfileScenario.Error)));
        Assert.Contains("unavailable", cut.Find("[data-testid='database-dialog-message']").TextContent);
        cut.Render(parameters => parameters.Add(component => component.View, ProfileScenarios.Create(ProfileScenario.Empty)));
        Assert.Contains("Create the first PostgreSQL profile", cut.Markup);
        Assert.NotNull(cut.Find("[data-testid='database-startup-open-settings']"));
        Assert.True(cut.Find("[data-testid='database-startup-switch']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Utilities_use_actual_callbacks_and_captured_recent_profile_identity() {
        var view = ProfileScenarios.Utilities(ProfileScenarios.Create(ProfileScenario.Selected));
        var conversations = 0;
        var settings = 0;
        var readiness = 0;
        Guid? opened = null;
        DatabaseProfileIntent? activation = null;
        var cut = Render<ShellUtilitiesSurface>(parameters => parameters
            .Add(component => component.View, view)
            .Add(component => component.OpenConversations, () => conversations++)
            .Add(component => component.OpenSettings, () => settings++)
            .Add(component => component.OpenRuntimeCapabilities, () => readiness++)
            .Add(component => component.OpenDatabaseDialog, origin => opened = origin)
            .Add(component => component.ActivateProfile, intent => activation = intent));
        await cut.Find("[data-testid='shell-agent-chats-action']").ClickAsync(new());
        await cut.Find("[data-testid='shell-settings-action']").ClickAsync(new());
        await cut.Find("[data-testid='shell-runtime-capabilities-action']").ClickAsync(new());
        await cut.Find("[data-testid='database-shell-open-dialog']").ClickAsync(new());
        await cut.Find($"[data-testid='database-shell-recent-profile-{ProfileScenarios.SecondaryId:N}']").ClickAsync(new());
        Assert.Equal((1, 1, 1), (conversations, settings, readiness));
        Assert.Equal(view.OpeningId, opened);
        Assert.Equal(new DatabaseProfileIntent(view.OpeningId, ProfileScenarios.SecondaryId), activation);
        Assert.Equal(view.SafeSummary, cut.FindComponents<CopyButton>().Single(copy => copy.Instance.Text == "Copy safe summary").Instance.Value);
    }

    [Fact]
    public async Task Complete_independent_shell_scenario_uses_actual_dialog_and_status_children() {
        var cut = Render<ProfilePage>();
        Assert.Single(cut.FindComponents<ShellUtilitiesSurface>());
        Assert.Single(cut.FindComponents<ShellStatusBar>());
        await cut.Find("[data-testid='profile-scenario-open']").ClickAsync(new());
        await cut.Find("[data-testid='database-startup-switch']").ClickAsync(new());
        Assert.Contains("unchanged", cut.Find("[data-testid='database-dialog-message']").TextContent);
        Assert.Equal("Activation requests: 1", cut.Find("[data-testid='profile-scenario-activations']").TextContent);
        Assert.Contains("Pending restart: Review workspace", cut.Markup);
    }
}
