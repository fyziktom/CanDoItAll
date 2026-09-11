using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
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
    public async Task Storage_cancelled_receipt_HTTP_requires_current_explicit_reconcile_scope(bool canReconcile) {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        await using var host = await CreateStorageRecoveryHttpHostAsync(clock, driver);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await CreateOwnerContinuationCaseAsync(services, clock, driver, true);
        await CancelOwnerContinuationRunAsync(services, created.Test.Run.Id);
        await SetStorageRecoveryTokenAsync(host, canReconcile ? StorageRecoveryTestServices.ReconcileScopes : StorageRecoveryTestServices.ReadScopes);
        var canonical = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var context = new StoragePlacementRecoveryContext(canonical.Profile.Profile.Id, canonical.Generation);
        var command = new StoragePlacementRecoveryCommand(context, created.Prepared.Plan.StorageIntentId);
        var url = StorageRecoveryUrl(context, command.IntentId).Replace("?", "/owner-continuation?", StringComparison.Ordinal);
        using var read = await host.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var observation = (await read.Content.ReadFromJsonAsync<StoragePlacementOwnerContinuationObservation>())!;
        Assert.Equal(canReconcile ? StoragePlacementOwnerContinuationAction.ReconcileCancelledRunReceipts
            : StoragePlacementOwnerContinuationAction.None, observation.AvailableAction);
        var store = services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var before = JsonSerializer.Serialize((await store.GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission);
        using var response = await host.Client.PostAsJsonAsync(StoragePlacementRecoveryEndpoints.Route + "/reconcile-cancelled-run-receipts", command);
        Assert.Equal(canReconcile ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        if (canReconcile) {
            var result = (await response.Content.ReadFromJsonAsync<StoragePlacementOwnerContinuationObservation>())!;
            Assert.Equal(StoragePlacementOwnerContinuationState.ReceiptRecorded, result.State);
            AssertSafeOwnerContinuation(result, host.RootPath);
        } else {
            Assert.Equal(before, JsonSerializer.Serialize((await store.GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission));
        }
        Assert.Equal(1, driver.StableWrites);
    }

    [Fact]
    public async Task Storage_cancelled_receipt_HTTP_rechecks_revocation_after_read_and_retains_original_journal() {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        await using var host = await CreateStorageRecoveryHttpHostAsync(clock, driver);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await CreateOwnerContinuationCaseAsync(services, clock, driver, true);
        await CancelOwnerContinuationRunAsync(services, created.Test.Run.Id);
        var credential = await SetStorageRecoveryTokenAsync(host, StorageRecoveryTestServices.ReconcileScopes);
        var canonical = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var context = new StoragePlacementRecoveryContext(canonical.Profile.Profile.Id, canonical.Generation);
        var command = new StoragePlacementRecoveryCommand(context, created.Prepared.Plan.StorageIntentId);
        using (var read = await host.Client.GetAsync(StorageRecoveryUrl(context, command.IntentId)
            .Replace("?", "/owner-continuation?", StringComparison.Ordinal))) {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }
        var store = services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var before = JsonSerializer.Serialize((await store.GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission);
        await host.App.Services.GetRequiredService<IApiTokenRegistry>().RevokeAsync(credential.Id, DateTimeOffset.UtcNow);
        using var response = await host.Client.PostAsJsonAsync(StoragePlacementRecoveryEndpoints.Route + "/reconcile-cancelled-run-receipts", command);
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
        Assert.Equal(before, JsonSerializer.Serialize((await store.GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission));
        Assert.Equal(1, driver.StableWrites);
    }
}
