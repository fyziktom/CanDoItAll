using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Security.Abstractions;

namespace CanDoItAll.Modules.Security;

public sealed class StorageSecretResolver(ISecretRuntimeResolver secretResolver) : IStorageSecretResolver
{
    public async Task<string?> ResolveCredentialAsync(Guid? secretId, CancellationToken cancellationToken = default)
    {
        if (!secretId.HasValue)
        {
            return null;
        }

        return await secretResolver.ResolveValueAsync(
            new SecretRuntimeRequest(
                secretId.Value,
                SecretRuntimePurposes.StorageCredential,
                [secretId.Value],
                ConsumerType: SecretRuntimeConsumerTypes.StorageCredential,
                ConsumerId: SecretRuntimeConsumerIds.StorageRuntime()),
            cancellationToken);
    }
}
