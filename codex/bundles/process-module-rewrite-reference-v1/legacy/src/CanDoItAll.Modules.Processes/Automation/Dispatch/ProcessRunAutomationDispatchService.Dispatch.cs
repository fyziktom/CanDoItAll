using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    public async Task DispatchAsync(
        Guid processRunId,
        Guid? triggerStepRunId,
        string trigger,
        Func<CancellationToken, Task>? renewLeaseAsync = null,
        CancellationToken cancellationToken = default)
    {
        if (processRunId == Guid.Empty)
        {
            return;
        }

        var claimCoordinator = CreateDispatchClaimCoordinator();
        while (!cancellationToken.IsCancellationRequested)
        {
            var candidateHeaderLoadStarted = Stopwatch.GetTimestamp();
            var candidateHeaders = await LoadDispatchCandidateHeadersAsync(
                processRunId,
                triggerStepRunId,
                trigger,
                cancellationToken);
            logger.LogDebug(
                "Loaded {CandidateCount} dispatch candidate headers for process run {ProcessRunId} in {ElapsedMilliseconds} ms.",
                candidateHeaders.Count,
                processRunId,
                GetElapsedMilliseconds(candidateHeaderLoadStarted));
            if (candidateHeaders.Count == 0)
            {
                return;
            }

            foreach (var candidateHeader in candidateHeaders)
            {
                using var dispatchGuard = await ProcessDispatchGuardLease.WaitAsync(
                    candidateHeader.StepRunId,
                    StepDispatchGuards,
                    cancellationToken);
                var dispatchClaim = await TryClaimStepDispatchAsync(
                    claimCoordinator,
                    processRunId,
                    candidateHeader.StepRunId,
                    trigger,
                    triggerStepRunId,
                    cancellationToken);
                if (dispatchClaim is null)
                {
                    continue;
                }

                dispatchGuard.Release();

                var dispatchResult = await RunClaimedDispatchAsync(
                    claimCoordinator,
                    processRunId,
                    triggerStepRunId,
                    trigger,
                    dispatchClaim,
                    renewLeaseAsync,
                    cancellationToken);
                if (dispatchResult == ProcessClaimedDispatchResult.ContinueCandidates)
                {
                    continue;
                }

                return;
            }

            return;
        }
    }

    private static double GetElapsedMilliseconds(long startTimestamp)
        => Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

    internal async Task HandleWorkflowExecutionOutcomeAsync(
        DispatchCandidate candidate,
        ProcessWorkflowExecutionOutcome workflowOutcome,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await CreateFinalizerAdapter().FinalizeWorkflowCompletionAsync(
            new ProcessDispatchWorkflowFinalizerInput(
                ProcessDispatchRouteModelAdapters.FromDispatcherCandidate(candidate),
                workflowOutcome,
                ProcessDispatchRouteModelAdapters.FromDispatcherClaim(dispatchClaim)),
            cancellationToken);
    }

    internal sealed record ProcessStepDispatchClaim(Guid StepRunId, string ClaimToken);

    internal sealed record DispatchCandidateHeader(
        Guid StepRunId,
        ProcessStepRunStatus Status,
        ProcessStepKind StepKind);

    internal async Task<bool> IsRunClosedToAutomationAsync(
        Guid processRunId,
        Guid stepRunId,
        CancellationToken cancellationToken)
    {
        return await CreateRunClosureGuardService().IsRunClosedToAutomationAsync(
            processRunId,
            stepRunId,
            cancellationToken);
    }

    private ProcessMissingUpstreamArtifactMaterializationJournalCoordinator CreateMissingUpstreamArtifactMaterializationJournalCoordinator()
    {
        return new ProcessMissingUpstreamArtifactMaterializationJournalCoordinator(dbContextFactory, clock);
    }

    private ProcessDispatchPreExecutionGuardHandler CreatePreExecutionGuardHandler()
    {
        return new ProcessDispatchPreExecutionGuardHandler(
            new ProcessMissingUpstreamArtifactMaterializationCoordinator(
                CreateMissingUpstreamArtifactMaterializationJournalCoordinator(),
                serviceScopeFactory,
                logger));
    }

    private async Task<IReadOnlyList<DispatchCandidateHeader>> LoadDispatchCandidateHeadersAsync(
        Guid processRunId,
        Guid? targetStepRunId,
        string trigger,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await ProcessDispatchCandidateHeaderSelector.SelectAsync(
            dbContext,
            processRunId,
            targetStepRunId,
            trigger,
            clock.GetUtcNow(),
            cancellationToken);
    }

    internal static bool ApplyProjectStructureReadAccess(AgentEditorModel agentEditor, Guid projectId)
    {
        return ProcessDispatchTechnicalAgentBindingCoordinator.ApplyProjectStructureReadAccess(agentEditor, projectId);
    }

    private static async Task<string> LoadLatestManualRecoveryDirectiveAsync(
        AppDbContext dbContext,
        Guid runId,
        Guid stepRunId,
        DateTimeOffset? stepStartedAtUtc,
        CancellationToken cancellationToken)
    {
        return await ProcessDispatchRecoveryQueryHelper.LoadLatestManualRecoveryDirectiveAsync(
            dbContext,
            runId,
            stepRunId,
            stepStartedAtUtc,
            cancellationToken);
    }

}
