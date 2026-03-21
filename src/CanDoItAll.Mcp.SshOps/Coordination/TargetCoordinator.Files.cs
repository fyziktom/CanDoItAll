namespace CanDoItAll.Mcp.SshOps.Coordination;

public sealed partial class TargetCoordinator
{
    public async Task<SshOpsToolResult<FsApplyBundleData>> FsApplyBundleAsync(
        string correlationId,
        string targetName,
        RemoteFileBundleEntry[] bundle,
        CancellationToken cancellationToken)
    {
        if (bundle.Length == 0)
        {
            throw new ToolInvocationException("ValidationFailed", "At least one bundle item is required.");
        }

        var target = targetCatalog.GetRequired(targetName);
        await using var lease = await AcquireMutationLeaseAsync(target, "fs_apply_bundle", cancellationToken);
        await EnsureNoRunningOperationsAsync(target, cancellationToken);

        var totalBytes = bundle.Sum(item => GetContentBytes(item).Length);
        if (totalBytes > runtimeConfiguration.Options.Server.MaxBundleBytes)
        {
            throw new ToolInvocationException("ValidationFailed", $"Bundle size {totalBytes} bytes exceeds the configured server limit of {runtimeConfiguration.Options.Server.MaxBundleBytes} bytes.");
        }

        string? revisionId = null;
        var backupEntries = new List<RevisionEntryMetadata>();
        var written = 0;
        var normalizedPaths = new List<string>(bundle.Length);

        foreach (var item in bundle)
        {
            if (!string.Equals(item.Mode, "overwrite", StringComparison.OrdinalIgnoreCase))
            {
                throw new ToolInvocationException("ValidationFailed", $"Bundle entry mode '{item.Mode}' is not supported.");
            }

            var remotePath = pathGuard.EnsureAllowedPath(target, item.Path);
            normalizedPaths.Add(remotePath);
            var parentPath = GetParentPosixPath(remotePath)
                ?? throw new ToolInvocationException("PathNotAllowed", $"Path '{remotePath}' does not have a writable parent directory.");

            await transport.EnsureDirectoryAsync(target, parentPath, useSudo: false, cancellationToken);
            var stat = await transport.StatAsync(target, remotePath, cancellationToken);

            if (item.BackupBeforeWrite && stat.Exists)
            {
                revisionId ??= CorrelationIdFactory.Create("rev");
                var backupPath = pathGuard.ResolveInsideStateRoot(target, $"revisions/{revisionId}/{Guid.NewGuid():N}");
                await transport.EnsureDirectoryAsync(target, GetParentPosixPath(backupPath)!, useSudo: false, cancellationToken);
                await CopyRemotePathAsync(target, remotePath, backupPath, useSudo: false, cancellationToken);
                backupEntries.Add(new RevisionEntryMetadata(remotePath, backupPath));
            }

            var bytes = GetContentBytes(item);
            await transport.UploadBytesAsync(target, remotePath, bytes, ensureParentDirectory: true, cancellationToken);
            if (!string.IsNullOrWhiteSpace(item.Permissions))
            {
                var chmodResult = await transport.ExecuteAsync(
                    target,
                    ["chmod", item.Permissions!, remotePath],
                    new RemoteExecutionOptions(),
                    cancellationToken);
                EnsureSuccess(chmodResult, "ValidationFailed", $"Could not set permissions on '{remotePath}'.");
            }

            written++;
        }

        if (revisionId is not null)
        {
            var stackName = ResolveStackName(target, normalizedPaths);
            var manifest = new RevisionManifestMetadata(revisionId, target.Name, stackName, DateTimeOffset.UtcNow, backupEntries);
            SaveRevisionManifest(manifest);
            await UploadJsonAsync(target, pathGuard.ResolveInsideStateRoot(target, $"revisions/{revisionId}/manifest.json"), manifest, cancellationToken);
        }

        return Result(
            new FsApplyBundleData(written, backupEntries.Count, revisionId),
            target: target.Name,
            status: "success",
            summary: $"Wrote {written} file(s) to the target.",
            nextSuggestedTools: ["compose_validate"],
            warnings: backupEntries.Count > 0 ? [$"Created {backupEntries.Count} backup(s) before overwrite."] : null);
    }

    public async Task<SshOpsToolResult<FsReadTextData>> FsReadTextAsync(
        string correlationId,
        string targetName,
        string path,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        var remotePath = pathGuard.EnsureAllowedPath(target, path);
        var stat = await transport.StatAsync(target, remotePath, cancellationToken);
        if (!stat.Exists || stat.IsDirectory)
        {
            throw new ToolInvocationException("RemotePathMissing", $"Remote file '{remotePath}' was not found.", new { path = remotePath, target = target.Name });
        }

        var safeMaxBytes = Math.Clamp(maxBytes, 1, 1024 * 1024);
        var content = await transport.ReadTextAsync(target, remotePath, safeMaxBytes, cancellationToken);
        var truncated = stat.Size > safeMaxBytes;
        return Result(
            new FsReadTextData(remotePath, content, truncated),
            target: target.Name,
            status: "success",
            summary: $"Read remote file '{remotePath}'.");
    }

