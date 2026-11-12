using Bunit;
using CanDoItAll.Application.UI.Profiles;
using CanDoItAll.Application.UI.Shell;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WebMainLayout = CanDoItAll.Web.Components.Layout.MainLayout;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class MainLayoutProfileAdmissionTests {
    [Fact]
    public async Task Duplicate_activation_and_selection_during_admission_keep_the_captured_profile_and_require_restart() {
        var gate = new ActivationGate();
        await using var harness = await CreateHarnessAsync(gate);
        var firstId = await SaveProfileAsync(harness, "First target");
        var secondId = await SaveProfileAsync(harness, "Second target");
        var runtimeId = RuntimeId(harness);
        var cut = await OpenAsync(harness, firstId);
        var dialog = cut.FindComponent<DatabaseProfileDialogSurface>().Instance;
        var intent = new DatabaseProfileIntent(dialog.View.OpeningId, firstId);
        await cut.InvokeAsync(() => dialog.SelectDatabaseProfile.InvokeAsync(intent));
        var activation = cut.InvokeAsync(() => dialog.SwitchDatabaseProfile.InvokeAsync(intent));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await cut.InvokeAsync(() => dialog.SwitchDatabaseProfile.InvokeAsync(intent));
        await cut.InvokeAsync(() => dialog.SelectDatabaseProfile.InvokeAsync(new(intent.OpeningId, secondId)));
        Assert.Single(gate.Targets);
        Assert.Equal(firstId, dialog.View.SelectedDatabaseProfileId);
        gate.Release.TrySetResult();
        await activation.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(firstId, await SavedProfileIdAsync(harness));
        Assert.Equal(runtimeId, RuntimeId(harness));
        Assert.Equal(firstId, dialog.View.DatabaseSelection!.PendingRestartProfileId);
        Assert.True(dialog.View.SelectedDialogProfileIsPendingRestart);
        Assert.False(dialog.View.CanSwitchSelectedProfile);
        Assert.False(dialog.View.DatabaseDialogBusy);
        Assert.Contains("Restart", dialog.View.Message!.Text);
    }

    [Fact]
    public async Task Retained_selection_callback_cannot_retarget_and_delayed_activation_cannot_reopen_closed_dialog() {
        var gate = new ActivationGate();
        await using var harness = await CreateHarnessAsync(gate);
        var firstId = await SaveProfileAsync(harness, "First target");
        var secondId = await SaveProfileAsync(harness, "Second target");
        var cut = await OpenAsync(harness, firstId);
        var dialog = cut.FindComponent<DatabaseProfileDialogSurface>().Instance;
        var openingId = dialog.View.OpeningId;
        await cut.InvokeAsync(() => dialog.SelectDatabaseProfile.InvokeAsync(new(openingId, firstId)));
        var oldIntent = new DatabaseProfileIntent(openingId, firstId);
        await cut.InvokeAsync(() => dialog.SelectDatabaseProfile.InvokeAsync(new(openingId, secondId)));
        await cut.InvokeAsync(() => dialog.SwitchDatabaseProfile.InvokeAsync(oldIntent));
        Assert.Empty(gate.Targets);

        var activation = cut.InvokeAsync(() => dialog.SwitchDatabaseProfile.InvokeAsync(new(openingId, secondId)));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await cut.InvokeAsync(() => dialog.CloseDatabaseDialog.InvokeAsync(openingId));
        Assert.False(dialog.View.IsOpen);
        Assert.NotEqual(openingId, dialog.View.OpeningId);
        gate.Release.TrySetResult();
        await activation.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(dialog.View.IsOpen);
        Assert.Null(dialog.View.Message);
        Assert.Equal(secondId, await SavedProfileIdAsync(harness));

        await cut.InvokeAsync(() => cut.Find("[data-testid='database-shell-action']").ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.True(dialog.View.IsOpen));
        Assert.Equal(secondId, dialog.View.DatabaseSelection!.PendingRestartProfileId);
        var successor = dialog.View.OpeningId;
        await cut.InvokeAsync(() => dialog.CloseDatabaseDialog.InvokeAsync(openingId));
        await cut.InvokeAsync(() => dialog.SelectDatabaseProfile.InvokeAsync(oldIntent));
        Assert.Equal(successor, dialog.View.OpeningId);
        Assert.True(dialog.View.IsOpen);
        Assert.Equal(secondId, dialog.View.SelectedDatabaseProfileId);
    }

    [Fact]
    public async Task Unconfirmed_postcommit_outcome_blocks_replay_until_explicit_profile_readback() {
        var gate = new ActivationGate { ThrowAfterCommit = true };
        gate.Release.TrySetResult();
        await using var harness = await CreateHarnessAsync(gate);
        var targetId = await SaveProfileAsync(harness, "Unknown outcome target");
        var cut = await OpenAsync(harness, targetId);
        var dialog = cut.FindComponent<DatabaseProfileDialogSurface>().Instance;
        var intent = new DatabaseProfileIntent(dialog.View.OpeningId, targetId);
        await cut.InvokeAsync(() => dialog.SelectDatabaseProfile.InvokeAsync(intent));
        await cut.InvokeAsync(() => dialog.SwitchDatabaseProfile.InvokeAsync(intent));
        Assert.Equal(targetId, await SavedProfileIdAsync(harness));
        Assert.Contains("could not be confirmed", dialog.View.Message!.Text);
        Assert.False(dialog.View.CanSwitchSelectedProfile);
        Assert.False(dialog.View.DatabaseDialogBusy);
        await cut.InvokeAsync(() => dialog.SwitchDatabaseProfile.InvokeAsync(intent));
        Assert.Single(gate.Targets);

        await cut.InvokeAsync(() => dialog.CloseDatabaseDialog.InvokeAsync(intent.OpeningId));
        await cut.InvokeAsync(() => cut.Find("[data-testid='database-shell-action']").ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.True(dialog.View.IsOpen));
        Assert.Equal(targetId, dialog.View.DatabaseSelection!.PendingRestartProfileId);
        Assert.True(dialog.View.SelectedDialogProfileIsPendingRestart);
        Assert.False(dialog.View.CanSwitchSelectedProfile);
        Assert.Single(gate.Targets);
    }

    [Fact]
    public async Task Retired_layout_cannot_publish_activation_into_a_successor_layout() {
        var gate = new ActivationGate();
        await using var harness = await CreateHarnessAsync(gate);
        var targetId = await SaveProfileAsync(harness, "Retired target");
        var first = await OpenAsync(harness, targetId);
        var dialog = first.FindComponent<DatabaseProfileDialogSurface>().Instance;
        var intent = new DatabaseProfileIntent(dialog.View.OpeningId, targetId);
        await first.InvokeAsync(() => dialog.SelectDatabaseProfile.InvokeAsync(intent));
        var activation = first.InvokeAsync(() => dialog.SwitchDatabaseProfile.InvokeAsync(intent));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await first.InvokeAsync(first.Instance.Dispose);
        var successor = await OpenAsync(harness, targetId);
        var successorDialog = successor.FindComponent<DatabaseProfileDialogSurface>().Instance;
        var opening = successorDialog.View.OpeningId;
        gate.Release.TrySetResult();
        await activation.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(opening, successorDialog.View.OpeningId);
        Assert.Null(successorDialog.View.Message);
        Assert.False(successorDialog.View.DatabaseDialogBusy);
        Assert.Equal(targetId, await SavedProfileIdAsync(harness));
    }

    [Fact]
    public async Task Credential_bearing_native_editor_never_crosses_the_render_or_copy_contract() {
        await using var harness = await CreateHarnessAsync(new ActivationGate());
        const string password = "shell-profile-password-only-sentinel";
        var targetId = await SaveProfileAsync(harness, "Safe target", password);
        var cut = await OpenAsync(harness, targetId);
        var editor = await harness.Context.Services.GetRequiredService<IDatabaseProfileService>().GetEditorAsync(targetId);
        Assert.Equal(password, editor.PostgresPassword);
        Assert.DoesNotContain(editor.PostgresPassword, cut.Markup, StringComparison.Ordinal);
        Assert.All(cut.FindComponents<CanDoItAll.Components.BaseLib.CopyButton>(), copy =>
            Assert.DoesNotContain(editor.PostgresPassword, copy.Instance.Value ?? string.Empty, StringComparison.Ordinal));
        Assert.DoesNotContain("Password=", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(cut.FindComponent<ShellUtilitiesSurface>().Instance.View.Details);
    }

    private static async Task<ComponentTestHarness> CreateHarnessAsync(ActivationGate gate) {
        var harness = await ComponentTestHarness.CreateAsync(services => services.Replace(
            ServiceDescriptor.Singleton<IDatabaseSwitchCoordinator>(provider => {
                gate.Inner = ActivatorUtilities.CreateInstance<DatabaseSwitchCoordinator>(provider);
                return gate;
            })), new TestHarnessOptions {
                ConfigurationOverrides = new Dictionary<string, string?> {
                    ["Database:Provider"] = null,
                    ["Database:ConnectionString"] = null
                }
            });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.browserState.isDatabaseStartupPromptDismissed").SetResult(true);
        return harness;
    }

    private static async Task<Guid> SaveProfileAsync(ComponentTestHarness harness, string name, string? password = null) {
        var profile = harness.TestEnvironment.CreatePostgreSqlProfile(Guid.NewGuid().ToString("N"));
        var editor = TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(profile, name);
        if (password is not null) {
            editor.PostgresPassword = password;
        }
        var saved = await harness.Context.Services.GetRequiredService<IDatabaseProfileService>()
            .SaveAsync(editor);
        Assert.True(saved.IsSuccess);
        return saved.Value;
    }

    private static async Task<IRenderedComponent<WebMainLayout>> OpenAsync(ComponentTestHarness harness, Guid targetId) {
        var cut = harness.Context.Render<WebMainLayout>();
        cut.WaitForAssertion(() => Assert.Contains(cut.FindComponent<DatabaseProfileDialogSurface>().Instance.View.DatabaseProfiles,
            profile => profile.Id == targetId), TimeSpan.FromSeconds(20));
        await cut.InvokeAsync(() => cut.Find("[data-testid='database-shell-action']").ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.True(cut.FindComponent<DatabaseProfileDialogSurface>().Instance.View.IsOpen));
        return cut;
    }

    private static Guid RuntimeId(ComponentTestHarness harness) => harness.Context.Services
        .GetRequiredService<IActiveDatabaseProfileResolver>().ResolveCurrentProfile().Profile.Id;

    private static async Task<Guid> SavedProfileIdAsync(ComponentTestHarness harness)
        => (await harness.Context.Services.GetRequiredService<IDatabaseProfileService>().GetCurrentSelectionAsync()).ActiveProfileId;

    private sealed class ActivationGate : IDatabaseSwitchCoordinator {
        public IDatabaseSwitchCoordinator Inner { get; set; } = default!;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<Guid> Targets { get; } = [];
        public bool ThrowAfterCommit { get; init; }

        public async Task<Result<DatabaseSwitchResult>> SwitchAsync(Guid targetProfileId, CancellationToken cancellationToken = default) {
            Targets.Add(targetProfileId);
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            var result = await Inner.SwitchAsync(targetProfileId, cancellationToken);
            Assert.True(result.IsSuccess);
            if (ThrowAfterCommit) {
                throw new InvalidOperationException("Synthetic lost activation acknowledgement.");
            }
            return result;
        }
    }
}
