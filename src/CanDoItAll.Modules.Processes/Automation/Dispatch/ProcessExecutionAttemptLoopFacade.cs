using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessExecutionAttemptLoopFacade
{
    public ProcessExecutionAttemptLoopFacade(
        IProcessAutomationExecutionClient executionClient,
        IDbContextFactory<AppDbContext> dbContextFactory,
        IAiTechnicalAgentBridge technicalAgentBridge,
        ILogger logger,
        TimeSpan providerFallbackHealthProbeTimeout)
    {
        HistoricalCarriedProof = new ProcessHistoricalCarriedProofQueryCoordinator(executionClient);
        ProviderRepair = new ProcessProviderRepairCoordinator(
            executionClient,
            new ProcessProviderHealthProbeCoordinator(
                executionClient,
                logger,
                providerFallbackHealthProbeTimeout),
            new ProcessAssignedAgentProviderRepairCoordinator(
                dbContextFactory,
                technicalAgentBridge,
                executionClient,
                logger),
            logger);
    }

    public ProcessHistoricalCarriedProofQueryCoordinator HistoricalCarriedProof { get; }

    public ProcessProviderRepairCoordinator ProviderRepair { get; }
}
