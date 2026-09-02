using System.Reflection;
using Bunit;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class SharedProviderRefreshButtonTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Refresh_preserves_selected_imports_and_notifies_only_on_success(bool fail) {
        using var context = new BunitContext();
        var service = DispatchProxy.Create<ISharedProviderManagementService, RefreshProxy>();
        var proxy = (RefreshProxy)(object)service;
        proxy.Fail = fail;
        context.Services.AddSingleton(service);
        var notified = false;
        var cut = context.Render<SharedProviderRefreshButton>(p => p
            .Add(c => c.ProviderId, proxy.Selected.ProviderProfileId)
            .Add(c => c.Refreshed, () => notified = true));
        Assert.Equal(0, proxy.ListCalls);
        cut.Find("[data-testid='shared-provider-refresh-capabilities']").Click();
        cut.WaitForAssertion(() => {
            Assert.Equal([proxy.Selected.RemotePublicationId], proxy.SynchronizedIds);
            Assert.Equal(!fail, notified);
        });
        Assert.Contains(fail ? "Source unavailable" : "unsaved selections were preserved",
            cut.Find("[data-testid='shared-provider-refresh-result']").TextContent);
    }

    public class RefreshProxy : DispatchProxy {
        public bool Fail { get; set; }
        public int ListCalls { get; private set; }
        public SharedProviderImportedProfileSnapshot Selected { get; } = Import(SharedProviderSelectionState.Selected);
        public SharedProviderImportedProfileSnapshot Retired { get; } = Import(SharedProviderSelectionState.Retired);
        public IReadOnlyList<SharedProviderPublicationId> SynchronizedIds { get; private set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name == nameof(ISharedProviderManagementService.ListSourcesAsync)) {
                ListCalls++;
                var source = new SharedProviderSourceSnapshot(Selected.SourceId, "Source", new Uri("http://source.invalid/"),
                    Guid.NewGuid(), true, SharedProviderSourceNetworkPolicy.AllowPrivateNetwork, SharedProviderSourceStatus.Available,
                    null, null, null, null, "", Guid.NewGuid());
                return Task.FromResult<IReadOnlyList<SharedProviderSourceManagementSnapshot>>([new(source, [Selected, Retired])]);
            }
            if (targetMethod?.Name == nameof(ISharedProviderManagementService.SynchronizeSourceAsync)) {
                Assert.Equal(Selected.SourceId, args![0]);
                SynchronizedIds = ((IReadOnlySet<SharedProviderPublicationId>)args[1]!).ToArray();
                return Fail
                    ? Task.FromException<SharedProviderSourceOperationResult>(new InvalidOperationException("Source unavailable"))
                    : Task.FromResult(SharedProviderSourceOperationResult.NotModified(new SharedProviderCatalogEntityTag($"\"sha256:{new string('a', 64)}\"")));
            }
            throw new InvalidOperationException($"Unexpected operation {targetMethod?.Name}.");
        }

        private static SharedProviderImportedProfileSnapshot Import(SharedProviderSelectionState selection) {
            var publication = new SharedProviderPublicationId(Guid.NewGuid());
            return new(Guid.NewGuid(), Guid.NewGuid(), "Source", publication, Guid.NewGuid(), "Alias", true, "Provider",
                SharedProviderPurpose.Chat, SharedProviderTransport.OpenAiCompatible,
                SharedProviderRoutingModelIdCodec.Create(publication, "real-model"), selection,
                SharedProviderAvailabilityState.Available, [], Guid.NewGuid(), Guid.NewGuid());
        }
    }
}
