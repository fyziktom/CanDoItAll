using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CanDoItAll.Tests.Unit.Infrastructure;

[Trait("Category", "UnixPortabilityCore")]
public sealed class ExistingDatabaseProfileReaderTests {
    [Fact]
    public async Task Identical_catalog_in_a_different_control_plane_root_does_not_reuse_the_original_identity() {
        await using var fixture = await Fixture.CreateAsync();
        using ServiceProvider provider = fixture.CreateReadProvider();
        var original = await fixture.Reader(provider).ReadAsync(fixture.ProfileId);
        string relocated = Path.Combine(fixture.Environment.RootPath, "other-control-plane");
        fixture.Files.WriteText(relocated, Path.Combine(relocated, "database-profiles", "catalog.json"),
            File.ReadAllText(fixture.CatalogPath), DurableFileWriteOptions.Private);
        var paths = new ControlPlanePathResolver(Options.Create(new ControlPlaneOptions { RootPath = relocated }),
            fixture.Environment.CreateHostEnvironment("Recovery.Tests"), fixture.Files);
        var reader = new ExistingDatabaseProfileReader(paths, fixture.Files,
            new ControlPlaneSecretProtector(provider.GetRequiredService<IDataProtectionProvider>()));
        var changed = await reader.ReadAsync(fixture.ProfileId);
        Assert.NotEqual(original.IdentitySha256, changed.IdentitySha256);
        Assert.Equal(original.CreateResolvedProfile().ConnectionString, changed.CreateResolvedProfile().ConnectionString);
        Assert.Throws<InvalidOperationException>(() => original.RequireSame(changed));
    }

    [Fact]
    public async Task Native_profile_resolves_without_file_changes_and_snapshot_does_not_alias_mutable_records() {
        await using var fixture = await Fixture.CreateAsync();
        using ServiceProvider provider = fixture.CreateReadProvider();
        ExistingDatabaseProfileReader reader = fixture.Reader(provider);
        string[] before = fixture.FileState();
        ExistingDatabaseProfileSnapshot snapshot = await reader.ReadAsync(fixture.ProfileId);
        ResolvedDatabaseProfile resolved = snapshot.CreateResolvedProfile();
        Assert.Equal(fixture.ProfileId, resolved.Profile.Id);
        Assert.Equal(DatabaseProfileResolutionSource.ExplicitPersistedProfile, resolved.ResolutionSource);
        Assert.Equal(Fixture.Password, new NpgsqlConnectionStringBuilder(resolved.ConnectionString).Password);
        Assert.Equal(fixture.WorkspaceRoot, HostBoundPathPolicy.ResolveRequired(resolved.Profile.Storage.WorkspacePath, "test"));
        resolved.Profile.Storage.WorkspacePath!.Path = fixture.Environment.RootPath;
        resolved.Profile.PostgreSql!.Port = 1;
        Assert.Equal(fixture.WorkspaceRoot, snapshot.CreateResolvedProfile().Profile.Storage.WorkspaceRoot);
        Assert.Equal(5432, snapshot.CreateResolvedProfile().Profile.PostgreSql!.Port);
        snapshot.RequireSame(await reader.ReadAsync(fixture.ProfileId));
        Assert.Equal(before, fixture.FileState());
        Assert.False(provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.AutoGenerateKeys);
        Assert.Throws<NotSupportedException>(() => provider.GetRequiredService<IKeyManager>()
            .CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(2)));
        Assert.Equal(before, fixture.FileState());

        JsonObject changed = fixture.ReadCatalog();
        changed["profiles"]![0]!["postgreSql"]!["port"] = 5433;
        fixture.WriteCatalog(changed);
        ExistingDatabaseProfileSnapshot successor = await reader.ReadAsync(fixture.ProfileId);
        Assert.Throws<InvalidOperationException>(() => snapshot.RequireSame(successor));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Explicit_missing_or_empty_identity_never_selects_another_profile(bool empty) {
        await using var fixture = await Fixture.CreateAsync();
        using ServiceProvider provider = fixture.CreateReadProvider();
        string[] before = fixture.FileState();
        Exception? error = await Record.ExceptionAsync(() => fixture.Reader(provider).ReadAsync(empty ? Guid.Empty : Guid.NewGuid()));
        Assert.NotNull(error);
        Assert.Equal(before, fixture.FileState());
    }

