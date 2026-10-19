using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class ExistingDatabaseProfileSnapshot {
    private readonly byte[] profileBytes;
    private readonly string connectionString;

    internal ExistingDatabaseProfileSnapshot(DatabaseProfileRecord profile, string connectionString, string identitySha256) {
        ProfileId = profile.Id;
        profileBytes = JsonSerializer.SerializeToUtf8Bytes(profile, ExistingDatabaseProfileReader.SerializerOptions);
        this.connectionString = connectionString;
        IdentitySha256 = identitySha256;
    }

    public Guid ProfileId { get; }
    public string IdentitySha256 { get; }

    public ResolvedDatabaseProfile CreateResolvedProfile()
        => new(JsonSerializer.Deserialize<DatabaseProfileRecord>(profileBytes, ExistingDatabaseProfileReader.SerializerOptions)!,
            DatabaseProfileResolutionSource.ExplicitPersistedProfile, connectionString);

    public void RequireSame(ExistingDatabaseProfileSnapshot observed) {
        ArgumentNullException.ThrowIfNull(observed);
        if (ProfileId != observed.ProfileId || IdentitySha256 != observed.IdentitySha256 ||
            !string.Equals(connectionString, observed.connectionString, StringComparison.Ordinal)) {
            throw new InvalidOperationException("The explicit database profile changed during recovery.");
        }
    }
}

