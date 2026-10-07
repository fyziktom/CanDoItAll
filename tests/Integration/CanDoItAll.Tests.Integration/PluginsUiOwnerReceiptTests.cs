using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.Plugins.Pages;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.Plugins.UI;
using CanDoItAll.SharedKernel.Configuration;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "HostPlatform")]
public sealed class PluginsUiOwnerReceiptTests {
    public enum InvalidUpload { InvalidZip, Oversized, ReadFailure }

    [Fact]
    public async Task Replacement_and_cleanup_double_fault_preserves_stage_and_blocks_presentation_replay() {
        await using var environment = CanDoItAllTestEnvironment.Create("plugins-ui-replacement-double-fault");
        var root = Path.Combine(environment.RootPath, "packages");
        var manifest = Manifest();
        var primary = new IOException("Controlled installed-directory replacement failure");
        var cleanup = new UnauthorizedAccessException("Controlled temporary-directory cleanup failure");
        var attempts = new List<string>();
        await using var provider = await ProviderAsync(environment, root, services => services.AddScoped(serviceProvider =>
            new PluginPackageManifestStore(serviceProvider.GetRequiredService<PluginPackageOptions>(),
                serviceProvider.GetRequiredService<ILogger<PluginPackageManifestStore>>()) {
                DeletePackageDirectory = path => {
                    attempts.Add(path);
                    if (Path.GetFileName(path) == manifest.Plugin.Package!.PackageId.Value) {
                        throw primary;
                    }
                    throw cleanup;
                }
            }));
        await using var scope = provider.CreateAsyncScope();
        var options = provider.GetRequiredService<PluginPackageOptions>();
        Directory.CreateDirectory(Path.Combine(options.InstalledRootPath, manifest.Plugin.Package!.PackageId.Value));
        using var workspace = scope.ServiceProvider.GetRequiredService<PluginWorkspaceSession>()
            .CreateWorkspace(new Uri("http://fixture.invalid/"), _ => Task.CompletedTask);
        await workspace.OpenPackagesAsync();
        using var archive = Archive(manifest);
        var file = new PackageBrowserFile(archive.ToArray());
        await workspace.UploadAsync(file);
        var operation = workspace.View.Operations[new PluginOperationTarget.Upload()];
        Assert.Equal(PluginMutationStatus.Unknown, operation.Status);
        Assert.Equal(PluginPackageStage.ReplacementStarted, operation.PackageProgress!.Stage);
        Assert.Equal(manifest.Plugin.Package.PackageId, operation.PackageProgress.PackageId);
        Assert.Equal(manifest.Plugin.Id, operation.PackageProgress.PluginId);
        Assert.Equal(2, attempts.Count);
        Assert.True(operation.PreventsReplay);
        await workspace.UploadAsync(file);
        await workspace.RefreshAsync();
        Assert.Equal(1, file.OpenCount);
        Assert.Equal(2, attempts.Count);
        Assert.Null(await scope.ServiceProvider.GetRequiredService<PluginInstallationStore>().FindAsync(manifest.Plugin.Id));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(options.RuntimeStateRootPath, "uploads")));
        var archivePath = Path.Combine(environment.RootPath, "owned-fixture.zip");
        await File.WriteAllBytesAsync(archivePath, archive.ToArray());
        var failure = await Assert.ThrowsAsync<PluginPackageStageException>(() =>
            scope.ServiceProvider.GetRequiredService<PluginPackageManifestStore>()
                .ExtractInstalledPackageAsync(archivePath, manifest, options.MaxPackageBytes));
        Assert.Same(primary, failure.InnerException);
        Assert.Same(cleanup, failure.CleanupException);
        Assert.Equal(PluginPackageStage.ReplacementStarted, failure.Progress.Stage);
    }

    [Theory]
    [InlineData(InvalidUpload.InvalidZip)]
    [InlineData(InvalidUpload.Oversized)]
    [InlineData(InvalidUpload.ReadFailure)]
    public async Task Real_invalid_uploads_enforce_limits_and_clean_owned_staging_before_any_installation(InvalidUpload kind) {
        await using var environment = CanDoItAllTestEnvironment.Create("plugins-ui-upload-rejection");
        var root = Path.Combine(environment.RootPath, "packages");
        await using var provider = await ProviderAsync(environment, root, services => services.AddSingleton(new PluginPackageOptions {
            RootPath = root, CatalogRootPath = Path.Combine(root, "catalogue"), InstalledRootPath = Path.Combine(root, "installed"),
            RuntimeStateRootPath = Path.Combine(root, "state"), MaxPackageBytes = 64
        }));
        await using var scope = provider.CreateAsyncScope();
        await using Stream stream = kind == InvalidUpload.ReadFailure ? new FailingStream() : new MemoryStream(new byte[kind == InvalidUpload.Oversized ? 128 : 3]);
        var receipt = await scope.ServiceProvider.GetRequiredService<PluginWorkspaceSession>().UploadAsync(stream, "../../escape.zip", CancellationToken.None);
        Assert.Equal(PluginMutationStatus.Refused, receipt.Status);
        Assert.Null(receipt.PackageProgress);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "state", "uploads")));
        Assert.Null(await scope.ServiceProvider.GetRequiredService<PluginInstallationStore>().FindAsync(Manifest().Plugin.Id));
        Assert.False(File.Exists(Path.Combine(environment.RootPath, "escape.zip")));
    }

    [Fact]
    public async Task Restart_metadata_and_stop_survive_secondary_logger_failures_without_duplicate_requests() {
        await using var environment = CanDoItAllTestEnvironment.Create("plugins-ui-restart-receipt");
        var logger = new ArmedLogger<PluginRuntimeRestartService>();
        using var lifetime = new OwnedLifetime();
        await using var provider = await ProviderAsync(environment, Path.Combine(environment.RootPath, "packages"), services => {
            services.AddSingleton<ILogger<PluginRuntimeRestartService>>(logger);
            services.AddSingleton<IHostApplicationLifetime>(lifetime);
        });
        await using var scope = provider.CreateAsyncScope();
        logger.Armed = true;
        await using var stream = Archive(Manifest());
        var session = scope.ServiceProvider.GetRequiredService<PluginWorkspaceSession>();
        var installed = await session.UploadAsync(stream, "fixture.zip", CancellationToken.None);
        Assert.Equal(PluginMutationStatus.SavedWithWarning, installed.Status);
        Assert.Equal(PluginPackageStage.RestartRecorded, installed.PackageProgress!.Stage);
        Assert.True(installed.PackageProgress.RestartStatus!.IsRestartRequired);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lifetime.ApplicationStopping.Register(() => stopped.TrySetResult());
        var requested = await session.RestartAsync();
        Assert.Equal(PluginMutationStatus.SavedWithWarning, requested.Status);
        Assert.True(requested.Value!.IsRestartRequested);
        var repeated = await session.RestartAsync();
        Assert.Equal(requested.Value, repeated.Value);
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, lifetime.Stops);
    }
    [Fact]
    public async Task Real_upload_keeps_installed_identity_when_restart_metadata_cannot_be_written() {
        await using var environment = CanDoItAllTestEnvironment.Create("plugins-ui-package-receipt");
        var root = Path.Combine(environment.RootPath, "packages");
        await using var provider = await ProviderAsync(environment, root);
        await using var scope = provider.CreateAsyncScope();
        var options = provider.GetRequiredService<PluginPackageOptions>();
        Directory.CreateDirectory(Path.Combine(options.RuntimeStateRootPath, "plugin-runtime-restart.json"));
        var manifest = Manifest();
        await using var stream = Archive(manifest);
        var session = scope.ServiceProvider.GetRequiredService<PluginWorkspaceSession>();
        var receipt = await session.UploadAsync(stream, "../../untrusted-name.zip", CancellationToken.None);
        Assert.Equal(PluginMutationStatus.SavedWithWarning, receipt.Status);
        Assert.Equal(PluginPackageStage.Installed, receipt.PackageProgress!.Stage);
        Assert.Equal(manifest.Plugin.Package!.PackageId, receipt.PackageProgress.PackageId);
        Assert.True(receipt.PackageProgress.RestartRequired);
        Assert.Null(receipt.PackageProgress.RestartStatus);
        var installation = await scope.ServiceProvider.GetRequiredService<PluginInstallationStore>().FindAsync(manifest.Plugin.Id);
        Assert.NotNull(installation);
        Assert.True(File.Exists(Path.Combine(options.InstalledRootPath, manifest.Plugin.Package.PackageId.Value, PluginPackageManifestStore.ManifestFileName)));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(options.RuntimeStateRootPath, "uploads")));
    }

    [Fact]
    public async Task Extracted_archive_is_reported_as_partial_when_extraction_logging_fails() {
        await using var environment = CanDoItAllTestEnvironment.Create("plugins-ui-extraction-receipt");
        var logger = new ArmedLogger<PluginPackageManifestStore>();
        await using var provider = await ProviderAsync(environment, Path.Combine(environment.RootPath, "packages"),
            services => services.AddSingleton<ILogger<PluginPackageManifestStore>>(logger));
        await using var scope = provider.CreateAsyncScope();
        logger.Armed = true;
        var manifest = Manifest();
        await using var stream = Archive(manifest);
        var receipt = await scope.ServiceProvider.GetRequiredService<PluginWorkspaceSession>().UploadAsync(stream, "fixture.zip", CancellationToken.None);
        Assert.Equal(PluginMutationStatus.Unknown, receipt.Status);
        Assert.Equal(PluginPackageStage.Extracted, receipt.PackageProgress!.Stage);
        Assert.Null(await scope.ServiceProvider.GetRequiredService<PluginInstallationStore>().FindAsync(manifest.Plugin.Id));
        var descriptors = await scope.ServiceProvider.GetRequiredService<PluginPackageManifestStore>().ListInstalledPluginDescriptorsAsync();
        Assert.Contains(descriptors, descriptor => descriptor.Id == manifest.Plugin.Id);
    }

    [Fact]
    public async Task Catalog_and_grant_receipts_survive_postcommit_logging_failures() {
        await using var environment = CanDoItAllTestEnvironment.Create("plugins-ui-catalog-grant-receipts");
        var installationLogger = new ArmedLogger<PluginInstallationStore>();
        var grantLogger = new ArmedLogger<PluginGrantStore>();
        await using var provider = await ProviderAsync(environment, Path.Combine(environment.RootPath, "packages"), services => {
            services.AddSingleton<ILogger<PluginInstallationStore>>(installationLogger);
            services.AddSingleton<ILogger<PluginGrantStore>>(grantLogger);
        });
        await using var scope = provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<PluginWorkspaceSession>();
        installationLogger.Armed = true;
        var installed = await session.LifecycleAsync(Office365PluginConstants.PluginId, PluginLifecycleAction.Install);
        Assert.Equal(PluginMutationStatus.SavedWithWarning, installed.Status);
        Assert.True(installed.Value!.IsEnabled);
        grantLogger.Armed = true;
        var grant = await session.GrantAsync(Office365PluginConstants.PluginId,
            new(PluginCapabilityKind.OAuth2, PluginGrantState.Granted));
        Assert.Equal(PluginMutationStatus.SavedWithWarning, grant.Status);
        Assert.Equal(PluginGrantState.Granted, grant.Value!.State);
        var stored = await scope.ServiceProvider.GetRequiredService<PluginGrantStore>().ListAsync(Office365PluginConstants.PluginId);
        Assert.Equal(grant.Value.ConcurrencyToken, Assert.Single(stored).ConcurrencyToken);
        var disabled = await session.LifecycleAsync(Office365PluginConstants.PluginId, PluginLifecycleAction.Disable);
        Assert.Equal(PluginMutationStatus.SavedWithWarning, disabled.Status);
        Assert.False(disabled.Value!.IsEnabled);
        Assert.False((await scope.ServiceProvider.GetRequiredService<PluginInstallationStore>().FindAsync(Office365PluginConstants.PluginId))!.IsEnabled);
    }

    [Fact]
    public async Task OAuth_refusal_after_connection_creation_reports_that_identity_without_session_or_token() {
        await using var environment = CanDoItAllTestEnvironment.Create("plugins-ui-oauth-refusal");
        var descriptor = OAuthDescriptor();
        await using var provider = await ProviderAsync(environment, Path.Combine(environment.RootPath, "packages"),
            services => services.AddScoped<IPluginCatalogSource>(_ => new Source(descriptor)));
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await PrepareOAuthAsync(services, descriptor.Id);
        PluginOAuthProgress? progress = null;
        var result = await services.GetRequiredService<PluginOAuthService>().StartObservedAsync(descriptor.Id,
            new(descriptor.Connections[0].Key), new Uri("http://fixture.invalid/"), "test", value => progress = value);
        Assert.True(result.IsFailure);
        Assert.Equal(PluginOAuthStage.ConnectionResolved, progress!.Stage);
        var connection = Assert.Single(await services.GetRequiredService<PluginConnectionStore>().ListAsync(descriptor.Id));
        Assert.Equal(connection.Id, progress.ConnectionId);
        await using var database = await services.GetRequiredService<IDbContextFactory<PluginsDbContext>>().CreateDbContextAsync();
        Assert.False(await database.Set<PluginOAuthSessionRecord>().AnyAsync(item => item.ConnectionId == connection.Id.Value));
    }

    [Fact]
    public async Task OAuth_session_commit_survives_logger_failure_and_does_not_claim_connected() {
        await using var environment = CanDoItAllTestEnvironment.Create("plugins-ui-oauth-session");
        var logger = new ArmedLogger<PluginOAuthService>();
        var descriptor = OAuthDescriptor() with { OAuth2 = OAuthDescriptor().OAuth2! with { ClientId = "fixture-client" } };
        await using var provider = await ProviderAsync(environment, Path.Combine(environment.RootPath, "packages"), services => {
            services.AddScoped<IPluginCatalogSource>(_ => new Source(descriptor));
            services.AddSingleton<ILogger<PluginOAuthService>>(logger);
        });
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await PrepareOAuthAsync(services, descriptor.Id);
        var session = services.GetRequiredService<PluginWorkspaceSession>();
        using var workspace = session.CreateWorkspace(new("http://fixture.invalid/"), _ => throw new InvalidOperationException("Browser must not open."));
        logger.Armed = true;
        var result = await session.StartOAuthAsync(descriptor.Id, new(descriptor.Connections[0].Key));
        Assert.Equal(PluginMutationStatus.SavedWithWarning, result.Status);
        Assert.Equal(PluginOAuthStage.SessionCreated, result.OAuthProgress!.Stage);
        Assert.Null(result.Value);
        await using var database = await services.GetRequiredService<IDbContextFactory<PluginsDbContext>>().CreateDbContextAsync();
        var persisted = await database.Set<PluginOAuthSessionRecord>().SingleAsync(item => item.PluginId == descriptor.Id.Value);
        Assert.Equal(result.OAuthProgress.ConnectionId.Value, persisted.ConnectionId);
        Assert.Equal("Pending", persisted.Status);
        Assert.NotEmpty(persisted.CodeVerifierVaultKey);
        Assert.Empty(await services.GetRequiredService<PluginOAuthService>().ListStatusesAsync(descriptor.Id));
    }

    [Fact]
    public async Task Moved_contracts_preserve_real_HTTP_wire_defaults_identifiers_and_assembly_forwarding() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var id = Office365PluginConstants.PluginId;
        var response = await host.Client.PostAsJsonAsync($"/api/plugins/{id}/connections", new {
            connectionKey = Office365PluginConstants.ConnectionKey.Value, displayName = "Wire contract", settingsJson = "{}"
        });
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.True(json.RootElement.GetProperty("isEnabled").GetBoolean());
        Assert.Equal(id.Value, json.RootElement.GetProperty("pluginId").GetString());
        Assert.Equal("{}", json.RootElement.GetProperty("settingsJson").GetString());
        var connection = JsonSerializer.Deserialize<PluginConnectionItem>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.NotEqual(Guid.Empty, connection.Id.Value);
        var settings = await host.Client.GetFromJsonAsync<PluginSettingsDetail>($"/api/plugins/{id}/settings");
        Assert.Contains(settings!.Connections, item => item.Id == connection.Id);
        Assert.Equal(typeof(PluginConnectionItem), Type.GetType("CanDoItAll.Modules.Plugins.PluginConnectionItem, CanDoItAll.Modules.Plugins", throwOnError: true));
        Assert.Equal("CanDoItAll.Modules.Plugins.Contracts", typeof(PluginPackageManifest).Assembly.GetName().Name);
        Assert.Equal("CanDoItAll.Modules.Plugins", typeof(IRuntimePluginServiceRegistrar).Assembly.GetName().Name);
        Assert.Equal(2, (int)PluginInstallationStateKind.InstalledDisabled);
        Assert.Equal(1, (int)PluginPackageInstallSourceKind.Upload);
        Assert.True(JsonSerializer.Deserialize<PluginPackageInstallRequest>("{}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Enable);
    }

    private static Task<ServiceProvider> ProviderAsync(CanDoItAllTestEnvironment environment, string packageRoot, Action<IServiceCollection>? configure = null)
        => TestApplicationBootstrap.BuildServiceProviderAsync(environment.CreatePostgreSqlProfile("plugins"), "PluginsUiReceipts",
            TestSchemaBootstrapModules.Full, new Dictionary<string, string?> { ["PluginPackages:RootPath"] = packageRoot }, configure);

    private static async Task PrepareOAuthAsync(IServiceProvider services, PluginId id) {
        Assert.True((await services.GetRequiredService<PluginCatalogService>().InstallAsync(id, new())).IsSuccess);
        Assert.True((await services.GetRequiredService<PluginSettingsService>().UpdateGrantAsync(id,
            new(PluginCapabilityKind.OAuth2, PluginGrantState.Granted), "test")).IsSuccess);
    }

    private static PluginDescriptor OAuthDescriptor() => Manifest().Plugin with {
        Capabilities = PluginCapabilityKind.OAuth2,
        Connections = [new(new("mail"), "Test account", "Fixture", PluginConnectionAuthKind.OAuth2, ConfigurationSchema.Empty())],
        OAuth2 = new(new("mail"), new("https://provider.invalid/authorize"), new("https://provider.invalid/token"), ["profile"])
    };
    private static PluginPackageManifest Manifest() => new() {
        Plugin = new(new("ui.receipt"), "UI receipt fixture", "Harmless manifest", "1.0.0", "Tests", PluginSourceKind.LocalPackage,
            PluginTrustLevel.LocalPackage, "1.0.0", PluginCapabilityKind.None, [], PluginSettingsDescriptor.Empty, [],
            new(new("ui.receipt.package"), "1.0.0", "1.0.0", "", "")), RequiresRestart = true, IconPath = "icon.svg"
    };
    private static MemoryStream Archive(PluginPackageManifest manifest) {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true)) {
            using (var entry = zip.CreateEntry(PluginPackageManifestStore.ManifestFileName).Open()) {
                JsonSerializer.Serialize(entry, manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            using var icon = new StreamWriter(zip.CreateEntry("icon.svg").Open());
            icon.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><path d=\"M1 1h14v14H1z\"/></svg>");
        }
        stream.Position = 0;
        return stream;
    }
    private sealed class Source(PluginDescriptor descriptor) : IPluginCatalogSource {
        public ValueTask<IReadOnlyList<PluginDescriptor>> ListPluginsAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<PluginDescriptor>>([descriptor]);
    }
    private sealed class ArmedLogger<T> : ILogger<T> {
        public bool Armed { get; set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (Armed) {
                throw new InvalidOperationException("Controlled post-commit logging failure.");
            }
        }
    }
    private sealed class OwnedLifetime : IHostApplicationLifetime, IDisposable {
        private readonly CancellationTokenSource stopping = new();
        public int Stops { get; private set; }
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => stopping.Token;
        public void StopApplication() {
            Stops++;
            stopping.Cancel();
        }
        public void Dispose() => stopping.Dispose();
    }
    private sealed class FailingStream : MemoryStream {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(new IOException("Controlled browser stream failure."));
    }
    private sealed class PackageBrowserFile(byte[] bytes) : IBrowserFile {
        public string Name => "fixture.zip";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => bytes.Length;
        public string ContentType => "application/zip";
        public int OpenCount { get; private set; }
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) {
            Assert.True(Size <= maxAllowedSize);
            OpenCount++;
            return new MemoryStream(bytes, writable: false);
        }
    }
}
