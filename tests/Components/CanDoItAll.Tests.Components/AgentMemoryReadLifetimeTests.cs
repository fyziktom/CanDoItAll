using Bunit;
using CanDoItAll.AgentFramework.Editor.UI;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.AgentFramework.Pages.Components;

namespace CanDoItAll.Tests.Components;

public sealed class AgentMemoryReadLifetimeTests : AgentMemorySettingsPanelTestBase {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Metadata_read_survives_tab_remount_but_never_publishes_to_retired_owner(bool fail, bool retire) {
        var pending = new TaskCompletionSource<IReadOnlyList<MemoryProviderProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new HeldStore(pending.Task);
        using var context = CreateContext(store);
        using var lifetime = new CancellationTokenSource();
        var settings = CreateSettingsWithBinding("saved", "saved-provider");
        var state = new AgentMemoryEditorState(settings) { NewAlias = "unadded-東京" };
        var first = context.Render<AgentMemorySettingsPanel>(p => p.Add(x => x.Value, settings)
            .Add(x => x.State, state).Add(x => x.OwnerLifetime, lifetime.Token));
        Assert.True(state.ProvidersLoading);
        first.Dispose();
        if (retire) {
            lifetime.Cancel();
        }
        var nextState = retire ? new AgentMemoryEditorState(new()) { ProvidersLoaded = true } : state;
        var next = context.Render<AgentMemorySettingsPanel>(p => p.Add(x => x.Value, nextState.Value).Add(x => x.State, nextState));
        Assert.Equal(1, store.Reads);
        await next.InvokeAsync(() => {
            if (fail) {
                pending.SetException(new IOException("private metadata error"));
            } else {
                pending.SetResult([CreateProvider("late-provider", "Late safe provider", true)]);
            }
        });
        next.WaitForAssertion(() => Assert.False(state.ProvidersLoading));
        if (retire) {
            Assert.Empty(nextState.AvailableProviders);
            Assert.Empty(state.AvailableProviders);
        } else if (fail) {
            next.WaitForAssertion(() => Assert.Contains("could not be loaded", next.Markup));
            Assert.DoesNotContain("private metadata error", next.Markup);
        } else {
            next.WaitForElement("option[value='late-provider']");
        }
        Assert.Equal("unadded-東京", state.NewAlias);
        Assert.Equal("saved", Assert.Single(settings.ProviderBindings).Alias.Value);
    }

    private sealed class HeldStore(Task<IReadOnlyList<MemoryProviderProfile>> pending) : IMemoryProviderProfileStore {
        public int Reads { get; private set; }
        public Task<IReadOnlyList<MemoryProviderProfile>> ListAsync(CancellationToken cancellationToken = default) {
            Reads++;
            return pending;
        }
        public Task<MemoryProviderProfile?> GetAsync(MemoryProviderInstanceId id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task UpsertAsync(MemoryProviderProfile profile, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
