using System.Reflection;
using Bunit;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class SharedProviderRecoveryTests {
    [Fact]
    public async Task Unconfirmed_publish_retry_unlocks_authoritative_state() {
        var service = DispatchProxy.Create<ISharedProviderManagementService, SharingProxy>();
        var proxy = (SharingProxy)(object)service;
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(x => x.ProviderProfileId, proxy.Id));
        await cut.WaitForElement("[data-testid='shared-provider-publish']").ClickAsync();
        Assert.NotEmpty(cut.FindAll("[data-testid='shared-provider-warning']"));
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-warning']"));
        Assert.False(cut.Find("[data-testid='shared-provider-unpublish']").HasAttribute("disabled"));
        Assert.Equal(1, proxy.Writes);
    }

    [Fact]
    public async Task Failed_sharing_retry_remains_locked() {
        var service = DispatchProxy.Create<ISharedProviderManagementService, SharingProxy>();
        var proxy = (SharingProxy)(object)service;
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(x => x.ProviderProfileId, proxy.Id));
        await cut.WaitForElement("[data-testid='shared-provider-publish']").ClickAsync();
        proxy.Read = (_, _) => Task.FromException<SharedProviderProfileSharingSnapshot>(new IOException("Unavailable canonical read."));
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.NotEmpty(cut.FindAll("[data-testid='shared-provider-warning']"));
        Assert.Equal(1, proxy.Writes);
        proxy.Read = null;
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-warning']"));
        Assert.False(cut.Find("[data-testid='shared-provider-unpublish']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Rejected_sharing_retry_clears_obsolete_warning() {
        var service = DispatchProxy.Create<ISharedProviderManagementService, SharingProxy>();
        var proxy = (SharingProxy)(object)service;
        proxy.Reject = true;
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(x => x.ProviderProfileId, proxy.Id));
        await cut.WaitForElement("[data-testid='shared-provider-publish']").ClickAsync();
        Assert.NotEmpty(cut.FindAll("[data-testid='shared-provider-warning']"));
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-warning']"));
        Assert.False(cut.Find("[data-testid='shared-provider-publish']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Stale_sharing_verification_cannot_unlock_other_target() {
        var service = DispatchProxy.Create<ISharedProviderManagementService, SharingProxy>();
        var proxy = (SharingProxy)(object)service;
        var second = Guid.NewGuid();
        var pending = new TaskCompletionSource<SharedProviderProfileSharingSnapshot>();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(x => x.ProviderProfileId, proxy.Id));
        await cut.WaitForElement("[data-testid='shared-provider-publish']").ClickAsync();
        proxy.Read = (id, _) => id == proxy.Id ? pending.Task : Task.FromResult(proxy.State(id));
        var retry = cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        cut.Render(p => p.Add(x => x.ProviderProfileId, second));
        await cut.WaitForElement("[data-testid='shared-provider-publish']").ClickAsync();
        pending.SetResult(proxy.State(proxy.Id));
        await retry;
        Assert.NotEmpty(cut.FindAll("[data-testid='shared-provider-warning']"));
        Assert.True(cut.Find("[data-testid='shared-provider-publish']").HasAttribute("disabled"));
        Assert.Equal(2, proxy.Writes);
        proxy.Read = null;
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.False(cut.Find("[data-testid='shared-provider-unpublish']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Imported_settings_verification_reloads_tokens_without_replay() {
        var service = DispatchProxy.Create<ISharedProviderManagementService, SharingProxy>();
        var proxy = (SharingProxy)(object)service;
        proxy.Imported = true;
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(x => x.ProviderProfileId, proxy.Id));
        cut.WaitForElement("[data-testid='shared-provider-import-alias']").Change("Submitted alias");
        await cut.Find("[data-testid='shared-provider-import-save']").ClickAsync();
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.Equal(1, proxy.Writes);
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-warning']"));
        Assert.False(cut.Find("[data-testid='shared-provider-import-save']").HasAttribute("disabled"));
        Assert.Equal(proxy.ImportToken, cut.FindComponent<SharedProviderImportedProfileContent>().Instance.Import.ImportConcurrencyToken);
        Assert.Equal("Submitted alias", cut.Find("[data-testid='shared-provider-import-alias']").GetAttribute("value"));
    }

    public class SharingProxy : DispatchProxy {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid ImportId { get; } = Guid.NewGuid();
        public Guid ImportToken { get; } = Guid.NewGuid();
        public bool Imported { get; set; }
        public bool Reject { get; set; }
        public int Writes { get; private set; }
        public Func<Guid, CancellationToken, Task<SharedProviderProfileSharingSnapshot>>? Read { get; set; }
        private readonly HashSet<Guid> saved = [];
        private readonly Dictionary<Guid, SharedProviderProfileSharingSnapshot> states = [];
        private readonly Guid sourceId = Guid.NewGuid();
        private readonly Guid originalImportToken = Guid.NewGuid();
        private readonly Guid originalProviderToken = Guid.NewGuid();
        private readonly Guid savedProviderToken = Guid.NewGuid();
        private readonly CanDoItAll.SharedProviders.Abstractions.SharedProviderPublicationId publicationId = new(Guid.NewGuid());
        public string? CanonicalAliasOverride { get; set; }
        private string requestedAlias = "Original alias";
        private bool intendedEnabled = true;

        public SharedProviderProfileSharingSnapshot State(Guid id) {
            if (!Imported) {
                if (!states.TryGetValue(id, out var state)) {
                    state = SharedProviderPublicationPanelTests.CreateLocalState(id, false, true);
                    states[id] = state;
                }
                return saved.Contains(id) ? state with { Publication = state.Publication! with {
                    IsPublished = true, ConcurrencyToken = savedProviderToken } } : state;
            }
            var publication = publicationId;
            return new(id, SharedProviderProfileOwnership.Imported, null, null,
                new(ImportId, sourceId, "Fixture source", publication, id,
                    saved.Contains(id) ? CanonicalAliasOverride ?? requestedAlias : "Original alias", saved.Contains(id) ? intendedEnabled : true, "Remote",
                    CanDoItAll.SharedProviders.Abstractions.SharedProviderPurpose.Chat,
                    CanDoItAll.SharedProviders.Abstractions.SharedProviderTransport.OpenAiCompatible,
                    CanDoItAll.SharedProviders.Abstractions.SharedProviderRoutingModelIdCodec.Create(publication, "model"),
                    SharedProviderSelectionState.Selected,
                    SharedProviderAvailabilityState.Available, [],
                    saved.Contains(id) ? ImportToken : originalImportToken, saved.Contains(id) ? savedProviderToken : originalProviderToken));
        }

        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            if (method?.Name == nameof(ISharedProviderManagementService.GetProfileSharingAsync)) {
                var id = (Guid)args![0]!;
                return Read?.Invoke(id, (CancellationToken)args[^1]!) ?? Task.FromResult(State(id));
            }
            if (method?.Name is nameof(ISharedProviderManagementService.SetPublicationAsync) or nameof(ISharedProviderManagementService.UpdateImportedProfileAsync)) {
                var id = args![0] is SharedProviderImportedProfileUpdateRequest request ? request.ProviderProfileId : (Guid)args[0]!;
                if (args[0] is SharedProviderImportedProfileUpdateRequest settings) {
                    requestedAlias = settings.LocalAlias.Trim();
                    intendedEnabled = settings.IsEnabled;
                }
                Writes++;
                if (Reject) {
                    return Task.FromException<SharedProviderProfileSharingSnapshot>(new SharedProviderConcurrencyException("Publication", id));
                }
                saved.Add(id);
                return Task.FromException<SharedProviderProfileSharingSnapshot>(new IOException("Synthetic unknown response."));
            }
            throw new InvalidOperationException("Unexpected recovery call.");
        }
    }
}
