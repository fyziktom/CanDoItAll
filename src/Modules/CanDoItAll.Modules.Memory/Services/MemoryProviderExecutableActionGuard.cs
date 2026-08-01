using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderExecutableActionGuard(
    IMemoryProviderProfileStore providerStore,
    IMemoryOperationLedgerStore operationStore)
{
    public async Task EnsureProviderCanExecuteAsync(
        string? providerInstanceId,
        MemoryCapabilityId capability,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerInstanceId))
        {
            throw new InvalidOperationException("Select a provider before running this action.");
        }

        var providerId = MemoryProviderInstanceId.Parse(providerInstanceId);
        var provider = await providerStore.GetAsync(providerId, cancellationToken);
        if (provider is null)
        {
            throw new InvalidOperationException($"Memory provider '{providerId}' was not found.");
        }

        EnsureCanExecute(provider, capability);
    }

    public async Task EnsureOperationCanExecuteAsync(
        MemoryOperationId operationId,
        MemoryCapabilityId capability,
        CancellationToken cancellationToken)
    {
        var operation = await operationStore.GetAsync(operationId, cancellationToken)
            ?? throw new InvalidOperationException($"Memory operation '{operationId}' was not found.");
        await EnsureProviderCanExecuteAsync(
            operation.ProviderInstanceId.Value,
            capability,
            cancellationToken);
    }

    public static void RejectOperationCancellation()
    {
        throw new InvalidOperationException(
            "Operation cancellation is not executable by the currently shipped memory provider drivers.");
    }

    private static void EnsureCanExecute(
        MemoryProviderProfile provider,
        MemoryCapabilityId capability)
    {
        if (!provider.IsEnabled || provider.HealthState != MemoryProviderHealthState.Healthy)
        {
            throw new InvalidOperationException(
                $"Memory provider '{provider.InstanceId}' is not enabled and healthy.");
        }

        var claimsCapability = provider.Manifest.Capabilities.Any(item =>
            item.Supported && item.Id == capability);
        if (!claimsCapability || !MemoryProviderCapabilityPolicy.CanExecute(provider.DriverKind, capability))
        {
            throw new InvalidOperationException(
                $"Memory provider driver '{provider.DriverKind}' cannot execute capability '{capability}'.");
        }
    }
}
