using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Infrastructure;

internal static class MigrationBackupIntegrity
{
    private const int CurrentFormatVersion = 1;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private static readonly DurableFileWriteOptions CreateNewPrivateOptions = new()
    {
        CommitMode = DurableFileCommitMode.CreateNew,
        RequirePrivateUnixMode = true
    };

    public static string CreateOrVerify(
        DurableFileWriter writer,
        string authorityRoot,
        string backupPath,
        string sourceJson)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        ArgumentNullException.ThrowIfNull(sourceJson);

        if (!File.Exists(backupPath))
        {
            TryCreate(writer, authorityRoot, backupPath, sourceJson);
        }

        string backupJson = File.ReadAllText(backupPath);
        string backupSha256 = ComputeSha256(backupJson);
        if (!string.Equals(backupSha256, ComputeSha256(sourceJson), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The existing migration backup does not match the current source document.");
        }

        EnsureIntegrityManifest(writer, authorityRoot, backupPath, backupSha256);
        VerifyIntegrityManifest(backupPath, backupSha256);
        return backupJson;
    }

    public static async Task<string> CreateOrVerifyAsync(
        DurableFileWriter writer,
        string authorityRoot,
        string backupPath,
        string sourceJson,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        ArgumentNullException.ThrowIfNull(sourceJson);

        if (!File.Exists(backupPath))
        {
            await TryCreateAsync(
                writer,
                authorityRoot,
                backupPath,
                sourceJson,
                cancellationToken).ConfigureAwait(false);
        }

        string backupJson = await File.ReadAllTextAsync(backupPath, cancellationToken).ConfigureAwait(false);
        string backupSha256 = ComputeSha256(backupJson);
        if (!string.Equals(backupSha256, ComputeSha256(sourceJson), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The existing migration backup does not match the current source document.");
        }

        await EnsureIntegrityManifestAsync(
            writer,
            authorityRoot,
            backupPath,
            backupSha256,
            cancellationToken).ConfigureAwait(false);
        await VerifyIntegrityManifestAsync(backupPath, backupSha256, cancellationToken).ConfigureAwait(false);
        return backupJson;
    }

    public static string ReadVerified(string backupPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        if (!File.Exists(backupPath))
        {
            throw new InvalidOperationException("The migration backup is missing.");
        }

        string backupJson = File.ReadAllText(backupPath);
        VerifyIntegrityManifest(backupPath, ComputeSha256(backupJson));
        return backupJson;
    }

    public static async Task<string> ReadVerifiedAsync(
        string backupPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        if (!File.Exists(backupPath))
        {
            throw new InvalidOperationException("The migration backup is missing.");
        }

        string backupJson = await File.ReadAllTextAsync(backupPath, cancellationToken).ConfigureAwait(false);
        await VerifyIntegrityManifestAsync(
            backupPath,
            ComputeSha256(backupJson),
            cancellationToken).ConfigureAwait(false);
        return backupJson;
    }

    public static string ComputeSha256(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    public static string ResolveManifestPath(string backupPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        return backupPath + ".integrity.json";
    }

    private static void TryCreate(
        DurableFileWriter writer,
        string authorityRoot,
        string path,
        string content)
    {
        try
        {
            writer.WriteText(authorityRoot, path, content, CreateNewPrivateOptions);
        }
        catch (IOException) when (File.Exists(path))
        {
        }
    }

    private static async Task TryCreateAsync(
        DurableFileWriter writer,
        string authorityRoot,
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        try
        {
            await writer.WriteTextAsync(
                authorityRoot,
                path,
                content,
                CreateNewPrivateOptions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (IOException) when (File.Exists(path))
        {
        }
    }

    private static void EnsureIntegrityManifest(
        DurableFileWriter writer,
        string authorityRoot,
        string backupPath,
        string backupSha256)
    {
        string manifestPath = ResolveManifestPath(backupPath);
        if (File.Exists(manifestPath))
        {
            return;
        }

        string manifestJson = JsonSerializer.Serialize(
            new MigrationBackupIntegrityManifest
            {
                BackupSha256 = backupSha256
            },
            SerializerOptions);
        TryCreate(writer, authorityRoot, manifestPath, manifestJson);
    }

    private static async Task EnsureIntegrityManifestAsync(
        DurableFileWriter writer,
        string authorityRoot,
        string backupPath,
        string backupSha256,
        CancellationToken cancellationToken)
    {
        string manifestPath = ResolveManifestPath(backupPath);
        if (File.Exists(manifestPath))
        {
            return;
        }

        string manifestJson = JsonSerializer.Serialize(
            new MigrationBackupIntegrityManifest
            {
                BackupSha256 = backupSha256
            },
            SerializerOptions);
        await TryCreateAsync(
            writer,
            authorityRoot,
            manifestPath,
            manifestJson,
            cancellationToken).ConfigureAwait(false);
    }

    private static void VerifyIntegrityManifest(string backupPath, string backupSha256)
    {
        string manifestPath = ResolveManifestPath(backupPath);
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException("The migration backup integrity manifest is missing.");
        }

        VerifyIntegrityManifestJson(File.ReadAllText(manifestPath), backupSha256);
    }

    private static async Task VerifyIntegrityManifestAsync(
        string backupPath,
        string backupSha256,
        CancellationToken cancellationToken)
    {
        string manifestPath = ResolveManifestPath(backupPath);
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException("The migration backup integrity manifest is missing.");
        }

        string manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        VerifyIntegrityManifestJson(manifestJson, backupSha256);
    }

    private static void VerifyIntegrityManifestJson(string manifestJson, string backupSha256)
    {
        MigrationBackupIntegrityManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<MigrationBackupIntegrityManifest>(
                manifestJson,
                SerializerOptions)
                ?? throw new InvalidOperationException("The migration backup integrity manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The migration backup integrity manifest is invalid.",
                exception);
        }

        if (manifest.FormatVersion != CurrentFormatVersion ||
            !string.Equals(manifest.BackupSha256, backupSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The migration backup checksum is invalid.");
        }
    }

    private sealed class MigrationBackupIntegrityManifest
    {
        public int FormatVersion { get; set; } = CurrentFormatVersion;

        public string BackupSha256 { get; set; } = string.Empty;
    }
}
