using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.UiSandbox;
using CanDoItAll.Modules.Memory.Components;
using CanDoItAll.Modules.Memory.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.MemoryUi;

public sealed class MemoryExtensionLifetimeTests {
    [Fact]
    public async Task Registered_panel_receives_exact_parameters_and_retires_on_A_B_A_and_disposal() {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var observer = new PanelObserver();
        context.Services.AddSingleton(observer);
        var store = new MemoryScenarioStore();
        var a = (await store.GetSnapshotAsync(MemoryScenarioStore.ProviderA)).SelectedProvider!;
        var b = (await store.GetSnapshotAsync(MemoryScenarioStore.ProviderB)).SelectedProvider!;
        var surface = new MemoryProviderUiSurfaceProjection("registered-panel", MemoryProviderUiSurfaceKind.RazorComponentLibrary,
            "Registered panel", "test.registered", null, MemoryCapabilityIds.UiRcl, MemoryProviderUiSurfaceAvailability.Available, "approved", typeof(TrackedPanel));
        var cut = context.Render<MemoryProviderUiSurfaceHost>(p => p.Add(c => c.Provider, a).Add(c => c.Surfaces, [surface]));
        Assert.Equal(MemoryScenarioStore.ProviderA, cut.Find("[data-testid='extension-provider']").TextContent);
        await cut.InvokeAsync(() => cut.Render(p => p.Add(c => c.Provider, b).Add(c => c.Surfaces, [surface])));
        Assert.Equal(MemoryScenarioStore.ProviderB, cut.Find("[data-testid='extension-provider']").TextContent);
        Assert.Equal([MemoryScenarioStore.ProviderA], observer.Disposed);
        await cut.InvokeAsync(() => cut.Render(p => p.Add(c => c.Provider, a).Add(c => c.Surfaces, [surface])));
        Assert.Equal(3, observer.Created);
        await context.DisposeComponentsAsync();
        Assert.Equal([MemoryScenarioStore.ProviderA, MemoryScenarioStore.ProviderB, MemoryScenarioStore.ProviderA], observer.Disposed);
    }

    public sealed class PanelObserver {
        public int Created { get; set; }
        public List<string> Disposed { get; } = [];
    }
    public sealed class TrackedPanel : ComponentBase, IDisposable {
        [Inject] public PanelObserver Observer { get; set; } = null!;
        [Parameter] public MemoryProviderManagementProfile Provider { get; set; } = null!;
        [Parameter] public MemoryProviderUiSurfaceProjection Surface { get; set; } = null!;
        protected override void OnInitialized() => Observer.Created++;
        protected override void BuildRenderTree(RenderTreeBuilder builder) {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "data-testid", "extension-provider");
            builder.AddContent(2, Provider.InstanceId.Value);
            builder.CloseElement();
        }
        public void Dispose() => Observer.Disposed.Add(Provider.InstanceId.Value);
    }
}
