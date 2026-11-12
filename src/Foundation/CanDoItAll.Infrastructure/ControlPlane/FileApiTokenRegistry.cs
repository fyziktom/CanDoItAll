using System.Text.Json;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class FileApiTokenRegistry(
    IControlPlanePathResolver paths,
    DurableFileWriter writer,
    IPhysicalFileSystemPathPolicyFactory pathPolicies) : IApiTokenRegistry {
    private const int SchemaVersion = 1;
    private const string DirectoryName = "api-tokens";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DurableFileWriteOptions CreateOptions = DurableFileWriteOptions.Private with {
        CommitMode = DurableFileCommitMode.CreateNew
    };
    private ApiAuthorityFileRead? currentRead;

    public Task PrepareCurrentReadAsync(CancellationToken cancellationToken = default) {
        Volatile.Write(ref currentRead, null);
        cancellationToken.ThrowIfCancellationRequested();
        var location = ApiAuthorityFileRead.Prepare(paths, writer, pathPolicies, DirectoryName);
        Volatile.Write(ref currentRead, location);
        return Task.CompletedTask;
    }

    public async ValueTask<ApiTokenCurrentObservation> ObserveCurrentAsync(Guid id, CancellationToken cancellationToken = default) {
        if (id == Guid.Empty) {
            throw new ArgumentException("A token ID is required.", nameof(id));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var location = Volatile.Read(ref currentRead);
        if (location is null) {
            return new ApiTokenCurrentObservation.Unavailable(ApiAuthorityReadFailure.NotReady);
        }
        try {
            try {
                var bytes = await location.ReadAsync($"{id:N}.json", ApiAuthorityFileRead.MaximumTokenBytes, cancellationToken).ConfigureAwait(false);
                return new ApiTokenCurrentObservation.Found(Decode(bytes, id));
            } catch (FileNotFoundException) {
                location.ValidateDirectory();
                return new ApiTokenCurrentObservation.Absent();
            }
        } catch (Exception exception) when (ApiAuthorityFileRead.Classify(exception) is { }) {
            return new ApiTokenCurrentObservation.Unavailable(ApiAuthorityFileRead.Classify(exception)!.Value);
        }
    }

    public void Register(ApiTokenRecord token) {
        ArgumentNullException.ThrowIfNull(token);
        if (token.Scopes is null) {
            throw new InvalidDataException("The API token registry record is invalid.");
        }
        token = token with { Scopes = Array.AsReadOnly(token.Scopes.ToArray()) };
        Validate(token, token.Id);
        var root = ResolveRoot();
        writer.WriteBytes(root, TokenPath(root, token.Id), Encode(token), CreateOptions);
    }

    public async Task<ApiTokenRecord?> FindAsync(Guid id, CancellationToken cancellationToken = default) {
        var path = TokenPath(ResolveRoot(), id);
        try {
            var bytes = await ApiAuthorityFileRead.ReadFileAsync(path, ApiAuthorityFileRead.MaximumTokenBytes, cancellationToken).ConfigureAwait(false);
            return Decode(bytes, id);
        } catch (FileNotFoundException) {
            return null;
        }
    }

    public async Task<ApiTokenPage> SearchAsync(ApiTokenQuery query, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(query.Offset);
        if (query.PageSize is < 1 or > 100) {
            throw new ArgumentOutOfRangeException(nameof(query), "Page size must be between 1 and 100.");
        }

        var search = query.Search?.Trim() ?? string.Empty;
        var matches = new List<ApiTokenRecord>();
        foreach (var path in Directory.EnumerateFiles(ResolveRoot(), "*.json")) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var id)) {
                throw new InvalidDataException("The API token registry contains an invalid record name.");
            }
            var token = await FindAsync(id, cancellationToken).ConfigureAwait(false);
            if (token is not null && (query.Kind is null || token.Kind == query.Kind) && Matches(token, search)) {
                matches.Add(token);
            }
        }

        return new ApiTokenPage(matches.OrderByDescending(token => token.IssuedAtUtc)
            .ThenBy(token => token.Id).Skip(query.Offset).Take(query.PageSize).Select(ApiTokenSummary.FromRecord).ToArray(), matches.Count);
    }

    public async Task RevokeAsync(Guid id, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken = default) {
        var root = ResolveRoot();
        await using var coordination = await AcquireMutationAsync(root, id, cancellationToken).ConfigureAwait(false);
        var token = await FindAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The token no longer exists. Refresh the list.");
        if (token.RevokedAtUtc.HasValue) {
            return;
        }
        await writer.WriteBytesAsync(root, TokenPath(root, id), Encode(token with { RevokedAtUtc = revokedAtUtc }),
            DurableFileWriteOptions.Private, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) {
        var root = ResolveRoot();
        await using var coordination = await AcquireMutationAsync(root, id, cancellationToken).ConfigureAwait(false);
        await writer.DeleteAsync(root, TokenPath(root, id), DurableFileWriteOptions.Private, cancellationToken)
            .ConfigureAwait(false);
    }

    private ValueTask<IAsyncDisposable> AcquireMutationAsync(string root, Guid id, CancellationToken cancellationToken)
        => writer.AcquireCoordinationAsync(root, TokenPath(root, id) + ".mutation.lock",
            TimeSpan.FromSeconds(15), requirePrivateUnixMode: true, cancellationToken);

    private string ResolveRoot() {
        var root = paths.ResolveRootPath();
        var directory = Path.Combine(root, DirectoryName);
        writer.EnsureDirectory(root, directory, requirePrivateUnixMode: true);
        return directory;
    }

    private static string TokenPath(string root, Guid id) {
        if (id == Guid.Empty) {
            throw new ArgumentException("A token ID is required.", nameof(id));
        }
        return Path.Combine(root, $"{id:N}.json");
    }

    private static byte[] Encode(ApiTokenRecord token) {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new TokenDocument(SchemaVersion, token), JsonOptions);
        if (bytes.Length > ApiAuthorityFileRead.MaximumTokenBytes) {
            throw new ApiAuthorityReadException(ApiAuthorityReadFailure.SizeLimit);
        }
        return bytes;
    }

    private static ApiTokenRecord Decode(ReadOnlyMemory<byte> bytes, Guid id) {
        try {
            var document = JsonSerializer.Deserialize<TokenDocument>(bytes.Span, JsonOptions);
            if (document is null || document.SchemaVersion != SchemaVersion || document.Token is null) {
                throw new InvalidDataException("The API token registry record has an unsupported or missing format.");
            }
            Validate(document.Token, id);
            return document.Token with { Scopes = Array.AsReadOnly(document.Token.Scopes.ToArray()) };
        } catch (JsonException) {
            throw new InvalidDataException("The API token registry record is invalid.");
        }
    }

    private static void Validate(ApiTokenRecord token, Guid expectedId) {
        ArgumentNullException.ThrowIfNull(token);
        if (token.Id == Guid.Empty || token.Id != expectedId ||
            string.IsNullOrWhiteSpace(token.Subject) || string.IsNullOrWhiteSpace(token.DisplayName) ||
            token.Scopes is null || token.Scopes.Count == 0 || token.Scopes.Any(string.IsNullOrWhiteSpace) ||
            token.ExpiresAtUtc <= token.IssuedAtUtc || !Enum.IsDefined(token.Kind) ||
            (token.Kind == ApiCredentialKind.Machine && (token.UserId is not null || token.AuthenticationRevision is not null || token.AdministratorCredentialBinding is not null)) ||
            (token.Kind == ApiCredentialKind.UserSession && (token.UserId is null || token.UserId == Guid.Empty || token.AuthenticationRevision is null or < 1 || token.AdministratorCredentialBinding is not null)) ||
            (token.Kind == ApiCredentialKind.AdministratorSession && (token.UserId is not null || token.AuthenticationRevision is not null || string.IsNullOrWhiteSpace(token.AdministratorCredentialBinding)))) {
            throw new InvalidDataException("The API token registry record is invalid.");
        }
    }

    private static bool Matches(ApiTokenRecord token, string search) => search.Length == 0 ||
        token.Subject.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        token.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        token.Id.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
        token.Id.ToString("N").Contains(search, StringComparison.OrdinalIgnoreCase) ||
        token.Scopes.Any(scope => scope.Contains(search, StringComparison.OrdinalIgnoreCase));

    private sealed record TokenDocument(int SchemaVersion, ApiTokenRecord Token);
}
