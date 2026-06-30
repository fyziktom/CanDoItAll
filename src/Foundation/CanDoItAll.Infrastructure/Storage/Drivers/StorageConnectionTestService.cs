using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class StorageConnectionTestService(
    IStorageCatalogService catalogService,
    IStorageDriverRegistry driverRegistry,
    IStorageSecretResolver secretResolver,
    ILogger<StorageConnectionTestService> logger) : IStorageConnectionTestService
{
    public async Task<StorageConnectionTestResult> TestAsync(Guid storageId, CancellationToken cancellationToken = default)
    {
        var storage = await catalogService.GetAsync(storageId, cancellationToken);
        if (storage is null)
        {
            return new StorageConnectionTestResult(
                false,
                "The storage record was not found.",
                StorageHealthStatus.Unavailable,
                StorageCapability.None,
                DateTimeOffset.UtcNow);
        }

        var secretValue = await secretResolver.ResolveCredentialAsync(storage.CredentialSecretId, cancellationToken);

        try
        {
            var driver = driverRegistry.Resolve(storage.ProviderKind);
            var result = await driver.TestConnectionAsync(storage, secretValue, cancellationToken);

            storage.LastTestedAtUtc = result.TestedAtUtc;
            storage.HealthStatus = result.HealthStatus;
            storage.LastHealthMessage = result.Message;
            storage.CapabilityMask = result.CapabilityMask;
            await catalogService.SaveAsync(storage, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Storage connection test failed for {StorageId} ({ProviderKind}).", storage.Id, storage.ProviderKind);

            var failure = new StorageConnectionTestResult(
                false,
                $"Storage connection test failed: {ex.Message}",
                StorageHealthStatus.Unavailable,
                StorageCapability.None,
                DateTimeOffset.UtcNow);

            storage.LastTestedAtUtc = failure.TestedAtUtc;
            storage.HealthStatus = failure.HealthStatus;
            storage.LastHealthMessage = failure.Message;
            await catalogService.SaveAsync(storage, cancellationToken);

            return failure;
        }
    }
}
