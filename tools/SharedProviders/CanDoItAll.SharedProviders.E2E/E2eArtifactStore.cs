using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure;
using CanDoItAll.Modules.Workspace;

namespace CanDoItAll.SharedProviders.E2E;

internal sealed class E2eArtifactStore
{
    private const string HandoffDirectoryName = "handoff";
    private const string CredentialsDirectoryName = "credentials";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    private readonly E2eOptions options;
    private readonly DurableFileWriter durableFileWriter;

    public E2eArtifactStore(
        E2eOptions options,
        DurableFileWriter durableFileWriter)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.durableFileWriter = durableFileWriter
            ?? throw new ArgumentNullException(nameof(durableFileWriter));
        Directory.CreateDirectory(options.ArtifactRootPath);
        durableFileWriter.EnsureDirectory(
            options.ArtifactRootPath,
            ResolveDirectory(HandoffDirectoryName),
            requirePrivateUnixMode: false);
        durableFileWriter.EnsureDirectory(
            options.ArtifactRootPath,
            ResolveDirectory(CredentialsDirectoryName),
            requirePrivateUnixMode: true);
    }

    public Task WriteSnapshotAsync(
        E2eStateSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var targetPath = Path.Combine(
            ResolveDirectory(HandoffDirectoryName),
            $"{E2eCommandLine.ToToken(snapshot.Role)}-state.json");
        return durableFileWriter.WriteTextAsync(
            options.ArtifactRootPath,
            targetPath,
            JsonSerializer.Serialize(snapshot, JsonOptions),
            DurableFileWriteOptions.Default,
            cancellationToken);
    }

    public Task WriteSnapshotCheckpointAsync(
        E2eStateSnapshot snapshot,
        string checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(checkpoint) ||
            checkpoint.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
        {
            throw new ArgumentException("A snapshot checkpoint must be a safe token.", nameof(checkpoint));
        }

        var targetPath = Path.Combine(
            ResolveDirectory(HandoffDirectoryName),
            $"{E2eCommandLine.ToToken(snapshot.Role)}-{checkpoint}-state.json");
        return durableFileWriter.WriteTextAsync(
            options.ArtifactRootPath,
            targetPath,
            JsonSerializer.Serialize(snapshot, JsonOptions),
            DurableFileWriteOptions.Default,
            cancellationToken);
    }

    public Task WriteCredentialAsync(
        string fileName,
        string value,
        CancellationToken cancellationToken)
    {
        ValidateCredentialFileName(fileName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new E2eSafeException("A generated E2E credential was empty.");
        }

        return durableFileWriter.WriteTextAsync(
            options.ArtifactRootPath,
            Path.Combine(ResolveDirectory(CredentialsDirectoryName), fileName),
            value,
            DurableFileWriteOptions.Private,
            cancellationToken);
    }

    public Task WriteSyncOutcomeAsync(
        E2eRole role,
        SharedProviderSourceOperationOutcome outcome,
        CancellationToken cancellationToken)
    {
        var artifact = new E2eSyncOutcome(
            SchemaVersion: 1,
            role,
            outcome,
            DateTimeOffset.UtcNow);
        return durableFileWriter.WriteTextAsync(
            options.ArtifactRootPath,
            Path.Combine(
                ResolveDirectory(HandoffDirectoryName),
                $"{E2eCommandLine.ToToken(role)}-sync-outcome.json"),
            JsonSerializer.Serialize(artifact, JsonOptions),
            DurableFileWriteOptions.Default,
            cancellationToken);
    }

    public async Task<string> ReadCredentialAsync(
        string fileName,
        CancellationToken cancellationToken)
    {
        ValidateCredentialFileName(fileName);
        var path = Path.Combine(ResolveDirectory(CredentialsDirectoryName), fileName);
        if (!File.Exists(path))
        {
            throw new E2eSafeException("A required generated E2E credential file does not exist.");
        }

        durableFileWriter.HardenPrivateFile(options.ArtifactRootPath, path);
        return await E2eSecretFile.ReadRequiredAsync(path, "generated E2E credential", cancellationToken);
    }

    public async Task<E2eStateSnapshot> ReadCentralSnapshotAsync(
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(
            ResolveDirectory(HandoffDirectoryName),
            $"{E2eCommandLine.ToToken(E2eRole.Central)}-state.json");
        if (!File.Exists(path))
        {
            throw new E2eSafeException("The central sanitized handoff snapshot does not exist.");
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var snapshot = await JsonSerializer.DeserializeAsync<E2eStateSnapshot>(
                stream,
                JsonOptions,
                cancellationToken);
            if (snapshot is null || snapshot.Role != E2eRole.Central)
            {
                throw new E2eSafeException("The central sanitized handoff snapshot is invalid.");
            }

            return snapshot;
        }
        catch (JsonException exception)
        {
            throw new E2eSafeException("The central sanitized handoff snapshot is invalid.", exception);
        }
    }

    private string ResolveDirectory(string name)
        => Path.Combine(options.ArtifactRootPath, name);

    private static void ValidateCredentialFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName != Path.GetFileName(fileName) ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The credential file name is invalid.", nameof(fileName));
        }
    }
}

internal sealed record E2eSyncOutcome(
    int SchemaVersion,
    E2eRole Role,
    SharedProviderSourceOperationOutcome Outcome,
    DateTimeOffset CompletedAtUtc);