    public async Task<SshOpsToolResult<FsBackupPathData>> FsBackupPathAsync(
        string correlationId,
        string targetName,
        string path,
        string? label,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        await using var lease = await AcquireMutationLeaseAsync(target, "fs_backup_path", cancellationToken);
        await EnsureNoRunningOperationsAsync(target, cancellationToken);

        var backupMetadata = await CreateBackupAsync(target, path, label, cancellationToken);
        SaveBackupMetadata(backupMetadata);
        await UploadJsonAsync(target, pathGuard.ResolveInsideStateRoot(target, $"backups/{backupMetadata.BackupId}.json"), backupMetadata, cancellationToken);

        return Result(
            new FsBackupPathData(backupMetadata.BackupId, backupMetadata.BackupPath),
            target: target.Name,
            status: "success",
            summary: $"Created backup '{backupMetadata.BackupId}'.");
    }

    public async Task<SshOpsToolResult<FsRestoreBackupData>> FsRestoreBackupAsync(
        string correlationId,
        string targetName,
        string backupId,
        CancellationToken cancellationToken)
    {
        var target = targetCatalog.GetRequired(targetName);
        await using var lease = await AcquireMutationLeaseAsync(target, "fs_restore_backup", cancellationToken);
        await EnsureNoRunningOperationsAsync(target, cancellationToken);

        var metadata = LoadBackupMetadata(target.Name, backupId)
            ?? throw new ToolInvocationException("RollbackRevisionNotFound", $"Backup '{backupId}' was not found for target '{target.Name}'.");

        await RestoreCopiedPathAsync(target, metadata.BackupPath, metadata.OriginalPath, cancellationToken);

        return Result(
            new FsRestoreBackupData(backupId, metadata.OriginalPath),
            target: target.Name,
            status: "success",
            summary: $"Restored backup '{backupId}'.");
    }

    private async Task<BackupMetadata> CreateBackupAsync(
        ResolvedTargetConfiguration target,
        string path,
        string? label,
        CancellationToken cancellationToken)
    {
        var originalPath = pathGuard.EnsureAllowedPath(target, path);
        var stat = await transport.StatAsync(target, originalPath, cancellationToken);
        if (!stat.Exists)
        {
            throw new ToolInvocationException("RemotePathMissing", $"Remote path '{originalPath}' does not exist.");
        }

        var backupId = CorrelationIdFactory.Create("b");
        var backupPath = pathGuard.ResolveInsideStateRoot(target, $"backups/{backupId}");
        await transport.EnsureDirectoryAsync(target, GetParentPosixPath(backupPath)!, useSudo: false, cancellationToken);
        await CopyRemotePathAsync(target, originalPath, backupPath, useSudo: false, cancellationToken);

        return new BackupMetadata(backupId, target.Name, originalPath, backupPath, DateTimeOffset.UtcNow, label);
    }

    private async Task RestoreCopiedPathAsync(
        ResolvedTargetConfiguration target,
        string backupPath,
        string originalPath,
        CancellationToken cancellationToken)
    {
        var parent = GetParentPosixPath(originalPath) ?? "/";
        var result = await RunRemoteShellAsync(
            target,
            $"""
            mkdir -p {QuoteShell(parent)}
            rm -rf {QuoteShell(originalPath)}
            cp -a -- {QuoteShell(backupPath)} {QuoteShell(originalPath)}
            """,
            timeout: runtimeConfiguration.DefaultComposeApplyTimeout,
            cancellationToken: cancellationToken);
        EnsureSuccess(result, "ValidationFailed", $"Could not restore '{originalPath}' from backup.");
    }

    private async Task CopyRemotePathAsync(
        ResolvedTargetConfiguration target,
        string sourcePath,
        string destinationPath,
        bool useSudo,
        CancellationToken cancellationToken)
    {
        var result = await RunRemoteShellAsync(
            target,
            $"""
            mkdir -p {QuoteShell(GetParentPosixPath(destinationPath) ?? "/")}
            cp -a -- {QuoteShell(sourcePath)} {QuoteShell(destinationPath)}
            """,
            useSudo,
            runtimeConfiguration.DefaultComposeApplyTimeout,
            cancellationToken: cancellationToken);
        EnsureSuccess(result, "ValidationFailed", $"Could not copy remote path '{sourcePath}'.");
    }

    private async Task UploadJsonAsync<T>(
        ResolvedTargetConfiguration target,
        string remotePath,
        T value,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await transport.UploadBytesAsync(target, remotePath, Encoding.UTF8.GetBytes(json), ensureParentDirectory: true, cancellationToken);
    }
}
