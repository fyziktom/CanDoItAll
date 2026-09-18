using System.Reflection;
using Bunit;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class SharedProviderRefreshButtonTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Refresh_preserves_selected_imports_and_emits_explicit_scope(bool fail) {
        using var context = new BunitContext();
        var service = DispatchProxy.Create<ISharedProviderManagementService, RefreshProxy>();
        var proxy = (RefreshProxy)(object)service;
        proxy.Fail = fail;
        context.Services.AddSingleton(service);
        context.Services.AddScoped<SharedProviderRecovery>();
        var notified = false;
        SharedProviderChange? change = null;
        var cut = context.Render<SharedProviderRefreshButton>(p => p
            .Add(c => c.ProviderId, proxy.Selected.ProviderProfileId)
            .Add(c => c.Refreshed, (SharedProviderChangeDelivery delivery) => delivery.ReconcileAsync(() => {
                change = delivery.Change;
                notified = true;
                return Task.CompletedTask;
            })));
        Assert.Equal(0, proxy.ListCalls);
        cut.Find("[data-testid='shared-provider-refresh-capabilities']").Click();
        cut.WaitForAssertion(() => {
            Assert.Equal([proxy.Selected.RemotePublicationId], proxy.SynchronizedIds);
            Assert.True(notified);
            Assert.NotNull(change);
            Assert.Equal(fail, change.UnknownScope);
            Assert.Equal(fail ? SharedProviderCommitState.Unconfirmed : SharedProviderCommitState.Committed, change.CommitState);
        });
        Assert.Contains(fail ? "could not be confirmed" : "unsaved selections were preserved",
            cut.Find("[data-testid='shared-provider-refresh-result']").TextContent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Superseded_refresh_read_keeps_its_token_until_completion(bool dispose) {
        using var context = new BunitContext();
        var service = DispatchProxy.Create<ISharedProviderManagementService, RefreshProxy>();
        var proxy = (RefreshProxy)(object)service;
        proxy.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Services.AddSingleton(service);
        context.Services.AddScoped<SharedProviderRecovery>();
        var notifications = 0;
        var cut = context.Render<SharedProviderRefreshButton>(parameters => parameters
            .Add(component => component.ProviderId, proxy.Selected.ProviderProfileId)
            .Add(component => component.Refreshed, _ => notifications++));
        var refresh = cut.Find("[data-testid='shared-provider-refresh-capabilities']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Equal(1, proxy.ListCalls));
        if (dispose) {
            await cut.InvokeAsync(cut.Instance.Dispose);
        } else {
            cut.Render(parameters => parameters.Add(component => component.ProviderId, Guid.NewGuid()));
        }
        Assert.True(proxy.Token.IsCancellationRequested);
        var callbacks = 0;
        using var registration = proxy.Token.Register(() => callbacks++);
        Assert.Equal(1, callbacks);
        Assert.True(proxy.Token.WaitHandle.WaitOne(0));
        proxy.Pending.SetResult([]);
        await refresh;
        Assert.Throws<ObjectDisposedException>(() => proxy.Token.WaitHandle);
        Assert.Empty(proxy.SynchronizedIds);
        Assert.Equal(0, notifications);
    }

    public class RefreshProxy : DispatchProxy {
        public bool Fail { get; set; }
        public TaskCompletionSource<IReadOnlyList<SharedProviderSourceManagementSnapshot>>? Pending { get; set; }
        public CancellationToken Token { get; private set; }
        public int ListCalls { get; private set; }
        public SharedProviderImportedProfileSnapshot Selected { get; } = Import(SharedProviderSelectionState.Selected);
        public SharedProviderImportedProfileSnapshot Retired { get; } = Import(SharedProviderSelectionState.Retired);
        public IReadOnlyList<SharedProviderPublicationId> SynchronizedIds { get; private set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name == nameof(ISharedProviderManagementService.ListSourcesAsync)) {
                ListCalls++;
                Token = (CancellationToken)args![0]!;
                if (Pending is not null) {
                    return Pending.Task;
                }
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
                    : Task.FromResult(SharedProviderSourceOperationResult.NotModified(new SharedProviderCatalogEntityTag($"\"sha256:{new string('a', 64)}\"")) with {
                        Change = new(SharedProviderChangeKind.Reconciliation, [Selected.ProviderProfileId], remoteOwnedFieldsChanged: true)
                    });
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
