using System.Reflection;
using Bunit;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class SharedProviderLifetimeRegressionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Replacing_sharing_target_cancels_A_and_ignores_its_late_result(bool failure) {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var pending = new TaskCompletionSource<SharedProviderProfileSharingSnapshot>();
        var service = DispatchProxy.Create<ISharedProviderManagementService, SharingProxy>();
        var proxy = (SharingProxy)(object)service;
        CancellationToken firstToken = default;
        proxy.Read = (id, token) => {
            if (id == first) {
                firstToken = token;
                return pending.Task;
            }
            return Task.FromResult(SharedProviderPublicationPanelTests.CreateLocalState(second, false, false));
        };
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton(service));
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(c => c.ProviderProfileId, first));
        cut.Render(p => p.Add(c => c.ProviderProfileId, second));
        cut.WaitForElement("[data-testid='shared-provider-publish']");
        if (failure) {
            await cut.InvokeAsync(() => pending.SetException(new IOException("Synthetic old read failure.")));
        } else {
            await cut.InvokeAsync(() => pending.SetResult(
                SharedProviderPublicationPanelTests.CreateLocalState(first, false, true)));
        }
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid='shared-provider-publish']").HasAttribute("disabled")));
        Assert.True(firstToken.IsCancellationRequested);
    }

    [Fact]
    public async Task Disposing_sharing_panel_cancels_its_pending_read() {
        var pending = new TaskCompletionSource<SharedProviderProfileSharingSnapshot>();
        var service = DispatchProxy.Create<ISharedProviderManagementService, SharingProxy>();
        CancellationToken received = default;
        ((SharingProxy)(object)service).Read = (_, token) => {
            received = token;
            return pending.Task;
        };
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var id = Guid.NewGuid();
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(c => c.ProviderProfileId, id));
        await cut.InvokeAsync(() => cut.Instance.Dispose());
        pending.SetResult(SharedProviderPublicationPanelTests.CreateLocalState(id, false, true));
        Assert.True(received.IsCancellationRequested);
    }

    public class SharingProxy : DispatchProxy {
        public Func<Guid, CancellationToken, Task<SharedProviderProfileSharingSnapshot>> Read { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method?.Name == nameof(ISharedProviderManagementService.GetProfileSharingAsync)
                ? Read((Guid)args![0]!, (CancellationToken)args[1]!)
                : throw new InvalidOperationException("Unexpected shared mutation.");
    }
}
