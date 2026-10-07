using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.StorageSelection.Contracts;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "HostPlatform")]
public sealed class StorageSelectionAdapterIntegrationTests {
    [Fact]
    public async Task Registered_projection_preserves_real_bootstrap_catalog_and_routing() {
        await using var environment = CanDoItAllTestEnvironment.Create("storage-selection-owner");
        var profile = environment.CreatePostgreSqlProfile("original");
        await using var app = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = profile });
        await using var scope = app.Services.CreateAsyncScope();
        var source = Assert.IsType<WorkspaceStorageCatalogSelectionSource>(scope.ServiceProvider.GetRequiredService<IStorageCatalogSelectionSource>());
        var owner = scope.ServiceProvider.GetRequiredService<IStorageCatalogService>();
        var workspace = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        await owner.SaveAsync(new() {
            Name = "Retained remote metadata", ProviderKind = StorageProviderKind.Ftp,
            EndpointOrRoot = "ftp://example.invalid/archive", Configuration = new() { Port = 2121 }
        });
        Assert.True(source.IsCurrent);
        var initial = await source.ListAsync();
        Assert.Contains(initial, row => row.IsSystemDefault);
        var actual = await owner.ListAsync();
        Assert.Equal((await workspace.ListStorageCatalogAsync()).Select(WorkspaceStorageCatalogSelectionSource.Project), initial);
        var routingBefore = JsonSerializer.Serialize(await workspace.ListStorageRoutingDefaultsAsync());
        Assert.Equal(initial, await source.ListAsync());
        var after = await owner.ListAsync();
        Assert.Equal(actual.Select(row => row.Id), after.Select(row => row.Id));
        foreach (var before in actual) {
            var current = Assert.Single(after, row => row.Id == before.Id);
            if (before.IsSystemDefault) {
                Assert.True(current.RootLastValidatedAtUtc >= before.RootLastValidatedAtUtc);
                Assert.True(current.UpdatedAtUtc >= before.UpdatedAtUtc);
                Assert.Equal(before, current with {
                    RootLastValidatedAtUtc = before.RootLastValidatedAtUtc,
                    UpdatedAtUtc = before.UpdatedAtUtc, SourceFingerprint = before.SourceFingerprint
                });
            } else {
                Assert.Equal(before, current);
            }
        }
        Assert.Equal(routingBefore, JsonSerializer.Serialize(await workspace.ListStorageRoutingDefaultsAsync()));
        Assert.Equal(scope.ServiceProvider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id, source.Context.ProfileId);
    }

    [Fact]
    public async Task Retired_profile_notifies_and_refuses_reads_and_disposal_unsubscribes_once() {
        await using var environment = CanDoItAllTestEnvironment.Create("storage-selection-retirement");
        var profile = environment.CreatePostgreSqlProfile("original");
        await using var app = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = profile });
        await using var scope = app.Services.CreateAsyncScope();
        var source = Assert.IsType<WorkspaceStorageCatalogSelectionSource>(scope.ServiceProvider.GetRequiredService<IStorageCatalogSelectionSource>());
        var changed = 0;
        source.ContextChanged += () => changed++;
        var original = source.Context;
        var next = environment.CreatePostgreSqlProfile("successor");
        await using var successor = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = next });
        var runtime = (DatabaseRuntimeState)app.Services.GetRequiredService<IDatabaseRuntimeState>();
        runtime.PublishRestartObserved(runtime.GetSnapshot(), successor.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        Assert.Equal(1, changed);
        Assert.False(source.IsCurrent);
        Assert.Equal(original, source.Context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.ListAsync());
        source.Dispose();
        source.Dispose();
        Assert.Equal(2, changed);
        app.Services.GetRequiredService<IDatabaseSwitchNotificationService>().Publish(new(original.ProfileId, null, runtime.GetSnapshot().ActiveProfileId!.Value, string.Empty, original.Generation + 2));
        Assert.Equal(2, changed);
    }
}
