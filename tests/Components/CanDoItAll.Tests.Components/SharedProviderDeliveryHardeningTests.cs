using CanDoItAll.Modules.AgentFramework;
using Bunit;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class SharedProviderDeliveryHardeningTests {
    [Fact]
    public async Task Imported_settings_verification_rejects_changed_but_different_values() {
        var service = DispatchProxy.Create<ISharedProviderManagementService, SharedProviderRecoveryTests.SharingProxy>();
        var proxy = (SharedProviderRecoveryTests.SharingProxy)(object)service;
        proxy.Imported = true;
        proxy.CanonicalAliasOverride = "Canonical alias";
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(x => x.ProviderProfileId, proxy.Id));
        cut.WaitForElement("[data-testid='shared-provider-import-alias']").Change("Different requested alias");
        await cut.Find("[data-testid='shared-provider-import-save']").ClickAsync();
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.NotEmpty(cut.FindAll("[data-testid='shared-provider-warning']"));
        Assert.True(cut.Find("[data-testid='shared-provider-import-save']").HasAttribute("disabled"));
        Assert.Equal(1, proxy.Writes);
    }

    [Fact]
    public async Task Known_commit_callback_failure_retains_pending_target_delivery() {
        var id = Guid.NewGuid();
        var service = new RecordingSharedProviderManagementService(
            SharedProviderPublicationPanelTests.CreateLocalState(id, false, true) with { Change = new(SharedProviderChangeKind.Publication, [id]) });
        var callbacks = 0;
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<ISharedProviderManagementService>(service));
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p
            .Add(x => x.ProviderProfileId, id)
            .Add(x => x.ProvidersChanged, (SharedProviderChangeDelivery _) => {
                callbacks++;
                return callbacks == 1 ? Task.FromException(new IOException("Synthetic callback interruption.")) : Task.CompletedTask;
            }));
        await cut.WaitForElement("[data-testid='shared-provider-publish']").ClickAsync();
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.Equal(2, callbacks);
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-warning']"));
    }
}