    [Theory]
    [InlineData(CatalogFault.LegacySchema)]
    [InlineData(CatalogFault.FutureSchema)]
    [InlineData(CatalogFault.DuplicateIdentity)]
    [InlineData(CatalogFault.DuplicateProperty)]
    [InlineData(CatalogFault.MissingProvider)]
    [InlineData(CatalogFault.ForeignHost)]
    [InlineData(CatalogFault.LegacyWorkspace)]
    [InlineData(CatalogFault.MissingWorkspace)]
    [InlineData(CatalogFault.InvalidPort)]
    [InlineData(CatalogFault.Malformed)]
    [InlineData(CatalogFault.Oversized)]
    [InlineData(CatalogFault.PendingPublication)]
    public async Task Unsafe_catalog_is_refused_without_repair(CatalogFault fault) {
        await using var fixture = await Fixture.CreateAsync();
        using ServiceProvider provider = fixture.CreateReadProvider();
        JsonObject catalog = fixture.ReadCatalog();
        JsonObject profile = catalog["profiles"]![0]!.AsObject();
        switch (fault) {
            case CatalogFault.LegacySchema:
                catalog["schemaVersion"] = 1;
                break;
            case CatalogFault.FutureSchema:
                catalog["schemaVersion"] = 3;
                break;
            case CatalogFault.DuplicateIdentity:
                catalog["profiles"]!.AsArray().Add(profile.DeepClone());
                break;
            case CatalogFault.MissingProvider:
                profile.Remove("providerKind");
                break;
            case CatalogFault.ForeignHost:
                profile["storage"]!["workspacePath"]!["hostBindingId"] = "another-host";
                break;
            case CatalogFault.LegacyWorkspace:
                profile["storage"]!["workspaceRoot"] = fixture.WorkspaceRoot;
                break;
            case CatalogFault.MissingWorkspace:
                profile["storage"]!.AsObject().Remove("workspacePath");
                break;
            case CatalogFault.InvalidPort:
                profile["postgreSql"]!["port"] = 0;
                break;
            case CatalogFault.Oversized:
                profile["displayName"] = new string('x', 1024 * 1024);
                break;
        }
        fixture.WriteCatalog(catalog);
        if (fault == CatalogFault.DuplicateProperty) {
            string json = File.ReadAllText(fixture.CatalogPath);
            fixture.Write(fixture.CatalogPath, json.Insert(1, "\"schemaVersion\":2,"));
        } else if (fault == CatalogFault.Malformed) {
            fixture.Write(fixture.CatalogPath, "{");
        } else if (fault == CatalogFault.PendingPublication) {
            fixture.Write(Path.Combine(Path.GetDirectoryName(fixture.CatalogPath)!,
                $".catalog.json.{Guid.NewGuid():N}.pending"), "pending");
        }
        string[] before = fixture.FileState();
        Exception? error = await Record.ExceptionAsync(() => fixture.Reader(provider).ReadAsync(fixture.ProfileId));
        Assert.NotNull(error);
        Assert.DoesNotContain(Fixture.Password, error.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, fixture.FileState());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Completed_native_migration_reads_but_missing_or_changed_evidence_is_never_repaired(bool missingCommit) {
        await using var fixture = await Fixture.CreateAsync();
        JsonObject legacy = fixture.ReadCatalog();
        legacy["schemaVersion"] = 1;
        JsonObject storage = legacy["profiles"]![0]!["storage"]!.AsObject();
        storage.Remove("workspacePath");
        storage["workspaceRoot"] = fixture.WorkspaceRoot;
        fixture.Write(fixture.CatalogPath, "\n" + legacy.ToJsonString() + "\n");
        Assert.Single(await fixture.Native.ListAsync());
        Assert.True((await fixture.Native.SaveAsync(fixture.Editor(fixture.ProfileId))).IsSuccess);
        using ServiceProvider provider = fixture.CreateReadProvider();
        ExistingDatabaseProfileReader reader = fixture.Reader(provider);
        string[] before = fixture.FileState();
        Assert.Equal(fixture.ProfileId, (await reader.ReadAsync(fixture.ProfileId)).ProfileId);
        Assert.Equal(before, fixture.FileState());
        string migrationRoot = Path.Combine(Path.GetDirectoryName(fixture.CatalogPath)!, "migrations", "workspace-path-v2");
        if (missingCommit) {
            File.Delete(Path.Combine(migrationRoot, "commit.json"));
        } else {
            File.AppendAllText(Path.Combine(migrationRoot, "catalog.v2.staged.json"), "\n");
        }
        string[] damaged = fixture.FileState();
        Assert.NotNull(await Record.ExceptionAsync(() => reader.ReadAsync(fixture.ProfileId)));
        Assert.Equal(damaged, fixture.FileState());
    }

    [Theory]
    [InlineData(KeyFault.Empty)]
    [InlineData(KeyFault.Oversized)]
    [InlineData(KeyFault.TooMany)]
    [InlineData(KeyFault.UnsafeXml)]
    public async Task Unsupported_existing_key_material_does_not_create_or_repair_keys(KeyFault fault) {
        await using var fixture = await Fixture.CreateAsync();
        string originalPath = Directory.GetFiles(fixture.KeysRoot, "*.xml").Single();
        string original = File.ReadAllText(originalPath);
        switch (fault) {
            case KeyFault.Empty:
                File.Delete(originalPath);
                break;
            case KeyFault.Oversized:
                fixture.Write(originalPath, new string('x', 64 * 1024 + 1));
                break;
            case KeyFault.TooMany:
                for (int index = 0; index < 128; index++) {
                    fixture.Write(Path.Combine(fixture.KeysRoot, $"extra-{index}.xml"), original);
                }
                break;
            case KeyFault.UnsafeXml:
                fixture.Write(originalPath, "<!DOCTYPE key [<!ENTITY secret SYSTEM 'file:///unavailable'>]><key>&secret;</key>");
                break;
        }
        string[] before = fixture.FileState();
        using ServiceProvider provider = fixture.CreateReadProvider();
        Assert.NotNull(await Record.ExceptionAsync(() => fixture.Reader(provider).ReadAsync(fixture.ProfileId)));
        Assert.Equal(before, fixture.FileState());
    }

    [Fact]
    public async Task Missing_profile_and_key_roots_are_not_created() {
        await using var environment = CanDoItAllTestEnvironment.Create("control-plane-catalog");
        string missing = Path.Combine(environment.RootPath, "absent");
        var files = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        IHostEnvironment host = environment.CreateHostEnvironment("Recovery.Tests");
        var paths = new ControlPlanePathResolver(Options.Create(new ControlPlaneOptions { RootPath = missing }), host, files);
        var reader = new ExistingDatabaseProfileReader(paths, files,
            new ControlPlaneSecretProtector(new EphemeralDataProtectionProvider()));
        Assert.NotNull(await Record.ExceptionAsync(() => reader.ReadAsync(Guid.NewGuid())));
        Assert.False(Directory.Exists(missing));
        Assert.ThrowsAny<IOException>(() => DataProtectionKeyRingProtection.ConfigureExistingReader(
            new ServiceCollection().AddDataProtection(), new ConfigurationBuilder().Build(), host, missing));
        Assert.False(Directory.Exists(missing));
    }

    public enum CatalogFault {
        LegacySchema, FutureSchema, DuplicateIdentity, DuplicateProperty, MissingProvider, ForeignHost,
        LegacyWorkspace, MissingWorkspace, InvalidPort, Malformed, Oversized, PendingPublication
    }

    public enum KeyFault { Empty, Oversized, TooMany, UnsafeXml }

    private sealed class Fixture : IAsyncDisposable {
        internal const string Password = "recovery-read-only-secret";
        internal CanDoItAllTestEnvironment Environment { get; } = CanDoItAllTestEnvironment.Create("control-plane-catalog");
        internal DurableFileWriter Files { get; } = new(new PhysicalFileSystemPathPolicyFactory());
        internal string WorkspaceRoot => Path.Combine(Environment.RootPath, "workspace");
        internal string CatalogPath => Path.Combine(Environment.ControlPlaneRootPath, "database-profiles", "catalog.json");
        internal string KeysRoot => Path.Combine(Environment.ControlPlaneRootPath, "dataprotection-keys");
        internal Guid ProfileId { get; private set; }
        internal DatabaseProfileControlPlaneService Native { get; }
        private readonly IConfiguration configuration;
        private readonly IHostEnvironment host;
        private readonly ControlPlanePathResolver paths;
        private readonly ServiceProvider nativeProvider;

        private Fixture() {
            host = Environment.CreateHostEnvironment("Recovery.Tests");
            configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["ControlPlane:RootPath"] = Environment.ControlPlaneRootPath,
                ["DataProtection:KeyProtection:Provider"] = "UnprotectedDevelopment"
            }).Build();
            paths = new ControlPlanePathResolver(Options.Create(new ControlPlaneOptions { RootPath = Environment.ControlPlaneRootPath }), host, Files);
            var services = new ServiceCollection();
            DataProtectionKeyRingProtection.Configure(services.AddDataProtection(), configuration, host, KeysRoot, Files);
            nativeProvider = services.BuildServiceProvider();
            Native = new DatabaseProfileControlPlaneService(configuration, Options.Create(new StorageOptions()), host,
                paths, new ControlPlaneSecretProtector(nativeProvider.GetRequiredService<IDataProtectionProvider>()),
                Files, new SystemClock(), NullLogger<DatabaseProfileControlPlaneService>.Instance);
        }

