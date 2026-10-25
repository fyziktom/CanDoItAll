using System.Text.Json;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class FileApiUserStore(IControlPlanePathResolver paths, DurableFileWriter writer,
    IPhysicalFileSystemPathPolicyFactory pathPolicies) : IApiUserStore {
    private const int SchemaVersion = 2;
    private const int LegacySchemaVersion = 1;
    private const int MaximumAccounts = 10000;
    private const string DirectoryName = "api-users";
    private const string FileName = "accounts.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private ApiAuthorityFileRead? currentRead;

    public async Task<IReadOnlyList<ApiUserRecord>> ReadAsync(CancellationToken cancellationToken = default) {
        var document = await ReadDocumentAsync(ResolveRoot(), cancellationToken).ConfigureAwait(false);
        return Array.AsReadOnly(document.Users.Select(ApiUserStoreSnapshot.Freeze).ToArray());
    }

    public async Task EnsureAuthorityFormatAsync(CancellationToken cancellationToken = default) {
        Volatile.Write(ref currentRead, null);
        cancellationToken.ThrowIfCancellationRequested();
        var location = ApiAuthorityFileRead.Prepare(paths, writer, pathPolicies, DirectoryName);
        await using (var coordination = await AcquireAsync(location.DirectoryPath, cancellationToken).ConfigureAwait(false)) {
            var document = await ReadDocumentAsync(location.DirectoryPath, cancellationToken).ConfigureAwait(false);
            if (document.Generation is null) {
                await WriteAsync(location.DirectoryPath, document.Users, NewGeneration(), cancellationToken).ConfigureAwait(false);
            }
            writer.HardenPrivateFile(location.DirectoryPath, StorePath(location.DirectoryPath));
        }
        Volatile.Write(ref currentRead, location);
    }

    public async Task<ApiUserStoreSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        var location = Volatile.Read(ref currentRead) ?? throw new ApiAuthorityReadException(ApiAuthorityReadFailure.NotReady);
        try {
            var bytes = await location.ReadAsync(FileName, ApiAuthorityFileRead.MaximumAccountBytes, cancellationToken).ConfigureAwait(false);
            var document = Decode(bytes);
            return new(document.Generation ?? throw new ApiAuthorityReadException(ApiAuthorityReadFailure.InvalidFormat), document.Users);
        } catch (Exception exception) when (ApiAuthorityFileRead.Classify(exception) is { }) {
            throw new ApiAuthorityReadException(ApiAuthorityFileRead.Classify(exception)!.Value);
        }
    }

    public async ValueTask<ApiUserAuthorityObservation> ObserveAuthorityAsync(ApiUserAuthorityGeneration expected,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(expected);
        cancellationToken.ThrowIfCancellationRequested();
        var location = Volatile.Read(ref currentRead);
        if (location is null) {
            return new ApiUserAuthorityObservation.Unavailable(ApiAuthorityReadFailure.NotReady);
        }
        try {
            var bytes = await location.ReadAsync(FileName, ApiAuthorityFileRead.MaximumHeaderBytes, cancellationToken, prefixOnly: true).ConfigureAwait(false);
            return ReadGeneration(bytes.Span) == expected
                ? new ApiUserAuthorityObservation.Current()
                : new ApiUserAuthorityObservation.Changed();
        } catch (Exception exception) when (ApiAuthorityFileRead.Classify(exception) is { }) {
            return new ApiUserAuthorityObservation.Unavailable(ApiAuthorityFileRead.Classify(exception)!.Value);
        }
    }

    public async Task SaveAsync(ApiUserRecord user, long? expectedVersion, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(user);
        if (user.AllowedScopes is null) {
            throw new InvalidDataException("The API account store is invalid.");
        }
        user = ApiUserStoreSnapshot.Freeze(user);
        var root = ResolveRoot();
        await using var coordination = await AcquireAsync(root, cancellationToken).ConfigureAwait(false);
        var document = await ReadDocumentAsync(root, cancellationToken).ConfigureAwait(false);
        var users = document.Users.ToList();
        var index = users.FindIndex(candidate => candidate.Id == user.Id);
        if (expectedVersion is null) {
            if (index >= 0 || user.Version != 1 || user.AuthenticationRevision != 1) {
                throw new ApiUserConflictException("The account already exists.");
            }
        } else {
            if (index < 0) {
                throw new KeyNotFoundException("The account no longer exists.");
            }
            var current = users[index];
            if (current.Version != expectedVersion || user.Version != checked(current.Version + 1) ||
                user.AuthenticationRevision < current.AuthenticationRevision || user.CreatedAtUtc != current.CreatedAtUtc) {
                throw new ApiUserConflictException("The account changed. Refresh before retrying.");
            }
        }
        if (users.Any(candidate => candidate.Id != user.Id && candidate.NormalizedUserName == user.NormalizedUserName)) {
            throw new ApiUserConflictException("The username is already in use.");
        }
        var generation = document.Generation is null || index < 0 || !SameAuthority(users[index], user)
            ? NewGeneration() : document.Generation;
        if (index < 0) {
            users.Add(user);
        } else {
            users[index] = user;
        }
        await WriteAsync(root, users, generation, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) {
        var root = ResolveRoot();
        await using var coordination = await AcquireAsync(root, cancellationToken).ConfigureAwait(false);
        var users = (await ReadDocumentAsync(root, cancellationToken).ConfigureAwait(false)).Users.ToList();
        var current = users.Find(user => user.Id == id) ?? throw new KeyNotFoundException("The account no longer exists.");
        if (current.Version != expectedVersion) {
            throw new ApiUserConflictException("The account changed. Refresh before retrying.");
        }
        users.Remove(current);
        await WriteAsync(root, users, NewGeneration(), cancellationToken).ConfigureAwait(false);
    }

    private static bool SameAuthority(ApiUserRecord first, ApiUserRecord second) =>
        first.Id == second.Id && first.UserName == second.UserName && first.NormalizedUserName == second.NormalizedUserName &&
        first.Enabled == second.Enabled && first.AuthenticationRevision == second.AuthenticationRevision && first.PasswordHash == second.PasswordHash &&
        first.AllowedScopes.Order(StringComparer.Ordinal).SequenceEqual(second.AllowedScopes.Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private static ApiUserAuthorityGeneration NewGeneration() => new(Guid.NewGuid());

    private async Task WriteAsync(string root, IReadOnlyList<ApiUserRecord> users, ApiUserAuthorityGeneration generation,
        CancellationToken cancellationToken) {
        Validate(users);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new AccountDocument(SchemaVersion, generation.Value, users), JsonOptions);
        if (bytes.Length > ApiAuthorityFileRead.MaximumAccountBytes) {
            throw new ApiAuthorityReadException(ApiAuthorityReadFailure.SizeLimit);
        }
        await writer.WriteBytesAsync(root, StorePath(root), bytes, DurableFileWriteOptions.Private, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<DecodedDocument> ReadDocumentAsync(string root, CancellationToken cancellationToken) {
        try {
            var bytes = await ApiAuthorityFileRead.ReadFileAsync(StorePath(root), ApiAuthorityFileRead.MaximumAccountBytes, cancellationToken).ConfigureAwait(false);
            return Decode(bytes);
        } catch (FileNotFoundException) {
            return new(null, []);
        }
    }

    private static DecodedDocument Decode(ReadOnlyMemory<byte> bytes) {
        try {
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object) {
                throw InvalidFormat();
            }
            var schemaElement = RequireLegacyProperty(document.RootElement, "schemaVersion");
            var version = schemaElement.Deserialize<int>(JsonOptions);
            if (version == LegacySchemaVersion) {
                RequireUserCount(RequireLegacyProperty(document.RootElement, "users"));
                var legacy = document.RootElement.Deserialize<LegacyAccountDocument>(JsonOptions) ?? throw InvalidFormat();
                if (legacy.Users is null) {
                    throw InvalidFormat();
                }
                Validate(legacy.Users);
                return new(null, legacy.Users);
            }
            var properties = document.RootElement.EnumerateObject();
            if (!properties.MoveNext() || properties.Current.Name != "schemaVersion" ||
                properties.Current.Value.ValueKind != JsonValueKind.Number || !properties.Current.Value.TryGetInt32(out var schema) ||
                schema != SchemaVersion) {
                throw InvalidFormat();
            }
            if (!properties.MoveNext() || properties.Current.Name != "authorityGeneration" ||
                properties.Current.Value.ValueKind != JsonValueKind.String || !properties.Current.Value.TryGetGuid(out var id) || id == Guid.Empty) {
                throw InvalidFormat();
            }
            if (!properties.MoveNext() || properties.Current.Name != "users" || properties.Current.Value.ValueKind != JsonValueKind.Array) {
                throw InvalidFormat();
            }
            var usersElement = properties.Current.Value;
            RequireUserCount(usersElement);
            if (properties.MoveNext()) {
                throw InvalidFormat();
            }
            var users = usersElement.Deserialize<ApiUserRecord[]>(JsonOptions) ?? throw InvalidFormat();
            Validate(users);
            return new(new(id), users);
        } catch (JsonException) {
            throw InvalidFormat();
        }
    }

    private static JsonElement RequireLegacyProperty(JsonElement document, string name) {
        JsonElement? value = null;
        foreach (var property in document.EnumerateObject()) {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) {
                if (value.HasValue) {
                    throw InvalidFormat();
                }
                value = property.Value;
            }
        }
        return value ?? throw InvalidFormat();
    }

    private static void RequireUserCount(JsonElement users) {
        if (users.ValueKind != JsonValueKind.Array) {
            throw InvalidFormat();
        }
        if (users.GetArrayLength() > MaximumAccounts) {
            throw new ApiAuthorityReadException(ApiAuthorityReadFailure.SizeLimit);
        }
    }

    private static ApiUserAuthorityGeneration ReadGeneration(ReadOnlySpan<byte> bytes) {
        var reader = new Utf8JsonReader(bytes, isFinalBlock: false, state: default);
        ReadToken(ref reader, JsonTokenType.StartObject, bytes.Length);
        ReadProperty(ref reader, "schemaVersion"u8, bytes.Length);
        ReadToken(ref reader, JsonTokenType.Number, bytes.Length);
        if (!reader.TryGetInt32(out var schema) || schema != SchemaVersion) {
            throw InvalidFormat();
        }
        ReadProperty(ref reader, "authorityGeneration"u8, bytes.Length);
        ReadToken(ref reader, JsonTokenType.String, bytes.Length);
        if (!reader.TryGetGuid(out var generation) || generation == Guid.Empty) {
            throw InvalidFormat();
        }
        ReadProperty(ref reader, "users"u8, bytes.Length);
        ReadToken(ref reader, JsonTokenType.StartArray, bytes.Length);
        return new(generation);
    }

    private static void ReadProperty(ref Utf8JsonReader reader, ReadOnlySpan<byte> name, int length) {
        ReadToken(ref reader, JsonTokenType.PropertyName, length);
        if (!reader.ValueTextEquals(name)) {
            throw InvalidFormat();
        }
    }

    private static void ReadToken(ref Utf8JsonReader reader, JsonTokenType token, int length) {
        if (!reader.Read()) {
            throw new ApiAuthorityReadException(length == ApiAuthorityFileRead.MaximumHeaderBytes
                ? ApiAuthorityReadFailure.SizeLimit : ApiAuthorityReadFailure.InvalidFormat);
        }
        if (reader.TokenType != token) {
            throw InvalidFormat();
        }
    }

    private static InvalidDataException InvalidFormat() => new("The API account store format is unsupported or invalid.");

    private ValueTask<IAsyncDisposable> AcquireAsync(string root, CancellationToken cancellationToken) =>
        writer.AcquireCoordinationAsync(root, StorePath(root) + ".mutation.lock", TimeSpan.FromSeconds(15),
            requirePrivateUnixMode: true, cancellationToken);

    private string ResolveRoot() {
        var root = paths.ResolveRootPath();
        var directory = Path.Combine(root, DirectoryName);
        writer.EnsureDirectory(root, directory, requirePrivateUnixMode: true);
        return directory;
    }

    private static string StorePath(string root) => Path.Combine(root, FileName);

    private static void Validate(IReadOnlyList<ApiUserRecord> users) {
        if (users.Count > MaximumAccounts || users.Any(user => user is null || user.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(user.UserName) || user.NormalizedUserName != user.UserName.ToUpperInvariant() ||
            string.IsNullOrWhiteSpace(user.DisplayName) || string.IsNullOrWhiteSpace(user.PasswordHash) ||
            user.AllowedScopes is null || user.AllowedScopes.Any(string.IsNullOrWhiteSpace) ||
            user.Version < 1 || user.AuthenticationRevision < 1 || user.UpdatedAtUtc < user.CreatedAtUtc) ||
            users.Select(user => user.Id).Distinct().Count() != users.Count ||
            users.Select(user => user.NormalizedUserName).Distinct(StringComparer.Ordinal).Count() != users.Count) {
            throw new InvalidDataException("The API account store is invalid.");
        }
    }

    private sealed record DecodedDocument(ApiUserAuthorityGeneration? Generation, IReadOnlyList<ApiUserRecord> Users);
    private sealed record LegacyAccountDocument(int SchemaVersion, IReadOnlyList<ApiUserRecord> Users);
    private sealed record AccountDocument(int SchemaVersion, Guid AuthorityGeneration, IReadOnlyList<ApiUserRecord> Users);
}
