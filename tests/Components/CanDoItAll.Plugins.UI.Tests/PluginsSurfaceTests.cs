using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Plugins.Presentation;
using CanDoItAll.Plugins.UI;
using CanDoItAll.Plugins.UiSandbox;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Plugins;

public sealed class PluginsSurfaceTests : BunitContext {
    public PluginsSurfaceTests() {
        Services.AddCanDoItAllBaseLib();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData(ConfigurationFieldType.Text, "  unfinished text ")]
    [InlineData(ConfigurationFieldType.Url, "https://")]
    [InlineData(ConfigurationFieldType.Number, "-")]
    [InlineData(ConfigurationFieldType.Boolean, "true")]
    [InlineData(ConfigurationFieldType.Json, "{ ")]
    [InlineData(ConfigurationFieldType.SecretReference, "incomplete")]
    [InlineData(ConfigurationFieldType.Select, "one")]
    [InlineData(ConfigurationFieldType.MultilineText, "line one\nline two \n")]
    [InlineData(ConfigurationFieldType.Guid, "incomplete")]
    public async Task Every_schema_control_retains_raw_input_before_blur_through_unmount(ConfigurationFieldType kind, string raw) {
        var cut = Render<CanDoItAll.Plugins.UiSandbox.Components.Home>();
        var workspace = Workspace(cut);
        await ClickAsync(cut, "plugins-list-item-sandbox-plugin-1");
        await ClickAsync(cut, "plugins-tab-settings");
        var input = cut.Find($"[data-testid='plugin-setting-sandbox-plugin-1-example-{kind}']");
        if (kind == ConfigurationFieldType.Boolean) {
            await input.ChangeAsync(new ChangeEventArgs { Value = true });
        } else if (kind == ConfigurationFieldType.Select) {
            await input.ChangeAsync(new ChangeEventArgs { Value = raw });
        } else {
            await input.InputAsync(new ChangeEventArgs { Value = raw });
        }
        var editor = workspace.View.Editor(workspace.View.SelectedPluginId!.Value, workspace.View.Settings.Value!.ConnectionDescriptors[0]);
        var expected = kind == ConfigurationFieldType.Boolean ? bool.TrueString : raw;
        Assert.Equal(expected, editor.State.GetText(kind.ToString()));
        await ClickAsync(cut, "plugins-tab-main");
        await cut.InvokeAsync(workspace.RefreshAsync);
        await ClickAsync(cut, "plugins-tab-settings");
        Assert.Equal(expected, editor.State.GetText(kind.ToString()));
        Assert.Same(editor, workspace.View.Editors[editor.Origin.Key]);
        Assert.True(editor.IsDirty);
        if (kind is ConfigurationFieldType.Number or ConfigurationFieldType.Json or ConfigurationFieldType.Url or ConfigurationFieldType.Guid or ConfigurationFieldType.SecretReference) {
            Assert.False(editor.Validation.Succeeded);
            Assert.NotEmpty(cut.FindAll(".workflow-canvas-error"));
        }
    }

    [Theory]
    [InlineData("main", "Main info")]
    [InlineData("executors", "Loaded from plugin descriptor")]
    [InlineData("settings", "Connection name")]
    [InlineData("connections", "Login")]
    [InlineData("logs", "Installation logs")]
    [InlineData("grants", "OAuth2")]
    public async Task All_six_real_sections_render(string section, string content) {
        var cut = Render<CanDoItAll.Plugins.UiSandbox.Components.Home>();
        await ClickAsync(cut, $"plugins-tab-{section}");
        Assert.Contains(content, cut.Markup, StringComparison.Ordinal);
        Assert.Single(cut.FindComponents<PluginsWorkspaceSurface>());
    }