        internal static async Task<Fixture> CreateAsync() {
            var fixture = new Fixture();
            try {
                var saved = await fixture.Native.SaveAsync(fixture.Editor(null));
                Assert.True(saved.IsSuccess);
                fixture.ProfileId = saved.Value;
                return fixture;
            } catch {
                await fixture.DisposeAsync();
                throw;
            }
        }

        internal DatabaseProfileEditorModel Editor(Guid? id) => new() {
            Id = id,
            DisplayName = "Recovery test",
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            PostgresHost = "localhost",
            PostgresPort = 5432,
            PostgresDatabaseName = "recovery-inspection-only",
            PostgresUsername = "postgres",
            PostgresPassword = Password,
            WorkspaceRoot = WorkspaceRoot
        };

        internal ServiceProvider CreateReadProvider() {
            var services = new ServiceCollection();
            DataProtectionKeyRingProtection.ConfigureExistingReader(services.AddDataProtection(), configuration, host, KeysRoot);
            return services.BuildServiceProvider();
        }

        internal ExistingDatabaseProfileReader Reader(ServiceProvider provider)
            => new(paths, Files, new ControlPlaneSecretProtector(provider.GetRequiredService<IDataProtectionProvider>()));

        internal JsonObject ReadCatalog() => JsonNode.Parse(File.ReadAllText(CatalogPath))!.AsObject();
        internal void WriteCatalog(JsonObject catalog) => Write(CatalogPath, catalog.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        internal void Write(string path, string text) => Files.WriteText(Environment.ControlPlaneRootPath, path, text, DurableFileWriteOptions.Private);

        internal string[] FileState() => Directory.EnumerateFiles(Environment.ControlPlaneRootPath, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => $"{Path.GetRelativePath(Environment.ControlPlaneRootPath, path)}:{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}")
            .ToArray();

        public async ValueTask DisposeAsync() {
            await nativeProvider.DisposeAsync();
            await Environment.DisposeAsync();
        }
    }
}
