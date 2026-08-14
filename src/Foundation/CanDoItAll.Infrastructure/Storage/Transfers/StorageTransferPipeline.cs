using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class StorageTransferPipeline(
    IStorageCatalogService catalogService,
    IStorageDriverRegistry driverRegistry,
    IStorageSecretResolver secretResolver,
    ILogger<StorageTransferPipeline> logger) : IStorageTransferPipeline
{
    public async Task<StorageTransferResult> ExecuteAsync(
        StorageTransferManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.Items.Count == 0)
        {
            return new StorageTransferResult(0, 0, 0, []);
        }

        var sourceStorage = manifest.SourceStorage
            ?? await ResolveStorageAsync(manifest.SourceStorageId, "source", cancellationToken);
        var targetStorage = manifest.TargetStorage
            ?? await ResolveStorageAsync(manifest.TargetStorageId, "target", cancellationToken);
        var sourceDriver = driverRegistry.Resolve(sourceStorage.ProviderKind);
        var targetDriver = driverRegistry.Resolve(targetStorage.ProviderKind);
        var options = NormalizeOptions(manifest.Options);

        var capabilityError = ValidateCapabilities(sourceDriver, targetDriver, options);
        if (capabilityError is not null)
        {
            var failures = manifest.Items
                .Select(item => new StorageTransferItemResult(item.SourcePath, item.TargetPath, false, capabilityError))
                .ToList();
            return new StorageTransferResult(failures.Count, 0, failures.Count, failures);
        }

        _ = await secretResolver.ResolveCredentialAsync(sourceStorage.CredentialSecretId, cancellationToken);
        _ = await secretResolver.ResolveCredentialAsync(targetStorage.CredentialSecretId, cancellationToken);

        var results = new ConcurrentBag<StorageTransferItemResult>();
        var completedCount = 0;
        var successCount = 0;
        var failureCount = 0;
        await Parallel.ForEachAsync(
            manifest.Items,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = options.MaxConcurrency
            },
            async (item, token) =>
            {
                StorageTransferItemResult result;
                try
                {
                    result = await TransferItemAsync(
                        item,
                        sourceStorage,
                        targetStorage,
                        sourceDriver,
                        targetDriver,
                        options,
                        token);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Storage transfer item failed from {SourcePath} to {TargetPath}. Source={SourceProvider}. Target={TargetProvider}.",
                        item.SourcePath,
                        item.TargetPath,
                        sourceStorage.ProviderKind,
                        targetStorage.ProviderKind);

                    result = new StorageTransferItemResult(
                        item.SourcePath,
                        item.TargetPath,
                        false,
                        ex.Message);
                }

                results.Add(result);

                var completed = Interlocked.Increment(ref completedCount);
                var succeeded = result.IsSuccess
                    ? Interlocked.Increment(ref successCount)
                    : successCount;
                var failed = result.IsSuccess
                    ? failureCount
                    : Interlocked.Increment(ref failureCount);

                if (options.ProgressCallback is not null)
                {
                    await options.ProgressCallback(
                        new StorageTransferProgress(
                            manifest.Items.Count,
                            completed,
                            succeeded,
                            failed,
                            result),
                        token);
                }
            });

        var orderedResults = results
            .OrderBy(item => item.SourcePath, StringComparer.Ordinal)
            .ThenBy(item => item.TargetPath, StringComparer.Ordinal)
            .ToList();

        return new StorageTransferResult(
            orderedResults.Count,
            orderedResults.Count(item => item.IsSuccess),
            orderedResults.Count(item => !item.IsSuccess),
            orderedResults);
    }

    private async Task<StorageTransferItemResult> TransferItemAsync(
        StorageTransferItem item,
        StorageCatalogRecord sourceStorage,
        StorageCatalogRecord targetStorage,
        IStorageDriver sourceDriver,
        IStorageDriver targetDriver,
        StorageTransferOptions options,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            try
            {
                return await TransferItemCoreAsync(
                    item,
                    sourceStorage,
                    targetStorage,
                    sourceDriver,
                    targetDriver,
                    options,
                    cancellationToken);
            }
            catch (Exception ex) when (attempt < options.MaxAttempts)
            {
                lastException = ex;

                var shouldRetry = options.RetryCallback is null ||
                    await options.RetryCallback(
                        new StorageTransferRetryContext(item, attempt, ex),
                        cancellationToken);
                if (!shouldRetry)
                {
                    break;
                }

                logger.LogWarning(
                    ex,
                    "Retrying storage transfer item {SourcePath} -> {TargetPath}. Attempt {Attempt} of {MaxAttempts}.",
                    item.SourcePath,
                    item.TargetPath,
                    attempt + 1,
                    options.MaxAttempts);
            }
        }

        throw lastException ?? new InvalidOperationException("The storage transfer failed without an exception.");
    }

    private async Task<StorageTransferItemResult> TransferItemCoreAsync(
        StorageTransferItem item,
        StorageCatalogRecord sourceStorage,
        StorageCatalogRecord targetStorage,
        IStorageDriver sourceDriver,
        IStorageDriver targetDriver,
        StorageTransferOptions options,
        CancellationToken cancellationToken)
    {
        var sourceReference = new StorageObjectReference(
            sourceStorage.Id,
            sourceStorage.ProviderKind,
            ResolveLocatorKind(sourceStorage.ProviderKind),
            item.SourcePath,
            Path.GetFileName(item.SourcePath),
            string.IsNullOrWhiteSpace(item.ContentType) ? "application/octet-stream" : item.ContentType);

        await using var sourceStream = await sourceDriver.OpenReadAsync(sourceStorage, sourceReference, cancellationToken);
        await using var sourceBuffer = new MemoryStream();
        await sourceStream.CopyToAsync(sourceBuffer, cancellationToken);
        var sourceBytes = sourceBuffer.ToArray();

        var writeResult = await targetDriver.SaveAsync(
            targetStorage,
            new StorageWriteRequest(
                Path.GetFileName(item.TargetPath),
                item.ContentType,
                sourceBytes,
                item.UsagePurpose,
                item.ContentKind,
                RelativePathHint: item.TargetPath),
            cancellationToken);

        var verificationMessage = await VerifyTransferAsync(
            item,
            targetStorage,
            targetDriver,
            writeResult.Reference,
            sourceBytes,
            options,
            cancellationToken);

        return new StorageTransferItemResult(
            item.SourcePath,
            item.TargetPath,
            true,
            string.IsNullOrWhiteSpace(verificationMessage)
                ? "Transferred successfully."
                : verificationMessage,
            writeResult.Reference);
    }

    private async Task<string> VerifyTransferAsync(
        StorageTransferItem item,
        StorageCatalogRecord targetStorage,
        IStorageDriver targetDriver,
        StorageObjectReference reference,
        byte[] sourceBytes,
        StorageTransferOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.VerifyTargetContent && options.VerificationCallback is null)
        {
            return string.Empty;
        }

        await using var targetStream = await targetDriver.OpenReadAsync(targetStorage, reference, cancellationToken);
        await using var targetBuffer = new MemoryStream();
        await targetStream.CopyToAsync(targetBuffer, cancellationToken);
        var targetBytes = targetBuffer.ToArray();

        var sourceHash = ComputeSha256(sourceBytes);
        var targetHash = ComputeSha256(targetBytes);
        if (options.VerifyTargetContent &&
            (sourceBytes.LongLength != targetBytes.LongLength || !string.Equals(sourceHash, targetHash, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The transferred content did not match the source payload.");
        }

        if (options.VerificationCallback is null)
        {
            return "Transferred successfully and verified.";
        }

        var verificationResult = await options.VerificationCallback(
            new StorageTransferVerificationContext(
                item,
                reference,
                sourceHash,
                targetHash,
                sourceBytes.LongLength,
                targetBytes.LongLength),
            cancellationToken);
        if (!verificationResult.IsSuccess)
        {
            throw new InvalidOperationException(verificationResult.Message);
        }

        return string.IsNullOrWhiteSpace(verificationResult.Message)
            ? "Transferred successfully and verified."
            : verificationResult.Message;
    }

    private async Task<StorageCatalogRecord> ResolveStorageAsync(
        Guid? storageId,
        string role,
        CancellationToken cancellationToken)
    {
        if (!storageId.HasValue)
        {
            throw new InvalidOperationException($"The transfer {role} storage was not supplied.");
        }

        return await catalogService.GetAsync(storageId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"The transfer {role} storage '{storageId.Value}' was not found.");
    }

    private static string? ValidateCapabilities(
        IStorageDriver sourceDriver,
        IStorageDriver targetDriver,
        StorageTransferOptions options)
    {
        if (!sourceDriver.SupportedCapabilities.HasFlag(StorageCapability.Read))
        {
            return $"Provider '{sourceDriver.ProviderKind}' does not support source reads required for batch transfer.";
        }

        if (!targetDriver.SupportedCapabilities.HasFlag(StorageCapability.Write))
        {
            return $"Provider '{targetDriver.ProviderKind}' does not support target writes required for batch transfer.";
        }

        if (!sourceDriver.SupportedCapabilities.HasFlag(StorageCapability.BatchTransfer) ||
            !targetDriver.SupportedCapabilities.HasFlag(StorageCapability.BatchTransfer))
        {
            return $"Provider pair '{sourceDriver.ProviderKind}' -> '{targetDriver.ProviderKind}' is not flagged for batch transfer.";
        }

        if ((options.VerifyTargetContent || options.VerificationCallback is not null) &&
            !targetDriver.SupportedCapabilities.HasFlag(StorageCapability.Read))
        {
            return $"Provider '{targetDriver.ProviderKind}' does not support target reads required for transfer verification.";
        }

        return null;
    }

    private static StorageTransferOptions NormalizeOptions(StorageTransferOptions? options)
    {
        return options is null
            ? new StorageTransferOptions()
            : options with
            {
                MaxConcurrency = Math.Max(1, options.MaxConcurrency),
                MaxAttempts = Math.Max(1, options.MaxAttempts)
            };
    }

    private static string ComputeSha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static StorageLocatorKind ResolveLocatorKind(StorageProviderKind providerKind)
    {
        return providerKind switch
        {
            StorageProviderKind.FileSystem => StorageLocatorKind.RelativePath,
            StorageProviderKind.Ipfs => StorageLocatorKind.ContentAddress,
            StorageProviderKind.Ftp => StorageLocatorKind.RemotePath,
            _ => StorageLocatorKind.RelativePath
        };
    }
}
