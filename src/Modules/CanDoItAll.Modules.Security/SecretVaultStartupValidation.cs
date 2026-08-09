using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Modules.Security;

public interface ISecretVaultCapabilityState
{
    SecretVaultProbeResult? Current { get; }
}

public sealed class SecretVaultCapabilityState : ISecretVaultCapabilityState
{
    private SecretVaultProbeResult? current;

    public SecretVaultProbeResult? Current => Volatile.Read(ref current);

    internal void Set(SecretVaultProbeResult result) => Volatile.Write(ref current, result);
}

public sealed class SecretVaultStartupValidator(
    ISecretVault secretVault,
    SecretVaultCapabilityState capabilityState) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (secretVault is not ISecretVaultCapability capability)
        {
            var result = new SecretVaultProbeResult(
                SecretVaultProviderKind.Auto,
                SecretVaultAvailability.InvalidConfiguration,
                "The registered secret vault must expose an explicit startup capability probe.");
            capabilityState.Set(result);
            throw new SecretVaultUnavailableException(result);
        }

        SecretVaultProbeResult probe = await capability.ProbeAsync(cancellationToken).ConfigureAwait(false);
        capabilityState.Set(probe);
        if (!probe.IsAvailable)
        {
            throw new SecretVaultUnavailableException(probe);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
