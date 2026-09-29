using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryProviderExecutableActionGuard(
    MemoryUiProfileOrigin origin,
    IMemoryProviderProfileStore providerStore,
    IMemoryOperationLedgerStore operationStore)
{
    public async Task EnsureProviderCanExecuteAsync(
        string? providerInstanceId,
        MemoryCapabilityId capability,
        CancellationToken cancellationToken)
    {
        origin.RequireCurrent();
        if (string.IsNullOrWhiteSpace(providerInstanceId))
        {
            throw new MemoryActionRefusedException("Select a provider before running this action.");
        }

        var providerId = MemoryProviderInstanceId.Parse(providerInstanceId);
        MemoryProviderProfile? provider;
        try {
            provider = await providerStore.GetAsync(providerId, cancellationToken);
        } catch (Exception) {
            throw new MemoryActionRefusedException("The provider could not be verified. No provider action was dispatched.");
        }
        if (provider is null)
        {
            throw new MemoryActionRefusedException($"Memory provider '{providerId}' was not found.");
        }

        origin.RequireCurrent();
        EnsureCanExecute(provider, capability);
    }

    public async Task EnsureOperationCanExecuteAsync(
        MemoryOperationId operationId,
        MemoryCapabilityId capability,
        CancellationToken cancellationToken)
    {
        MemoryOperationRecord? operation;
        try {
            operation = await operationStore.GetAsync(operationId, cancellationToken);
        } catch (Exception) {
            throw new MemoryActionRefusedException("The operation could not be verified. No provider action was dispatched.");
        }
        if (operation is null) {
            throw new MemoryActionRefusedException($"Memory operation '{operationId}' was not found.");
        }
        await EnsureProviderCanExecuteAsync(
            operation.ProviderInstanceId.Value,
            capability,
            cancellationToken);
    }

    public static void RejectOperationCancellation()
    {
        throw new MemoryActionRefusedException(
            "Operation cancellation is not executable by the currently shipped memory provider drivers.");
    }

    private static void EnsureCanExecute(
        MemoryProviderProfile provider,
        MemoryCapabilityId capability)
    {
        if (!provider.IsEnabled || provider.HealthState != MemoryProviderHealthState.Healthy)
        {
            throw new MemoryActionRefusedException(
                $"Memory provider '{provider.InstanceId}' is not enabled and healthy.");
        }

        var claimsCapability = provider.Manifest.Capabilities.Any(item =>
            item.Supported && item.Id == capability);
        if (!claimsCapability || !MemoryProviderCapabilityPolicy.CanExecute(provider.DriverKind, capability))
        {
            throw new MemoryActionRefusedException(
                $"Memory provider driver '{provider.DriverKind}' cannot execute capability '{capability}'.");
        }
    }
}
