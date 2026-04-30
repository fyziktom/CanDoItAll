using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Integration;

public sealed class ManagedFilesStorageIntegrationTests
{
    [Fact]
    public async Task Storage_keeps_managed_files_isolated_between_profiles()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("managed-files-storage");
        var alphaProfile = testEnvironment.CreateManagedSqliteProfile("alpha");
        var betaProfile = testEnvironment.CreateManagedSqliteProfile("beta");

        await using var alphaApplication = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = alphaProfile,
            SchemaModules = TestSchemaBootstrapModules.Full
        });

        await using var betaApplication = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = betaProfile,
            SchemaModules = TestSchemaBootstrapModules.Full
        });

        await using var alphaScope = alphaApplication.Services.CreateAsyncScope();
        await using var betaScope = betaApplication.Services.CreateAsyncScope();

        var alphaSeed = await TestProfileSeedHelper.SeedDistinctProjectAndManagedFileAsync(alphaScope.ServiceProvider, "Alpha");
        var betaSeed = await TestProfileSeedHelper.SeedDistinctProjectAndManagedFileAsync(betaScope.ServiceProvider, "Beta");

        Assert.StartsWith(alphaProfile.WorkspaceRootPath, alphaSeed.ManagedFileFullPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(betaProfile.WorkspaceRootPath, betaSeed.ManagedFileFullPath, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(alphaProfile.WorkspaceRootPath, betaSeed.ManagedFileRelativePath)));
        Assert.False(File.Exists(Path.Combine(betaProfile.WorkspaceRootPath, alphaSeed.ManagedFileRelativePath)));
    }

    [Fact]
    public async Task ManagedFiles_endpoint_serves_the_active_profile_after_a_runtime_switch()
    {
        await using var host = await ManagedFilesTestHost.CreateAsync();
        var betaProfileId = Guid.Empty;
        string alphaPath;
        string betaPath;

        await using (var alphaScope = host.App.Services.CreateAsyncScope())
        {
            var artifactStore = alphaScope.ServiceProvider.GetRequiredService<IManagedArtifactStore>();
            var profileService = alphaScope.ServiceProvider.GetRequiredService<IDatabaseProfileService>();

            alphaPath = await artifactStore.SaveTextAsync("switch-proof", "active.txt", "alpha-profile");

            var saveResult = await profileService.SaveAsync(new DatabaseProfileEditorModel
            {
                DisplayName = "Managed sqlite beta",
                ProviderKind = DatabaseProviderKind.Sqlite,
                SourceKind = DatabaseProfileSourceKind.ManagedSqlite
            });

            Assert.True(saveResult.IsSuccess, string.Join(" ", saveResult.Errors.Select(error => error.Message)));
            betaProfileId = saveResult.Value;
        }

        var beforeSwitchResponse = await host.Client.GetAsync("/managed-files/switch-proof/active.txt");
        beforeSwitchResponse.EnsureSuccessStatusCode();
        Assert.Equal("alpha-profile", await beforeSwitchResponse.Content.ReadAsStringAsync());

        ResolvedDatabaseProfile betaProfile;
        await using (var switchScope = host.App.Services.CreateAsyncScope())
        {
            var runtimeAccessor = switchScope.ServiceProvider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
            var bootstrapper = switchScope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
            var switchCoordinator = switchScope.ServiceProvider.GetRequiredService<IDatabaseSwitchCoordinator>();

            betaProfile = runtimeAccessor.ResolveProfile(betaProfileId);
            await bootstrapper.EnsureProfileReadyAsync(betaProfile);

            var switchResult = await switchCoordinator.SwitchAsync(betaProfileId);
            Assert.True(switchResult.IsSuccess, string.Join(" ", switchResult.Errors.Select(error => error.Message)));
        }

        await using (var betaScope = host.App.Services.CreateAsyncScope())
        {
            var artifactStore = betaScope.ServiceProvider.GetRequiredService<IManagedArtifactStore>();
            betaPath = await artifactStore.SaveTextAsync("switch-proof", "active.txt", "beta-profile");
        }

        var afterSwitchResponse = await host.Client.GetAsync("/managed-files/switch-proof/active.txt");
        afterSwitchResponse.EnsureSuccessStatusCode();
        Assert.Equal("beta-profile", await afterSwitchResponse.Content.ReadAsStringAsync());
        Assert.StartsWith(betaProfile.Profile.Storage.WorkspaceRoot, betaPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("alpha-profile", await File.ReadAllTextAsync(alphaPath));
        Assert.Equal("beta-profile", await File.ReadAllTextAsync(betaPath));
    }

    [Fact]
    public async Task ManagedFiles_traversal_requests_are_rejected()
    {
        await using var host = await ManagedFilesTestHost.CreateAsync();

        var response = await host.Client.GetAsync("/managed-files/..%2F..%2FREADME.md");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StorageObjects_preview_endpoint_serves_managed_file_reference_tokens()
    {
        await using var host = await ManagedFilesTestHost.CreateAsync();

        await using var scope = host.App.Services.CreateAsyncScope();
        var artifactStore = scope.ServiceProvider.GetRequiredService<IManagedArtifactStore>();
        var relativePath = artifactStore.GetRelativePath("route-proof", "alpha.txt").Replace('\\', '/');
        await artifactStore.SaveTextAsync("route-proof", "alpha.txt", "preview-alpha");

        var reference = StorageJson.CreateLegacyManagedFileReference(
            relativePath,
            "text/plain",
            "alpha.txt",
            "preview-alpha".Length);

        var response = await host.Client.GetAsync(StorageJson.BuildPreviewUrl(reference));

        response.EnsureSuccessStatusCode();
        Assert.Equal("preview-alpha", await response.Content.ReadAsStringAsync());
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task StorageObjects_download_endpoint_serves_ipfs_gateway_references()
    {
        await using var server = await FakeIpfsTestServer.StartAsync();
        await using var host = await ManagedFilesTestHost.CreateAsync();
        var cid = await server.StoreTextAsync("ipfs-download");
        var reference = new StorageObjectReference(
            null,
            StorageProviderKind.Ipfs,
            StorageLocatorKind.ContentAddress,
            cid,
            "proof.txt",
            "text/plain",
            "ipfs-download".Length,
            server.CreateGatewayUri(cid).ToString());

        var response = await host.Client.GetAsync(StorageJson.BuildDownloadUrl(reference));

        response.EnsureSuccessStatusCode();
        Assert.Equal("ipfs-download", await response.Content.ReadAsStringAsync());
        Assert.Contains("proof.txt", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Storage_connection_test_service_updates_ipfs_storage_health()
    {
        await using var server = await FakeIpfsTestServer.StartAsync();
        await using var host = await ManagedFilesTestHost.CreateAsync();

        await using var scope = host.App.Services.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IStorageCatalogService>();
        var connectionTestService = scope.ServiceProvider.GetRequiredService<IStorageConnectionTestService>();
        var storage = await catalogService.SaveAsync(new StorageCatalogRecord
        {
            Name = "IPFS integration test",
            ProviderKind = StorageProviderKind.Ipfs,
            ConnectionMode = StorageConnectionMode.Remote,
            EndpointOrRoot = server.ApiBaseUri.ToString(),
            CapabilityMask = StorageCapability.Read |
                             StorageCapability.Write |
                             StorageCapability.Download |
                             StorageCapability.InlinePreview |
                             StorageCapability.DirectUrl |
                             StorageCapability.BatchFolderUpload |
                             StorageCapability.BatchTransfer |
                             StorageCapability.ConnectionTest,
            ConfigJson = StorageJson.SerializeProviderConfiguration(new StorageProviderConfiguration
            {
                GatewayBaseUrl = server.GatewayBaseUri.ToString()
            })
        });

        var result = await connectionTestService.TestAsync(storage.Id);
        var updatedStorage = await catalogService.GetAsync(storage.Id);

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(updatedStorage);
        Assert.Equal(StorageHealthStatus.Healthy, updatedStorage!.HealthStatus);
        Assert.True(updatedStorage.CapabilityMask.HasFlag(StorageCapability.ConnectionTest));
        Assert.Contains("responded successfully", updatedStorage.LastHealthMessage, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class ManagedFilesTestHost : IAsyncDisposable
{
    private ManagedFilesTestHost(
        CanDoItAllTestEnvironment testEnvironment,
        WebApplication app,
        HttpClient client)
    {
        TestEnvironment = testEnvironment;
        App = app;
        Client = client;
    }

    public CanDoItAllTestEnvironment TestEnvironment { get; }

    public WebApplication App { get; }

    public HttpClient Client { get; }

    public static async Task<ManagedFilesTestHost> CreateAsync()
    {
        var testEnvironment = CanDoItAllTestEnvironment.Create("managed-files-host");
        Quartz.Logging.LogProvider.IsDisabled = false;
        Quartz.Logging.LogProvider.SetCurrentLogProvider(new NoOpQuartzLogProvider());

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = testEnvironment.RootPath,
            EnvironmentName = Environments.Development,
            ApplicationName = "CanDoItAll.Tests.Integration"
        });

        builder.Configuration.AddInMemoryCollection(CreateConfigurationValues(testEnvironment));
        TestApplicationBootstrap.ConfigureDefaultServices(
            builder.Services,
            builder.Configuration,
            builder.Environment,
            registerTestHostApplicationLifetime: false);

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapCanDoItAllManagedFiles();

        await TestApplicationBootstrap.InitializeSchemaAsync(app.Services, TestSchemaBootstrapModules.Full);
        await app.StartAsync();

        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses
            ?? throw new InvalidOperationException("The managed-files test host did not expose any server addresses.");
        var client = new HttpClient
        {
            BaseAddress = new Uri(addresses.Single())
        };

        return new ManagedFilesTestHost(testEnvironment, app, client);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync();
        await App.DisposeAsync();
        await TestEnvironment.DisposeAsync();
    }

    private static IReadOnlyDictionary<string, string?> CreateConfigurationValues(CanDoItAllTestEnvironment testEnvironment)
    {
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ControlPlane:RootPath"] = testEnvironment.ControlPlaneRootPath,
            ["Storage:ManagedFilesFolder"] = "managed-files",
            ["Storage:ExportsFolder"] = "exports",
            ["Storage:EvidenceFolder"] = "evidence",
            ["Storage:ManagerArtifactsFolder"] = Path.Combine(testEnvironment.RootPath, "manager-artifacts"),
            ["Workbench:MaxWarmTabs"] = "3",
            ["Workbench:SleepAfterMinutes"] = "15",
            ["Workbench:BrowserStorageKey"] = "candoitall.workbench.session",
            ["DevelopmentManager:TuningModeEnabled"] = "true",
            ["DevelopmentManager:ReviewBeforeSend"] = "true",
            ["DevelopmentManager:ManagerBaseUrl"] = "http://127.0.0.1:6407",
            [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
        };
    }

    private sealed class NoOpQuartzLogProvider : Quartz.Logging.ILogProvider
    {
        public Quartz.Logging.Logger GetLogger(string name)
        {
            return static (_, _, _, _) => false;
        }

        public IDisposable OpenNestedContext(string message)
        {
            return NoOpDisposable.Instance;
        }

        public IDisposable OpenMappedContext(string key, object value, bool destructure = false)
        {
            return NoOpDisposable.Instance;
        }
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public static readonly IDisposable Instance = new NoOpDisposable();

        public void Dispose()
        {
        }
    }
}
