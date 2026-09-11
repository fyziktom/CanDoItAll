using System.Net;
using System.Net.Http.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Storage_continuation_HTTP_uses_existing_read_authority_and_never_advertises_a_completed_plan_retry(bool authorized) {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        await using var host = await CreateStorageRecoveryHttpHostAsync(clock, driver);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var pending = await PreparePendingStorageAsync(services, clock);
        driver.ReadFailure = null;
        await services.GetRequiredService<StorageStablePlacementService>().ReconcileAsync(pending.Prepared.Plan.StorageIntentId);
        var token = await SetStorageRecoveryTokenAsync(host, authorized ? StorageRecoveryTestServices.ReadScopes : [ApiAccessScopeNames.Api]);
        var canonical = host.App.Services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var context = new StoragePlacementRecoveryContext(canonical.Profile.Profile.Id, canonical.Generation);
        var url = $"{StoragePlacementRecoveryEndpoints.Route}/pending-continuations?databaseProfileId={context.DatabaseProfileId:D}&generation={context.Generation}";
        using var response = await host.Client.GetAsync(url);
        Assert.Equal(authorized ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        if (authorized) {
            var page = (await response.Content.ReadFromJsonAsync<StoragePlacementContinuationPage>())!;
            var item = Assert.Single(page.Items);
            Assert.Equal(StoragePlacementContinuationPhase.NativeCommit, item.Phase);
            Assert.Equal(StoragePlacementRecoveryAction.None, item.Storage.AvailableAction);
            AssertSafeStorageProjection(item.Storage, host.RootPath);
            await host.App.Services.GetRequiredService<IApiTokenRegistry>().RevokeAsync(token.Id, DateTimeOffset.UtcNow);
            using var revoked = await host.Client.GetAsync(url);
            Assert.True(revoked.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
        }
        Assert.DoesNotContain("Private", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await AssertAssetRowsAsync(services, pending.Prepared, 0, 0, 0);
        Assert.Equal(1, driver.StableWrites);
    }
}
