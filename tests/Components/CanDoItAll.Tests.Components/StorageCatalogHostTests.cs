using System.Collections.Immutable;
using Bunit;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages;
using CanDoItAll.Modules.Workspace.Pages.Components;
using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;
using CanDoItAll.Workspace.StorageCatalog.UI;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Workspace;

[Trait("Category", "HostPlatform")]
public sealed class StorageCatalogHostTests {
    [Fact]
    public async Task Active_settings_slot_is_lazy_and_recovery_keeps_its_opening_target_across_editor_changes() {
        var counts = new ReadCounts();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.EnableUi();
            services.AddCanDoItAllApi(new ConfigurationBuilder().Build());
            services.AddScoped<WorkspaceStorageCatalogOwner>();
            services.AddScoped<IStorageCatalogOwner>(provider => new ObservedOwner(provider.GetRequiredService<WorkspaceStorageCatalogOwner>(), counts));
        });
        var cut = harness.Context.Render<SettingsPage>();
        cut.WaitForElement("[data-testid=defaults-name]");
        Assert.Equal(0, counts.Reads);
        await cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent.Trim() == "Storage").ClickAsync(new()));
        cut.WaitForElement("[data-testid=storage-settings-name]");
        var surface = cut.FindComponent<CatalogSurface>();
        var session = surface.Instance.Session;
        cut.WaitForAssertion(() => Assert.True(session.Catalog.IsAvailable));
        var system = Assert.Single(session.Catalog.Value, row => row.IsSystemDefault);
        await cut.InvokeAsync(() => session.SelectAsync(system.Id));
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-recovery]").ClickAsync(new()));
        var recovery = cut.FindComponent<StoragePlacementRecoveryDialog>();
        Assert.Equal(system.Id, recovery.Instance.StorageId);
        await cut.InvokeAsync(() => session.New(CatalogProvider.Ipfs));
        Assert.Equal(system.Id, cut.FindComponent<StoragePlacementRecoveryDialog>().Instance.StorageId);
        await cut.InvokeAsync(() => recovery.Instance.OnClose.InvokeAsync());
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-recovery]").ClickAsync(new()));
        Assert.Null(cut.FindComponent<StoragePlacementRecoveryDialog>().Instance.StorageId);
        await cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent.Trim() == "Workspace").ClickAsync(new()));
        Assert.False(session.IsCurrent);
        Assert.Empty(cut.FindComponents<CatalogSurface>());
        Assert.Empty(cut.FindComponents<StoragePlacementRecoveryDialog>());
    }

    private sealed class ReadCounts {
        public int Reads { get; set; }
    }
    private sealed class ObservedOwner(IStorageCatalogOwner inner, ReadCounts counts) : IStorageCatalogOwner {
        public CatalogContext Context => inner.Context;
        public bool IsCurrent => inner.IsCurrent;
        public CatalogChoices Choices => inner.Choices;
        public Task<ImmutableArray<CatalogRow>> ReadCatalogAsync(CancellationToken cancellationToken) {
            counts.Reads++;
            return inner.ReadCatalogAsync(cancellationToken);
        }
        public Task<ImmutableArray<CatalogSecret>> ReadSecretsAsync(CancellationToken cancellationToken) {
            counts.Reads++;
            return inner.ReadSecretsAsync(cancellationToken);
        }
        public Task<ImmutableArray<CatalogRoute>> ReadRoutesAsync(CancellationToken cancellationToken) {
            counts.Reads++;
            return inner.ReadRoutesAsync(cancellationToken);
        }
        public Task<CatalogEdit?> ReadEditorAsync(Guid id, CancellationToken cancellationToken) => inner.ReadEditorAsync(id, cancellationToken);
        public Task<CatalogOutcome> ExecuteAsync(CatalogCommand command, CancellationToken cancellationToken) => inner.ExecuteAsync(command, cancellationToken);
    }
}