public sealed class ExistingDatabaseProfileReader(IControlPlanePathResolver paths,
    DurableFileWriter files, IControlPlaneSecretProtector secrets) {
    private const int MaximumCatalogBytes = 1024 * 1024;
    private const int MaximumProfiles = 1024;
    private const int MaximumDirectoryEntries = 256;
    internal static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public async Task<ExistingDatabaseProfileSnapshot> ReadAsync(Guid profileId, CancellationToken cancellationToken = default) {
        if (profileId == Guid.Empty) {
            throw new ArgumentException("Recovery requires an explicit profile ID.", nameof(profileId));
        }
        cancellationToken.ThrowIfCancellationRequested();
        string root = paths.ResolveRootPathForRead();
        await using IAsyncDisposable coordination = await ControlPlaneFileCoordination.AcquireExistingAsync(
            files, root, ControlPlaneCoordinationScope.DatabaseProfiles, cancellationToken).ConfigureAwait(false);
        using var identity = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        identity.AppendData(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            Domain = "database-profile-recovery:v1", ProfileId = profileId, ControlPlaneRoot = Path.GetFullPath(root)
        })));
        using JsonDocument catalog = ReadDocument(root, Path.Combine(root, "database-profiles", "catalog.json"),
            MaximumCatalogBytes, identity);
        if (catalog.RootElement.GetProperty("schemaVersion").GetInt32() != 2) {
            throw new InvalidDataException("Recovery requires the current database profile catalog schema.");
        }
        JsonElement profiles = catalog.RootElement.GetProperty("profiles");
        if (profiles.ValueKind != JsonValueKind.Array || profiles.GetArrayLength() is 0 or > MaximumProfiles) {
            throw new InvalidDataException("The database profile catalog exceeds its supported bounds.");
        }
        DatabaseProfileRecord? selected = null;
        var identities = new HashSet<Guid>();
        foreach (JsonElement item in profiles.EnumerateArray()) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(item.GetProperty("id").GetString(), "D", out Guid id) ||
                id == Guid.Empty || !identities.Add(id)) {
                throw new InvalidDataException("The database profile catalog contains an ambiguous identity.");
            }
            if (id == profileId) {
                RequireProfileFields(item);
                selected = item.Deserialize<DatabaseProfileRecord>(SerializerOptions);
            }
        }
        if (selected is null) {
            throw new InvalidOperationException("The explicitly selected database profile does not exist.");
        }
        RequireCompletedMigration(root, identity);
        if (selected.ProviderKind != DatabaseProviderKind.PostgreSql ||
            selected.SourceKind != DatabaseProfileSourceKind.PostgresConnection || selected.InMemory is not null ||
            selected.Storage is null || selected.Storage.Mode != DatabaseProfileStorageMode.ExternalWorkspaceRoot ||
            selected.Storage.LegacyWorkspaceRoot is not null || selected.PostgreSql is null ||
            selected.PostgreSql.Port is < 1 or > 65535) {
            throw new InvalidDataException("Recovery requires a bound native PostgreSQL profile.");
        }
        ResolvedDatabaseProfile resolved = DatabaseProfileConnection.Resolve(selected,
            DatabaseProfileResolutionSource.ExplicitPersistedProfile, secrets);
        cancellationToken.ThrowIfCancellationRequested();
        return new ExistingDatabaseProfileSnapshot(selected, resolved.ConnectionString,
            Convert.ToHexStringLower(identity.GetHashAndReset()));
    }

    private static void RequireProfileFields(JsonElement profile) {
        foreach (string name in new[] { "providerKind", "sourceKind", "storage", "postgreSql" }) {
            _ = profile.GetProperty(name);
        }
        JsonElement storage = profile.GetProperty("storage");
        _ = storage.GetProperty("mode");
        JsonElement binding = storage.GetProperty("workspacePath");
        foreach (string name in new[] { "formatVersion", "platformFamily", "pathSyntax", "hostBindingId", "path", "state" }) {
            _ = binding.GetProperty(name);
        }
        JsonElement postgres = profile.GetProperty("postgreSql");
        _ = postgres.GetProperty("port");
        foreach (string name in new[] { "host", "databaseName", "username" }) {
            string? value = postgres.GetProperty(name).GetString();
            if (string.IsNullOrWhiteSpace(value) || value.Length > 1024) {
                throw new InvalidDataException("The selected PostgreSQL connection metadata is incomplete or oversized.");
            }
        }
    }

    private static void RequireCompletedMigration(string root, IncrementalHash identity) {
        string directory = Path.Combine(root, "database-profiles", "migrations", "workspace-path-v2");
        try {
            _ = File.GetAttributes(directory);
        } catch (FileNotFoundException) {
            return;
        } catch (DirectoryNotFoundException) {
            return;
        }
        ExistingPrivateFileReader.RequireDirectory(root, directory);
        string backupPath = Path.Combine(directory, "catalog.v1.backup.json");
        using JsonDocument backup = ReadDocument(root, backupPath, MaximumCatalogBytes, identity, out string sourceDigest);
        using JsonDocument integrity = ReadDocument(root, MigrationBackupIntegrity.ResolveManifestPath(backupPath), 4096, identity);
        using JsonDocument staged = ReadDocument(root, Path.Combine(directory, "catalog.v2.staged.json"), MaximumCatalogBytes,
            identity, out string targetDigest);
        using JsonDocument commit = ReadDocument(root, Path.Combine(directory, "commit.json"), 4096, identity);
        JsonElement marker = commit.RootElement;
        if (backup.RootElement.GetProperty("schemaVersion").GetInt32() != 1 ||
            staged.RootElement.GetProperty("schemaVersion").GetInt32() != 2 ||
            integrity.RootElement.GetProperty("formatVersion").GetInt32() != 1 ||
            integrity.RootElement.GetProperty("backupSha256").GetString() != sourceDigest ||
            marker.GetProperty("formatVersion").GetInt32() != 1 ||
            marker.GetProperty("state").GetString() != "PointerCommitted" ||
            marker.GetProperty("sourceSha256").GetString() != sourceDigest ||
            marker.GetProperty("targetSha256").GetString() != targetDigest ||
            marker.GetProperty("profileCount").GetInt32() != staged.RootElement.GetProperty("profiles").GetArrayLength() ||
            !marker.GetProperty("committedAtUtc").TryGetDateTimeOffset(out DateTimeOffset committed) ||
            committed.Offset != TimeSpan.Zero || committed == DateTimeOffset.MinValue) {
            throw new InvalidDataException("The database profile migration has no consistent terminal record.");
        }
    }

    private static JsonDocument ReadDocument(string root, string path, int maximumBytes, IncrementalHash identity) {
        return ReadDocument(root, path, maximumBytes, identity, out _);
    }

    private static JsonDocument ReadDocument(string root, string path, int maximumBytes, IncrementalHash identity,
        out string contentSha256) {
        string directory = ExistingPrivateFileReader.RequireDirectory(root, Path.GetDirectoryName(path)!);
        int count = 0;
        foreach (string entry in Directory.EnumerateFileSystemEntries(directory)) {
            if (++count > MaximumDirectoryEntries || DurableFileWriter.IsPendingPublicationFileName(
                Path.GetFileName(entry), Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException("Read-only profile inspection found unsupported or pending file publication.");
            }
        }
        byte[] bytes = ExistingPrivateFileReader.Read(root, path, maximumBytes);
        byte[] digest = SHA256.HashData(bytes);
        contentSha256 = Convert.ToHexStringLower(digest);
        identity.AppendData(digest);
        JsonDocument document = JsonDocument.Parse(bytes);
        try {
            RequireUniqueProperties(document.RootElement);
            return document;
        } catch {
            document.Dispose();
            throw;
        }
    }

    private static void RequireUniqueProperties(JsonElement element) {
        if (element.ValueKind == JsonValueKind.Object) {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in element.EnumerateObject()) {
                if (!names.Add(property.Name)) {
                    throw new InvalidDataException("Profile inspection refuses duplicate JSON properties.");
                }
                RequireUniqueProperties(property.Value);
            }
        } else if (element.ValueKind == JsonValueKind.Array) {
            foreach (JsonElement item in element.EnumerateArray()) {
                RequireUniqueProperties(item);
            }
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
