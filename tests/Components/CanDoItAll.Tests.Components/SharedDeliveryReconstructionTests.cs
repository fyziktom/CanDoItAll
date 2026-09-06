using Bunit;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Editor = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using Profile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class SharedDeliveryReconstructionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_commit_callback_failure_retains_pending_source_delivery(bool recreate) {
        var service = DispatchProxy.Create<ISharedProviderManagementService, SharedProviderOwnedEffectsTests.SourceProxy>();
        var proxy = (SharedProviderOwnedEffectsTests.SourceProxy)(object)service;
        proxy.Delay = false;
        var calls = 0;
        Task Callback(SharedProviderChangeDelivery delivery) {
            calls++;
            return calls == 1 ? Task.FromException(new IOException("Synthetic interrupted receiver.")) : delivery.ReconcileAsync(() => Task.CompletedTask);
        }
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        IRenderedComponent<SharedProviderSourcesDialog> Render() => harness.Context.Render<SharedProviderSourcesDialog>(
            p => p.Add(x => x.ProvidersChanged, Callback));
        var cut = Render();
        await cut.WaitForElement("[data-testid='shared-provider-source-sync']").ClickAsync();
        Assert.Contains("delivery is pending", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(1, proxy.Operations);
        if (recreate) {
            await cut.Find("[data-testid='shared-provider-connections-close']").ClickAsync();
            cut = Render();
            cut.WaitForElement("[data-testid='shared-provider-source-verify']");
        }
        await cut.Find("[data-testid='shared-provider-source-verify']").ClickAsync();
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-source-unresolved']"));
        Assert.DoesNotContain("delivery is pending", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(2, calls);
        Assert.Equal(1, proxy.Operations);
        await cut.Find("[data-testid='shared-provider-source-refresh']").ClickAsync();
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Component_recreation_can_resume_pending_target_delivery() {
        var id = Guid.NewGuid();
        var service = new RecordingSharedProviderManagementService(
            SharedProviderPublicationPanelTests.CreateLocalState(id, false, true) with {
                Change = new(SharedProviderChangeKind.Publication, [id])
            });
        var calls = 0;
        Task Callback(SharedProviderChangeDelivery delivery) {
            calls++;
            return calls == 1 ? Task.FromException(new IOException("Synthetic interrupted receiver.")) : delivery.ReconcileAsync(() => Task.CompletedTask);
        }
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<ISharedProviderManagementService>(service));
        IRenderedComponent<SharedProviderManagementPanel> Render() => harness.Context.Render<SharedProviderManagementPanel>(
            p => p.Add(x => x.ProviderProfileId, id).Add(x => x.ProvidersChanged, Callback));
        var cut = Render();
        await cut.WaitForElement("[data-testid='shared-provider-publish']").ClickAsync();
        Assert.Contains("delivery is pending", cut.Markup, StringComparison.Ordinal);
        await cut.InvokeAsync(cut.Instance.Dispose);
        var reopened = Render();
        await reopened.WaitForElement("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.Empty(reopened.FindAll("[data-testid='shared-provider-warning']"));
        Assert.Equal(2, calls);
        Assert.Equal(1, service.PublicationWrites);
        reopened.Render(p => p.Add(x => x.Revision, 1));
        reopened.WaitForElement("[data-testid='shared-provider-unpublish']");
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Exact_before_state_allows_deliberate_action_without_replay() {
        var service = DispatchProxy.Create<ISharedProviderManagementService, SharedProviderRecoveryTests.SharingProxy>();
        var proxy = (SharedProviderRecoveryTests.SharingProxy)(object)service;
        var before = proxy.State(proxy.Id);
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(x => x.ProviderProfileId, proxy.Id));
        await cut.WaitForElement("[data-testid='shared-provider-publish']").ClickAsync();
        proxy.Read = (_, _) => Task.FromResult(before);
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.Contains("write was not applied", cut.Markup, StringComparison.Ordinal);
        Assert.False(cut.Find("[data-testid='shared-provider-publish']").HasAttribute("disabled"));
        Assert.Equal(1, proxy.Writes);
    }

    [Fact]
    public async Task Parent_catalog_failure_keeps_delivery_pending_and_retry_retains_local_draft() {
        var service = DispatchProxy.Create<ISharedProviderManagementService, SharedProviderOwnedEffectsTests.SourceProxy>();
        var proxy = (SharedProviderOwnedEffectsTests.SourceProxy)(object)service;
        proxy.Delay = false;
        var reads = new Reads();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton(service);
            services.AddSingleton<IProviderProfilesReads>(reads);
        });
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']").Change("Unsaved local name");
        var context = cut.FindComponent<ProviderProfileEditorForm>().Instance.Context;
        await cut.FindAll("button[role='tab']").Single(x => x.TextContent.Contains("Runtime", StringComparison.Ordinal)).ClickAsync();
        var raw = "first\n\n second \n first";
        cut.Find("[data-testid='providers-suggested-models']").Change(raw);
        await cut.Find("[data-testid='providers-connections']").ClickAsync();
        reads.FailCatalog = true;
        await cut.WaitForElement("[data-testid='shared-provider-source-sync']").ClickAsync();
        Assert.Contains("delivery is pending", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(1, proxy.Operations);
        reads.FailCatalog = false;
        await cut.Find("[data-testid='shared-provider-source-verify']").ClickAsync();
        await cut.Find("[data-testid='shared-provider-connections-close']").ClickAsync();
        Assert.Same(context, cut.FindComponent<ProviderProfileEditorForm>().Instance.Context);
        Assert.Equal("Unsaved local name", ((Editor)context.Model).Name);
        Assert.Equal(raw, cut.Find("[data-testid='providers-suggested-models']").GetAttribute("value"));
        Assert.Equal(1, proxy.Operations);
    }

    private sealed class Reads : IProviderProfilesReads {
        private readonly Guid id = Guid.NewGuid();
        public bool FailCatalog { get; set; }
        public Task<ProviderProfilesCatalog> LoadCatalogAsync(CancellationToken token = default) => FailCatalog
            ? Task.FromException<ProviderProfilesCatalog>(new IOException("Synthetic unavailable catalog."))
            : Task.FromResult(new ProviderProfilesCatalog([new Profile(id, "Local provider", CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi,
                "", "", "model", ProviderTransportKind.Responses, true, true, true, false, true,
                "{}", "", "", null, ["model"])], new([])));
        public Task<Editor> LoadEditorAsync(Guid providerId, CancellationToken token = default) =>
            Task.FromResult(new Editor { Id = providerId, Name = "Local provider", DefaultModel = "model" });
    }
}

