using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessProviderHealthProbeCoordinator(
    IProcessAutomationExecutionClient executionClient,
    ILogger logger,
    TimeSpan probeTimeout)
{
    public async Task<ProcessRunAutomationDispatchService.ProviderFallbackResolution?> ResolveHealthyFallbackProviderAsync(
        IReadOnlyList<ProviderProfile> providers,
        Guid failedProviderId,
        CancellationToken cancellationToken)
    {
        foreach (var provider in ProcessProviderFallbackSelectionRules.OrderFallbackProviders(providers, failedProviderId))
        {
            ProviderHealthResult healthResult;
            try
            {
                using var probeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                probeCancellation.CancelAfter(probeTimeout);
                healthResult = await executionClient.TestProviderAsync(provider.Id, probeCancellation.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation(
                    "Skipping fallback provider '{ProviderName}' because its health probe exceeded {TimeoutSeconds} seconds.",
                    provider.Name,
                    probeTimeout.TotalSeconds);
                continue;
            }
            catch (Exception exception)
            {
                logger.LogInformation(
                    exception,
                    "Fallback provider probe for '{ProviderName}' failed while evaluating process execution recovery.",
                    provider.Name);
                continue;
            }

            if (!healthResult.Success)
            {
                logger.LogInformation(
                    "Skipping fallback provider '{ProviderName}' because its health probe failed: {Summary}",
                    provider.Name,
                    healthResult.Summary);
                continue;
            }

            return new ProcessRunAutomationDispatchService.ProviderFallbackResolution(
                provider,
                ProcessProviderFallbackSelectionRules.ResolveFallbackProviderModel(provider, healthResult),
                healthResult.Summary);
        }

        return null;
    }
}
