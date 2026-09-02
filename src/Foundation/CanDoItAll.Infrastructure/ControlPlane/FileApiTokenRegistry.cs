using System.Text.Json;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class FileApiTokenRegistry(
    IControlPlanePathResolver paths,
    DurableFileWriter writer) : IApiTokenRegistry {
    private const int SchemaVersion = 1;
    private const string DirectoryName = "api-tokens";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DurableFileWriteOptions CreateOptions = DurableFileWriteOptions.Private with {
        CommitMode = DurableFileCommitMode.CreateNew
    };

    public void Register(ApiTokenRecord token) {
        Validate(token, token.Id);
        var root = ResolveRoot();
        writer.WriteText(root, TokenPath(root, token.Id),
            JsonSerializer.Serialize(new TokenDocument(SchemaVersion, token), JsonOptions), CreateOptions);
    }

    public async Task<ApiTokenRecord?> FindAsync(Guid id, CancellationToken cancellationToken = default) {
        var path = TokenPath(ResolveRoot(), id);
        try {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var document = JsonSerializer.Deserialize<TokenDocument>(json, JsonOptions);
            if (document is null || document.SchemaVersion != SchemaVersion || document.Token is null) {
                throw new InvalidDataException("The API token registry record has an unsupported or missing format.");
            }
            Validate(document.Token, id);
            return document.Token;
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

        var search = query.Search.Trim();
        var matches = new List<ApiTokenRecord>();
        foreach (var path in Directory.EnumerateFiles(ResolveRoot(), "*.json")) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var id)) {
                throw new InvalidDataException("The API token registry contains an invalid record name.");
            }
            var token = await FindAsync(id, cancellationToken).ConfigureAwait(false);
            if (token is not null && Matches(token, search)) {
                matches.Add(token);
            }
        }

        return new ApiTokenPage(matches.OrderByDescending(token => token.IssuedAtUtc)
            .ThenBy(token => token.Id).Skip(query.Offset).Take(query.PageSize).ToArray(), matches.Count);
    }

    public async Task RevokeAsync(Guid id, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken = default) {
        var root = ResolveRoot();
        await using var coordination = await AcquireMutationAsync(root, id, cancellationToken).ConfigureAwait(false);
        var token = await FindAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The token no longer exists. Refresh the list.");
        if (token.RevokedAtUtc.HasValue) {
            return;
        }
        await writer.WriteTextAsync(root, TokenPath(root, id),
            JsonSerializer.Serialize(new TokenDocument(SchemaVersion, token with { RevokedAtUtc = revokedAtUtc }), JsonOptions),
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

    private static void Validate(ApiTokenRecord token, Guid expectedId) {
        ArgumentNullException.ThrowIfNull(token);
        if (token.Id == Guid.Empty || token.Id != expectedId ||
            string.IsNullOrWhiteSpace(token.Subject) || string.IsNullOrWhiteSpace(token.DisplayName) ||
            token.Scopes is null || token.Scopes.Count == 0 || token.Scopes.Any(string.IsNullOrWhiteSpace) ||
            token.ExpiresAtUtc <= token.IssuedAtUtc) {
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
