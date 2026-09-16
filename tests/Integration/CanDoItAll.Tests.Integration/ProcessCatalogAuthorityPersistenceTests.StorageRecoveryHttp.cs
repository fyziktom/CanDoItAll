using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Theory]
    [InlineData(StorageRecoveryHttpGrant.None, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData(StorageRecoveryHttpGrant.GeneralApi, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData(StorageRecoveryHttpGrant.StorageOnly, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData(StorageRecoveryHttpGrant.Metadata, HttpStatusCode.OK, HttpStatusCode.Forbidden)]
    [InlineData(StorageRecoveryHttpGrant.ReconcileWithoutProject, HttpStatusCode.OK, HttpStatusCode.Forbidden)]
    [InlineData(StorageRecoveryHttpGrant.Reconcile, HttpStatusCode.OK, HttpStatusCode.OK)]
    public async Task Storage_recovery_HTTP_requires_current_Storage_and_project_scopes(StorageRecoveryHttpGrant grant,
        HttpStatusCode expectedRead, HttpStatusCode expectedCommand) {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        await using var host = await CreateStorageRecoveryHttpHostAsync(clock, driver);
        await using var scope = host.App.Services.CreateAsyncScope();
        var pending = await PreparePendingStorageAsync(scope.ServiceProvider, clock);
        string[] scopes = grant switch {
            StorageRecoveryHttpGrant.None => [],
            StorageRecoveryHttpGrant.GeneralApi => new[] { ApiAccessScopeNames.Api },
            StorageRecoveryHttpGrant.StorageOnly => new[] { ApiAccessScopeNames.ReadStoragePlacementRecovery },
            StorageRecoveryHttpGrant.Metadata => StorageRecoveryTestServices.ReadScopes,
            StorageRecoveryHttpGrant.ReconcileWithoutProject => [.. StorageRecoveryTestServices.ReadScopes, ApiAccessScopeNames.ReconcileStoragePlacement],
            _ => StorageRecoveryTestServices.ReconcileScopes
        };
        if (grant != StorageRecoveryHttpGrant.None) {
            await SetStorageRecoveryTokenAsync(host, scopes);
        }
        var canonical = host.App.Services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var context = new StoragePlacementRecoveryContext(canonical.Profile.Profile.Id, canonical.Generation);
        using var read = await host.Client.GetAsync(StorageRecoveryUrl(context, pending.Prepared.Plan.StorageIntentId));
        Assert.Equal(expectedRead, read.StatusCode);
        if (expectedRead == HttpStatusCode.OK) {
            var item = (await read.Content.ReadFromJsonAsync<StoragePlacementRecoveryItem>())!;
            AssertSafeStorageProjection(item, host.RootPath);
            Assert.Equal(pending.Prepared.Plan.ProjectAdmission.ProjectId, item.Identity.OriginalProjectId);
            Assert.Equal(grant == StorageRecoveryHttpGrant.Reconcile ? StoragePlacementRecoveryAction.Reconcile : StoragePlacementRecoveryAction.None,
                item.AvailableAction);
        }
        driver.ReadFailure = null;
        using var response = await host.Client.PostAsJsonAsync(StoragePlacementRecoveryEndpoints.Route + "/reconcile",
            new StoragePlacementRecoveryCommand(context, pending.Prepared.Plan.StorageIntentId));
        Assert.Equal(expectedCommand, response.StatusCode);
        Assert.DoesNotContain("Private", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(1, driver.StableWrites);
        await AssertAssetRowsAsync(scope.ServiceProvider, pending.Prepared, 0, 0, 0);
    }

    [Theory]
    [InlineData(StorageRecoveryCredentialChange.Revoked)]
    [InlineData(StorageRecoveryCredentialChange.Deleted)]
    [InlineData(StorageRecoveryCredentialChange.ScopeReduced)]
    public async Task Storage_recovery_HTTP_rechecks_managed_credential_after_metadata(StorageRecoveryCredentialChange change) {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        await using var host = await CreateStorageRecoveryHttpHostAsync(clock, driver);
        await using var scope = host.App.Services.CreateAsyncScope();
        var pending = await PreparePendingStorageAsync(scope.ServiceProvider, clock);
        var token = await SetStorageRecoveryTokenAsync(host, StorageRecoveryTestServices.ReconcileScopes);
        var canonical = host.App.Services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var context = new StoragePlacementRecoveryContext(canonical.Profile.Profile.Id, canonical.Generation);
        using (var read = await host.Client.GetAsync(StorageRecoveryUrl(context, pending.Prepared.Plan.StorageIntentId))) {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }
        var registry = host.App.Services.GetRequiredService<IApiTokenRegistry>();
        if (change == StorageRecoveryCredentialChange.Revoked) {
            await registry.RevokeAsync(token.Id, DateTimeOffset.UtcNow);
        } else {
            await registry.DeleteAsync(token.Id);
            if (change == StorageRecoveryCredentialChange.ScopeReduced) {
                registry.Register(token with { Scopes = StorageRecoveryTestServices.ReadScopes });
            }
        }
        driver.ReadFailure = null;
        using var response = await host.Client.PostAsJsonAsync(StoragePlacementRecoveryEndpoints.Route + "/reconcile",
            new StoragePlacementRecoveryCommand(context, pending.Prepared.Plan.StorageIntentId));
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
        Assert.Equal(StorageStablePlacementState.Uncertain,
            (await scope.ServiceProvider.GetRequiredService<StorageStablePlacementService>().FindAsync(pending.Prepared.Plan.StorageIntentId))!.State);
        Assert.Equal(1, driver.StableWrites);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Storage_recovery_HTTP_FTP_requires_explicit_attestation_and_preserves_exact_original_target(bool verified) {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        var transport = new RecoveryFtpTransport();
        await using var host = await CreateStorageRecoveryHttpHostAsync(clock, driver, transport);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var test = await CreateAssetCaseAsync(services, clock, false);
        var owner = services.GetRequiredService<ProjectWorkbenchService>();
        var prepared = await owner.PrepareProcessAssetAsync(test.Invocation, default);
        prepared = await owner.MaterializeProcessAssetAsync(prepared, AssetMedia(), default);
        var ftp = await services.GetRequiredService<IStorageCatalogService>().SaveAsync(new() {
            Name = "Original FTP recovery fixture", ProviderKind = StorageProviderKind.Ftp,
            ConnectionMode = StorageConnectionMode.Remote, EndpointOrRoot = "ftp://private.example.test/original",
            Configuration = new() { Username = "private-account" }, HealthStatus = StorageHealthStatus.Healthy,
            CapabilityMask = StorageCapability.Read | StorageCapability.Write | StorageCapability.Download
        });
        var pending = await services.GetRequiredService<StorageStablePlacementService>().PlaceAsync(prepared.Plan.StorageIntentId,
            new("original.bin", "application/octet-stream", "retained asset content"u8.ToArray(), StorageUsagePurpose.ProjectAsset,
                StorageContentKind.Unknown, test.Owner.Project.ProjectId, RelativePathHint: "original/object.bin", PreferredStorageId: ftp.Id));
        Assert.Equal(StorageStablePlacementState.Uncertain, pending.State);
        await SetStorageRecoveryTokenAsync(host, StorageRecoveryTestServices.ReconcileScopes);
        var canonical = host.App.Services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var context = new StoragePlacementRecoveryContext(canonical.Profile.Profile.Id, canonical.Generation);
        using (var denied = await host.Client.PostAsJsonAsync(StoragePlacementRecoveryEndpoints.Route + "/verify-external-termination",
            new StoragePlacementExternalTerminationVerification(context, prepared.Plan.StorageIntentId, true))) {
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        Assert.Equal(0, transport.Reads);
        await SetStorageRecoveryTokenAsync(host, StorageRecoveryTestServices.VerifyScopes);
        using var response = await host.Client.PostAsJsonAsync(StoragePlacementRecoveryEndpoints.Route + "/verify-external-termination",
            new StoragePlacementExternalTerminationVerification(context, prepared.Plan.StorageIntentId, verified));
        Assert.Equal(verified ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private.example.test", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-account", json, StringComparison.Ordinal);
        Assert.DoesNotContain("original/object.bin", json, StringComparison.Ordinal);
        Assert.Equal(1, transport.Uploads);
        Assert.Equal(verified ? 1 : 0, transport.Reads);
        await using var database = await services.GetRequiredService<IDbContextFactory<StorageDbContext>>().CreateDbContextAsync();
        var row = await database.Set<StoragePlacementIntentRecord>().SingleAsync(item => item.Id == prepared.Plan.StorageIntentId.Value);
        Assert.Equal(verified, row.ExternalDispatchConfirmedStopped);
        Assert.Equal(verified ? StorageStablePlacementState.Completed : StorageStablePlacementState.Uncertain, row.State);
        await AssertAssetRowsAsync(services, prepared, 0, 0, 0);
    }

    private static async Task<ApiTestHost> CreateStorageRecoveryHttpHostAsync(NativeClock clock, AssetDriverProbe driver,
        RecoveryFtpTransport? ftp = null) {
        var configured = AssetHarness(clock, driver);
        return await ApiTestHost.CreateAsync(true, services => {
            configured.ConfigureServices!(services);
            StorageRecoveryTestServices.Add(services, false);
            if (ftp is not null) {
                services.AddSingleton<IFtpStorageTransport>(ftp);
            }
        });
    }

    private static async Task<ApiTokenRecord> SetStorageRecoveryTokenAsync(ApiTestHost host, params string[] scopes) {
        var result = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() {
            Subject = Guid.NewGuid().ToString("N"), DisplayName = "Storage recovery fixture", Scopes = [.. scopes]
        });
        host.Client.DefaultRequestHeaders.Authorization = new(result.TokenType, result.Token);
        return Assert.Single((await host.App.Services.GetRequiredService<IApiTokenRegistry>().SearchAsync(new(result.Subject))).Items);
    }

    private static string StorageRecoveryUrl(StoragePlacementRecoveryContext context, StoragePlacementIntentId id)
        => $"{StoragePlacementRecoveryEndpoints.Route}/{id.Value:D}?databaseProfileId={context.DatabaseProfileId:D}&generation={context.Generation}";

    public enum StorageRecoveryHttpGrant { None, GeneralApi, StorageOnly, Metadata, ReconcileWithoutProject, Reconcile }
    public enum StorageRecoveryCredentialChange { Revoked, Deleted, ScopeReduced }

    private sealed class RecoveryFtpTransport : IFtpStorageTransport {
        private byte[] bytes = [];
        internal int Uploads { get; private set; }
        internal int Reads { get; private set; }
        public Task<string?> TestConnectionAsync(StorageDriverInput storage, string? password, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Recovery must not test a new connection.");
        public Task UploadAsync(StorageDriverInput storage, string? password, string path, ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken) {
            Uploads++;
            bytes = content.ToArray();
            throw new IOException("Private FTP acknowledgement loss.");
        }
        public Task<Stream> OpenReadAsync(StorageDriverInput storage, string? password, string path, CancellationToken cancellationToken) {
            Reads++;
            Assert.Equal("original/object.bin", path);
            return Task.FromResult<Stream>(new MemoryStream(bytes, false));
        }
        public Task DeleteAsync(StorageDriverInput storage, string? password, string path, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Recovery must preserve original bytes.");
        public Task<RemoteBrowseTransportPage> BrowseAsync(StorageDriverInput storage, string? password, string path,
            RemoteBrowseTransportRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Recovery must not browse other targets.");
    }
}
