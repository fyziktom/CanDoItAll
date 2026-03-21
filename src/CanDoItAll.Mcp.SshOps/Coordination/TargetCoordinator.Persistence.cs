namespace CanDoItAll.Mcp.SshOps.Coordination;

public sealed partial class TargetCoordinator
{
    private string GetBackupMetadataDirectory(string targetName)
    {
        return Path.Combine(runtimeConfiguration.StateDirectory, "backups", targetName);
    }

    private string GetRevisionMetadataDirectory(string targetName)
    {
        return Path.Combine(runtimeConfiguration.StateDirectory, "revisions", targetName);
    }

    private string GetOperationMetadataDirectory(string targetName)
    {
        return Path.Combine(runtimeConfiguration.StateDirectory, "operations", targetName);
    }

    private void SaveBackupMetadata(BackupMetadata metadata)
    {
        Directory.CreateDirectory(GetBackupMetadataDirectory(metadata.TargetName));
        var path = Path.Combine(GetBackupMetadataDirectory(metadata.TargetName), $"{metadata.BackupId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(metadata, JsonOptions));
    }

    private BackupMetadata? LoadBackupMetadata(string targetName, string backupId)
    {
        var path = Path.Combine(GetBackupMetadataDirectory(targetName), $"{backupId}.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<BackupMetadata>(File.ReadAllText(path), JsonOptions)
            : null;
    }

    private void SaveRevisionManifest(RevisionManifestMetadata manifest)
    {
        Directory.CreateDirectory(GetRevisionMetadataDirectory(manifest.TargetName));
        var path = Path.Combine(GetRevisionMetadataDirectory(manifest.TargetName), $"{manifest.RevisionId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));
    }

    private RevisionManifestMetadata? LoadLatestRevisionManifest(string targetName, string stackName)
    {
        var directory = GetRevisionMetadataDirectory(targetName);
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => JsonSerializer.Deserialize<RevisionManifestMetadata>(File.ReadAllText(path), JsonOptions))
            .Where(static manifest => manifest is not null)
            .Cast<RevisionManifestMetadata>()
            .Where(manifest => string.Equals(manifest.StackName, stackName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(static manifest => manifest.CreatedAtUtc)
            .FirstOrDefault();
    }

    private void SaveOperationMetadata(OperationTrackingMetadata metadata)
    {
        Directory.CreateDirectory(GetOperationMetadataDirectory(metadata.TargetName));
        var path = Path.Combine(GetOperationMetadataDirectory(metadata.TargetName), $"{metadata.OperationId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(metadata, JsonOptions));
    }

    private IReadOnlyList<OperationTrackingMetadata> LoadOperationMetadata(string targetName)
    {
        var directory = GetOperationMetadataDirectory(targetName);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => JsonSerializer.Deserialize<OperationTrackingMetadata>(File.ReadAllText(path), JsonOptions))
            .Where(static metadata => metadata is not null)
            .Cast<OperationTrackingMetadata>()
            .OrderByDescending(static metadata => metadata.CreatedAtUtc)
            .ToArray();
    }

    private sealed record BackupMetadata(
        string BackupId,
        string TargetName,
        string OriginalPath,
        string BackupPath,
        DateTimeOffset CreatedAtUtc,
        string? Label);

    private sealed record RevisionEntryMetadata(
        string Path,
        string BackupPath);

    private sealed record RevisionManifestMetadata(
        string RevisionId,
        string TargetName,
        string StackName,
        DateTimeOffset CreatedAtUtc,
        IReadOnlyList<RevisionEntryMetadata> Entries);

    private sealed record OperationTrackingMetadata(
        string OperationId,
        string TargetName,
        string ResourceKey,
        string Kind,
        DateTimeOffset CreatedAtUtc);
}
