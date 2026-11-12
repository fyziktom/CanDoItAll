using System.Reflection;
using Bunit;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class SharedDeliveryAcknowledgementTests {
    [Fact]
    public async Task Own_committed_revision_refresh_does_not_cancel_import_delivery_acknowledgement() {
        var id = Guid.NewGuid();
        var import = SharedProviderSourceAndImportComponentTests.CreateImport(id);
        var service = new RecordingSharedProviderManagementService(new(id, SharedProviderProfileOwnership.Imported,
            null, null, import) { Change = new(SharedProviderChangeKind.ImportedSettings, [id]) });
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<ISharedProviderManagementService>(service));
        var recovery = harness.Context.Services.GetRequiredService<SharedProviderRecovery>();
        IRenderedComponent<SharedProviderManagementPanel>? cut = null;
        cut = harness.Context.Render<SharedProviderManagementPanel>(p => p
            .Add(x => x.ProviderProfileId, id)
            .Add(x => x.ProviderRevision, import.ProviderConcurrencyToken)
            .Add(x => x.ProvidersChanged, (SharedProviderChangeDelivery delivery) => delivery.ReconcileAsync(() => {
                cut!.Render(p => p.Add(x => x.ProviderRevision, service.State.Import!.ProviderConcurrencyToken));
                return Task.CompletedTask;
            })));
        cut.WaitForElement("[data-testid='shared-provider-import-alias']").Input("Committed alias");
        await cut.Find("[data-testid='shared-provider-import-save']").ClickAsync();
        cut.WaitForAssertion(() => {
            Assert.Null(recovery.FindTarget(id));
            Assert.Empty(cut.FindAll("[data-testid='shared-provider-warning']"));
            Assert.False(cut.Find("[data-testid='shared-provider-import-save']").HasAttribute("disabled"));
            Assert.Equal("Committed alias", cut.Instance.Presentation.Draft!.LocalAlias);
        });
        Assert.Equal("Committed alias", service.ImportedUpdate!.LocalAlias);
    }

    [Fact]
    public async Task No_op_receiver_keeps_known_target_delivery_pending() {
        var id = Guid.NewGuid();
        var service = new RecordingSharedProviderManagementService(
            SharedProviderPublicationPanelTests.CreateLocalState(id, false, true) with {
                Change = new(SharedProviderChangeKind.Publication, [id])
            });
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<ISharedProviderManagementService>(service));
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p
            .Add(x => x.ProviderProfileId, id)
            .Add(x => x.ProvidersChanged, (SharedProviderChangeDelivery _) => Task.CompletedTask));
        await cut.WaitForElement("[data-testid='shared-provider-publish']").ClickAsync();
        Assert.Contains("delivery is pending", cut.Markup, StringComparison.Ordinal);
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.Contains("delivery is pending", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(1, service.PublicationWrites);
        cut.Render(p => p.Add(x => x.ProvidersChanged,
            (SharedProviderChangeDelivery delivery) => delivery.ReconcileAsync(() => Task.CompletedTask)));
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-warning']"));
        Assert.Equal(1, service.PublicationWrites);
    }

    [Fact]
    public async Task No_op_receiver_keeps_known_source_delivery_pending() {
        var service = DispatchProxy.Create<ISharedProviderManagementService, SharedProviderOwnedEffectsTests.SourceProxy>();
        var proxy = (SharedProviderOwnedEffectsTests.SourceProxy)(object)service;
        proxy.Delay = false;
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = harness.Context.Render<SharedProviderSourcesDialog>(p => p
            .Add(x => x.ProvidersChanged, (SharedProviderChangeDelivery _) => Task.CompletedTask));
        await cut.WaitForElement("[data-testid='shared-provider-source-sync']").ClickAsync();
        Assert.Contains("delivery is pending", cut.Markup, StringComparison.Ordinal);
        await cut.Find("[data-testid='shared-provider-source-verify']").ClickAsync();
        Assert.Contains("delivery is pending", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(1, proxy.Operations);
        cut.Render(p => p.Add(x => x.ProvidersChanged,
            (SharedProviderChangeDelivery delivery) => delivery.ReconcileAsync(() => Task.CompletedTask)));
        await cut.Find("[data-testid='shared-provider-source-verify']").ClickAsync();
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-source-unresolved']"));
        Assert.Equal(1, proxy.Operations);
    }
}
