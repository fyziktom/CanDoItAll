using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class FileUserUiPreferencesStore(IControlPlanePathResolver paths, DurableFileWriter writer,
    IPhysicalFileSystemPathPolicyFactory pathPolicies) : IUserUiPreferencesStore {
    private const int SchemaVersion = 1;
    private const int MaximumBytes = 4096;
    private const string DirectoryName = "user-ui-preferences";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<UserUiPreferences> ReadAsync(UserUiPreferencesAccount account, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(account);
        cancellationToken.ThrowIfCancellationRequested();
        var location = Resolve(account);
        return ReadAsync(location.Root, location.File, cancellationToken);
    }

    public async Task<UserUiPreferences> UpdateAsync(UserUiPreferencesAccount account, UserUiPreferencesPatch patch,
        Func<CancellationToken, Task> authorizeCommit, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(authorizeCommit);
        patch.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        var location = Resolve(account);
        await using var coordination = await writer.AcquireCoordinationAsync(location.Root,
            location.File + ".mutation.lock", TimeSpan.FromSeconds(15), requirePrivateUnixMode: true, cancellationToken);
        var current = await ReadAsync(location.Root, location.File, cancellationToken);
        var updated = current.Apply(patch);
        await authorizeCommit(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (updated != current) {
            var document = new PreferencesDocument(SchemaVersion, updated.Theme, updated.MainMenuCollapsed);
            await writer.WriteBytesAsync(location.Root, location.File, JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions),
                DurableFileWriteOptions.Private, cancellationToken);
        }
        return updated;
    }

    private (string Root, string File) Resolve(UserUiPreferencesAccount account) {
        var root = paths.ResolveRootPath();
        var directory = Path.Combine(root, DirectoryName);
        writer.EnsureDirectory(root, directory, requirePrivateUnixMode: true);
        var fileName = account.UserId is { } id ? $"user-{id:N}.json" : "configured-administrator.json";
        return (root, Path.Combine(directory, fileName));
    }

    private async Task<UserUiPreferences> ReadAsync(string root, string file, CancellationToken cancellationToken) {
        pathPolicies.Create(root).EnsureSafePath(file, allowMissingLeaf: true);
        try {
            await using var stream = new FileStream(file, new FileStreamOptions {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read | FileShare.Delete,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                BufferSize = MaximumBytes
            });
            ExistingPrivateFileReader.RequirePrivateFile(stream);
            var bytes = new byte[MaximumBytes + 1];
            var length = 0;
            while (length < bytes.Length) {
                var read = await stream.ReadAsync(bytes.AsMemory(length), cancellationToken);
                if (read == 0) {
                    break;
                }
                length += read;
            }
            if (length > MaximumBytes) {
                throw new InvalidDataException("The UI preference document exceeds its size limit.");
            }
            var document = JsonSerializer.Deserialize<PreferencesDocument>(bytes.AsSpan(0, length), JsonOptions);
            if (document is null || document.SchemaVersion != SchemaVersion || !Enum.IsDefined(document.Theme)) {
                throw new InvalidDataException("The UI preference document is unsupported or invalid.");
            }
            pathPolicies.Create(root).EnsureSafePath(file, allowMissingLeaf: true);
            return new(document.Theme, document.MainMenuCollapsed);
        } catch (FileNotFoundException) {
            return UserUiPreferences.Default;
        } catch (JsonException exception) {
            throw new InvalidDataException("The UI preference document is invalid.", exception);
        }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record PreferencesDocument(
        [property: JsonRequired] int SchemaVersion,
        [property: JsonRequired] UiThemeMode Theme,
        [property: JsonRequired] bool MainMenuCollapsed);
}
