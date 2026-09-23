using System.Text.Json;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class FileApiUserStore(IControlPlanePathResolver paths, DurableFileWriter writer) : IApiUserStore {
    private const int SchemaVersion = 1;
    private const int MaximumAccounts = 10000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ApiUserRecord>> ReadAsync(CancellationToken cancellationToken = default) {
        var root = ResolveRoot();
        try {
            var json = await File.ReadAllTextAsync(StorePath(root), cancellationToken).ConfigureAwait(false);
            var document = JsonSerializer.Deserialize<ApiUserStoreDocument>(json, JsonOptions);
            if (document is null || document.SchemaVersion != SchemaVersion || document.Users is null) {
                throw new InvalidDataException("The API account store format is unsupported.");
            }
            Validate(document.Users);
            return document.Users;
        } catch (FileNotFoundException) {
            return [];
        }
    }

    public async Task SaveAsync(ApiUserRecord user, long? expectedVersion, CancellationToken cancellationToken = default) {
        var root = ResolveRoot();
        await using var coordination = await AcquireAsync(root, cancellationToken).ConfigureAwait(false);
        var users = (await ReadAsync(cancellationToken).ConfigureAwait(false)).ToList();
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
        if (index < 0) {
            users.Add(user);
        } else {
            users[index] = user;
        }
        await WriteAsync(root, users, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) {
        var root = ResolveRoot();
        await using var coordination = await AcquireAsync(root, cancellationToken).ConfigureAwait(false);
        var users = (await ReadAsync(cancellationToken).ConfigureAwait(false)).ToList();
        var current = users.Find(user => user.Id == id) ?? throw new KeyNotFoundException("The account no longer exists.");
        if (current.Version != expectedVersion) {
            throw new ApiUserConflictException("The account changed. Refresh before retrying.");
        }
        users.Remove(current);
        await WriteAsync(root, users, cancellationToken).ConfigureAwait(false);
    }

    private Task WriteAsync(string root, IReadOnlyList<ApiUserRecord> users, CancellationToken cancellationToken) {
        Validate(users);
        return writer.WriteTextAsync(root, StorePath(root),
            JsonSerializer.Serialize(new ApiUserStoreDocument(SchemaVersion, users), JsonOptions),
            DurableFileWriteOptions.Private, cancellationToken);
    }

    private ValueTask<IAsyncDisposable> AcquireAsync(string root, CancellationToken cancellationToken) =>
        writer.AcquireCoordinationAsync(root, StorePath(root) + ".mutation.lock", TimeSpan.FromSeconds(15),
            requirePrivateUnixMode: true, cancellationToken);

    private string ResolveRoot() {
        var root = paths.ResolveRootPath();
        var directory = Path.Combine(root, "api-users");
        writer.EnsureDirectory(root, directory, requirePrivateUnixMode: true);
        return directory;
    }

    private static string StorePath(string root) => Path.Combine(root, "accounts.json");

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
}

internal sealed record ApiUserStoreDocument(int SchemaVersion, IReadOnlyList<ApiUserRecord> Users);
