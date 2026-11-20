using Bunit;
using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages;
using CanDoItAll.Modules.Workspace.Pages.Components;
using CanDoItAll.RemoteInstances;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Shell;

[Trait("Category", "HostPlatform")]
public sealed class RemoteInstancePickerTests {
    [Fact]
    public async Task Feature_create_selects_the_saved_identity_and_both_management_surfaces_show_the_same_connection() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var secret = await SaveSecretAsync(harness);
        var selections = new List<RemoteInstanceSelection?>();
        var picker = harness.Context.Render<RemoteInstancePicker>(p => p.Add(c => c.SelectedChanged,
            (RemoteInstanceSelection? value) => selections.Add(value)));
        await picker.WaitForElement("[data-testid=remote-instance-add]:enabled").ClickAsync();
        picker.WaitForElement("[data-testid=remote-instance-name]").Input("West division");
        picker.Find("[data-testid=remote-instance-url]").Input("https://west.example.com/candoitall/");
        picker.WaitForElement($"[data-testid=remote-instance-credential] option[value='{secret:D}']");
        picker.Find("[data-testid=remote-instance-credential]").Change(secret.ToString());
        Assert.DoesNotContain("component-test-token-value", picker.Markup, StringComparison.Ordinal);
        await picker.WaitForElement("[data-testid=remote-instance-save]:enabled").ClickAsync();

        var registry = harness.Context.Services.GetRequiredService<IRemoteInstanceRegistry>();
        var saved = Assert.Single(await registry.ListAsync());
        Assert.Equal(new RemoteInstanceSelection(saved.Id, saved.ConcurrencyToken), Assert.Single(selections));
        Assert.Empty(picker.FindAll("[data-testid=remote-instance-editor]"));
        var registryHref = SettingsUrlCodec.Instance.Href(SettingsUrlCodec.Path, new(WorkspaceSection.RemoteInstances));
        var providerDialog = harness.Context.Render<SharedProviderSourcesDialog>(p => p.Add(c => c.RegistryHref, registryHref));
        providerDialog.WaitForAssertion(() => Assert.Equal(saved.Id.ToString(),
            providerDialog.Find("[data-testid=shared-provider-source-card]").GetAttribute("data-source-id")));
        var settingsHref = providerDialog.Find($"a[href='{registryHref}']").GetAttribute("href");
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo(Assert.IsType<string>(settingsHref));
        var settings = harness.Context.Render<SettingsPage>();
        settings.WaitForAssertion(() => Assert.Contains("West division", settings.Markup, StringComparison.Ordinal));
        await settings.WaitForElement("[data-testid=remote-instance-edit]:enabled").ClickAsync();
        settings.WaitForElement("[data-testid=remote-instance-enabled]").Change(false);
        await settings.WaitForElement("[data-testid=remote-instance-save]:enabled").ClickAsync();
        picker.WaitForAssertion(() => Assert.Null(selections.Last()));
        Assert.False((await registry.GetAsync(saved.Id)).IsEnabled);
        Assert.True(picker.Find($"option[value='{saved.Id:D}']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Replacing_a_peer_connection_preserves_an_open_draft_and_prevents_a_stale_save() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var secret = await SaveSecretAsync(harness);
        var registry = harness.Context.Services.GetRequiredService<IRemoteInstanceRegistry>();
        var original = new RemoteInstanceSettings("East division", new Uri("https://east.example.com/"), secret, true, false);
        var instance = await registry.CreateAsync(Guid.NewGuid(), original);
        var manager = harness.Context.Render<RemoteInstancePicker>(p => p.Add(c => c.ManageInstances, true));
        await manager.WaitForElement("[data-testid=remote-instance-edit]:enabled").ClickAsync();
        manager.WaitForElement("[data-testid=remote-instance-save]:enabled");
        manager.Find("[data-testid=remote-instance-name]").Input("Unsubmitted edit");
        var peer = await registry.UpdateAsync(instance.Id, instance.ConcurrencyToken, original with { Name = "Accepted by peer" });
        manager.WaitForAssertion(() => Assert.True(manager.Find("[data-testid=remote-instance-save]").HasAttribute("disabled")));
        Assert.Equal("Unsubmitted edit", manager.Find("[data-testid=remote-instance-name]").GetAttribute("value"));
        Assert.Equal(peer.ConcurrencyToken, (await registry.GetAsync(instance.Id)).ConcurrencyToken);
    }

    private static async Task<Guid> SaveSecretAsync(ComponentTestHarness harness) {
        var result = await harness.Context.Services.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
            Name = "Remote access",
            Kind = SecretKind.ApiKey,
            Scope = "workspace",
            SecretValue = "component-test-token-value"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
