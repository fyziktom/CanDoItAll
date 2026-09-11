using System.Net;
using System.Net.Http.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class WorkflowStructureDeliveryPersistenceTests {
    [Theory]
    [InlineData(ContinuationHttpGrant.None)]
    [InlineData(ContinuationHttpGrant.Read)]
    [InlineData(ContinuationHttpGrant.ReconcileWithoutProject)]
    [InlineData(ContinuationHttpGrant.Reconcile)]
    public async Task Storage_operator_Workflow_HTTP_requires_explicit_current_grants_and_original_intent(ContinuationHttpGrant grant) {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-continuation-http");
        var profile = environment.CreatePostgreSqlProfile("original");
        StoragePlacementIntentId storageIntent;
        StoragePlacementWorkflowContinuationIntent original;
        await using (var seed = await TestApplication.CreateAsync(new() {
            TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = ConfigureContinuation
        })) {
            await using var fixture = await CreateFixtureAsync(seed, prepareOutput: false);
            var prepared = await PrepareContinuationAssetAsync(fixture, false);
            storageIntent = prepared.Storage.IntentId;
            original = new(prepared.Plan.Identity.Occurrence.RunId.Value, prepared.Plan.Identity.Occurrence.Path,
                prepared.Plan.Identity.Slot, prepared.Plan.Fingerprint);
        }
        await using var host = await ApiTestHost.CreateAsync(true, services => {
            RemoveAutomaticDelivery(services);
            StorageRecoveryTestServices.Add(services, false);
            services.AddSingleton<ContinuationDriverProbe>();
            services.AddSingleton<IStorageDriverRegistry>(provider => provider.GetRequiredService<ContinuationDriverProbe>());
        }, sharedTestEnvironment: environment, sharedActiveProfile: profile);
        var scopes = grant switch {
            ContinuationHttpGrant.Read => StorageRecoveryTestServices.ReadScopes,
            ContinuationHttpGrant.ReconcileWithoutProject => [.. StorageRecoveryTestServices.ReadScopes, ApiAccessScopeNames.ReconcileStoragePlacement],
            ContinuationHttpGrant.Reconcile => StorageRecoveryTestServices.ReconcileScopes,
            _ => Array.Empty<string>()
        };
        if (grant != ContinuationHttpGrant.None) {
            var token = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() {
                Subject = Guid.NewGuid().ToString("N"), DisplayName = "Workflow continuation operator", Scopes = [.. scopes]
            });
            host.Client.DefaultRequestHeaders.Authorization = new(token.TokenType, token.Token);
        }
        var canonical = host.App.Services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var context = new StoragePlacementRecoveryContext(canonical.Profile.Profile.Id, canonical.Generation);
        var drivers = host.App.Services.GetRequiredService<ContinuationDriverProbe>();
        drivers.Armed = true;
        var url = $"{StoragePlacementRecoveryEndpoints.Route}/{storageIntent.Value:D}/owner-continuation?databaseProfileId={context.DatabaseProfileId:D}&generation={context.Generation}";
        using var read = await host.Client.GetAsync(url);
        Assert.Equal(grant == ContinuationHttpGrant.None ? HttpStatusCode.Forbidden : HttpStatusCode.OK, read.StatusCode);
        if (read.IsSuccessStatusCode) {
            var observation = (await read.Content.ReadFromJsonAsync<StoragePlacementOwnerContinuationObservation>())!;
            Assert.Equal(original, observation.WorkflowIntent);
            Assert.Equal(grant == ContinuationHttpGrant.Reconcile ? StoragePlacementOwnerContinuationAction.CompletePreparedWorkflowAsset
                : StoragePlacementOwnerContinuationAction.None, observation.AvailableAction);
            var json = await read.Content.ReadAsStringAsync();
            Assert.DoesNotContain(host.RootPath, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("proof.json", json, StringComparison.Ordinal);
            Assert.DoesNotContain("base64Data", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("planJson", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("requestJson", json, StringComparison.OrdinalIgnoreCase);
        }
        await using var inspection = host.App.Services.CreateAsyncScope();
        var owner = inspection.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        Assert.Null(await owner.FindWorkflowContributionAsync(new(new(new(original.RunId), original.OccurrencePath), original.Slot)));
        using var response = await host.Client.PostAsJsonAsync(StoragePlacementRecoveryEndpoints.Route + "/continue-workflow-asset",
            new StoragePlacementWorkflowContinuationCommand(context, storageIntent,
                StoragePlacementOwnerContinuationAction.CompletePreparedWorkflowAsset, original));
        Assert.Equal(grant == ContinuationHttpGrant.Reconcile ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        if (response.IsSuccessStatusCode) {
            Assert.Equal(StoragePlacementOwnerContinuationState.ReceiptRecorded,
                (await response.Content.ReadFromJsonAsync<StoragePlacementOwnerContinuationObservation>())!.State);
        }
        Assert.Equal(grant == ContinuationHttpGrant.Reconcile,
            await owner.FindWorkflowContributionAsync(new(new(new(original.RunId), original.OccurrencePath), original.Slot)) is not null);
        Assert.Equal(0, drivers.Attempts);
    }

    public enum ContinuationHttpGrant { None, Read, ReconcileWithoutProject, Reconcile }
}