    [Fact]
    public async Task Actual_grant_buttons_show_busy_for_conflicting_actions_and_await_the_admitted_write() {
        var cut = Render<CanDoItAll.Plugins.UiSandbox.Components.Home>();
        await ScenarioAsync(cut, PluginScenario.HeldGrant);
        await ClickAsync(cut, "plugins-tab-grants");
        var row = cut.FindAll("[data-testid=plugin-grant-row]").Single(row => row.GetAttribute("data-scope") == "connection-a");
        var grant = cut.InvokeAsync(() => row.QuerySelectorAll("button").First().ClickAsync(new MouseEventArgs()));
        cut.WaitForAssertion(() => Assert.All(cut.FindAll("[data-testid=plugin-grant-row][data-scope=connection-a] button"),
            button => Assert.Equal("true", button.GetAttribute("aria-busy"))));
        await ClickAsync(cut, "plugins-release");
        await grant;
        Assert.Equal("1", cut.Find("[data-testid=plugins-store-counts]").GetAttribute("data-grants"));
        Assert.Contains("Granted", cut.Find("[data-testid=plugin-grant-row][data-scope=connection-a]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Real_form_remains_editable_while_saved_identity_waits_for_readback() {
        var cut = Render<CanDoItAll.Plugins.UiSandbox.Components.Home>();
        await ScenarioAsync(cut, PluginScenario.HeldReadback);
        await ClickAsync(cut, "plugins-tab-settings");
        await cut.Find("[data-testid=plugin-setting-office365-mail-office365-clientId]")
            .InputAsync(new ChangeEventArgs { Value = "9415d780-72b3-4e80-80bd-06e640e7ba40" });
        var save = cut.InvokeAsync(() => ClickAsync(cut, "plugin-connection-save-office365-mail-office365"));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.Find("[data-testid=plugin-draft-office365-mail-office365]").GetAttribute("data-connection-id")!));
        var input = cut.Find("[data-testid=plugin-connection-name-office365-mail-office365]");
        Assert.False(input.HasAttribute("disabled"));
        await input.InputAsync(new ChangeEventArgs { Value = "While readback waits " });
        await ClickAsync(cut, "plugins-tab-main");
        await ClickAsync(cut, "plugins-release");
        await save;
        await ClickAsync(cut, "plugins-tab-settings");
        Assert.Equal("While readback waits ", cut.Find("[data-testid=plugin-connection-name-office365-mail-office365]").GetAttribute("value"));
        Assert.Equal("true", cut.Find("[data-testid=plugin-draft-office365-mail-office365]").GetAttribute("data-dirty"));
        Assert.Equal("Saved", cut.Find("[data-testid=plugin-draft-office365-mail-office365]").GetAttribute("data-status"));
        Assert.Equal("1", cut.Find("[data-testid=plugins-store-counts]").GetAttribute("data-saves"));
    }

    [Fact]
    public async Task Actual_InputFile_protects_reading_lifetime_and_fake_write_is_observable_on_reopen() {
        var cut = Render<CanDoItAll.Plugins.UiSandbox.Components.Home>();
        await ScenarioAsync(cut, PluginScenario.HeldUpload);
        var workspace = Workspace(cut);
        await ClickAsync(cut, "plugin-packages-open");
        var file = new BrowserFile([80, 75, 1, 2]);
        var upload = cut.InvokeAsync(() => workspace.UploadAsync(file));
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=plugin-package-upload]").HasAttribute("disabled")));
        var input = cut.FindComponent<InputFile>().Instance;
        await cut.InvokeAsync(workspace.ClosePackages);
        Assert.True(workspace.View.PackagesOpen);
        Assert.Same(input, cut.FindComponent<InputFile>().Instance);
        var rejected = new BrowserFile([1]);
        await cut.InvokeAsync(() => workspace.UploadAsync(rejected));
        Assert.Equal(0, rejected.OpenCount);
        await ClickAsync(cut, "plugins-release");
        await upload;
        Assert.True(file.Disposed);
        await cut.InvokeAsync(workspace.ClosePackages);
        await ClickAsync(cut, "plugin-packages-open");
        Assert.True(Assert.Single(workspace.View.Packages.Value).IsInstalled);
        await ClickAsync(cut, "plugin-runtime-restart");
        Assert.True(workspace.View.Restart.Value!.IsRestartRequested);
        await cut.InvokeAsync(workspace.RestartAsync);
        Assert.Equal("1", cut.Find("[data-testid=plugins-store-counts]").GetAttribute("data-restarts"));
    }

    [Fact]
    public async Task Upload_disposal_cancels_reading_and_disposes_the_open_stream() {
        var (store, workspace, _) = await PluginsWorkspaceTests.ReadyAsync(PluginScenario.HeldUpload);
        await workspace.OpenPackagesAsync();
        var file = new BrowserFile([1, 2, 3]);
        var upload = workspace.UploadAsync(file);
        await store.EnteredAsync(PluginScenarioWait.Upload);
        workspace.Dispose();
        store.ReleaseAll();
        await upload;
        Assert.True(file.Disposed);
        Assert.Equal(0, store.PackageWrites);
    }

    private static PluginsWorkspace Workspace(IRenderedComponent<CanDoItAll.Plugins.UiSandbox.Components.Home> cut)
        => Assert.IsType<PluginsWorkspace>(cut.FindComponent<PluginsWorkspaceSurface>().Instance.Workspace);
    private static Task ScenarioAsync(IRenderedComponent<CanDoItAll.Plugins.UiSandbox.Components.Home> cut, PluginScenario scenario)
        => cut.Find("[data-testid=plugins-scenario]").ChangeAsync(new ChangeEventArgs { Value = scenario.ToString() });
    private static Task ClickAsync(IRenderedComponent<CanDoItAll.Plugins.UiSandbox.Components.Home> cut, string id)
        => cut.Find($"[data-testid='{id}']").ClickAsync(new MouseEventArgs());

    private sealed class BrowserFile(byte[] bytes) : IBrowserFile {
        public string Name => "fixture.zip";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => bytes.Length;
        public string ContentType => "application/zip";
        public int OpenCount { get; private set; }
        public bool Disposed { get; private set; }
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) {
            OpenCount++;
            return new ObservedStream(bytes, () => Disposed = true);
        }
    }
    private sealed class ObservedStream(byte[] bytes, Action disposed) : MemoryStream(bytes) {
        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            disposed();
        }
    }
}
