using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDispatchCandidateHydrationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IWorkspacePathResolver workspacePathResolver,
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    IAiTechnicalAgentBridge technicalAgentBridge,
    IProcessAutomationExecutionClient executionClient,
    IClock clock,
    TimeSpan staleAutomationExecutionRunTimeout,
    ILogger<ProcessRunAutomationDispatchService> logger)
{
    public async Task<ProcessRouteCandidate?> LoadRouteCandidateAsync(
        Guid processRunId,
        Guid claimedStepRunId,
        string trigger,
        CancellationToken cancellationToken)
    {
        var candidate = await LoadAsync(
            processRunId,
            claimedStepRunId,
            trigger,
            cancellationToken);

        return candidate is null
            ? null
            : ProcessDispatchRouteModelAdapters.FromDispatcherCandidate(candidate);
    }

    public async Task<ProcessRunAutomationDispatchService.DispatchCandidate?> LoadAsync(
        Guid processRunId,
        Guid claimedStepRunId,
        string trigger,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var snapshot = await ProcessDispatchCandidateHydrationLoader.LoadAsync(
            dbContext,
            processRunId,
            claimedStepRunId,
            trigger,
            cancellationToken);
        if (snapshot is null)
        {
            return null;
        }

        var artifactInputPreparationService = new ProcessDispatchCandidateArtifactInputPreparationService(
            workspacePathResolver,
            databaseProfileRuntimeAccessor);
        var directAgentCandidateAssembler = new ProcessDispatchDirectAgentCandidateAssembler(
            technicalAgentBridge,
            executionClient,
            clock,
            staleAutomationExecutionRunTimeout,
            logger);
        var candidateAssembler = new ProcessDispatchHydratedCandidateAssembler(
            artifactInputPreparationService,
            directAgentCandidateAssembler);

        return await candidateAssembler.TryAssembleAsync(
            dbContext,
            snapshot,
            trigger,
            cancellationToken);
    }
}
