using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;


internal sealed class AgentFrameworkProcessExecutionClaimRecoveryReconciler(
    ProcessPersistenceDbContext dbContext,
    IAgentFrameworkWorkspaceService workspaceService,
    AgentFrameworkProcessExecutionClaimRecoveryCoordinator recoveryCoordinator)
{
    private const int ExecutionTakePerClaim = 10;
    private const int MaxCandidateClaims = 250;
    private const string RecoveryRequestedBy = "agent-execution-reconciliation";
    private static readonly TimeSpan ExecutionCreationSkew = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TerminalExecutionRecoveryGrace = TimeSpan.FromSeconds(30);

    public async Task<int> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await LoadActiveClaimCandidatesAsync(dbContext, cancellationToken).ConfigureAwait(false);
        var observedAtUtc = DateTimeOffset.UtcNow;
        var recoveredCount = 0;
        foreach (var candidate in candidates)
        {
            var executionRuns = await LoadExecutionRunsForClaimAsync(candidate, cancellationToken).ConfigureAwait(false);
            var executionRun = SelectRecoverableExecution(executionRuns, candidate, observedAtUtc);
            if (executionRun is null)
            {
                continue;
            }

            var runId = new ProcessRunId(candidate.RunId);
            var stepInstanceId = new ProcessStepInstanceId(candidate.StepInstanceId);
            var recovered = AgentFrameworkProcessExecutionClaimRecoveryCoordinator.IsRecoverableExecutionCompletion(
                executionRun.State,
                executionRun.Outcome)
                ? await recoveryCoordinator.SubmitRecoveredExecutionResultAsync(
                    executionRun,
                    runId,
                    stepInstanceId,
                    RecoveryRequestedBy,
                    cancellationToken).ConfigureAwait(false)
                : await recoveryCoordinator.BlockRecoveredExecutionClaimAsync(
                    executionRun,
                    runId,
                    stepInstanceId,
                    RecoveryRequestedBy,
                    cancellationToken).ConfigureAwait(false);

            if (recovered)
            {
                recoveredCount++;
            }
        }

        return recoveredCount;
    }

    internal static async Task<IReadOnlyList<ActiveProcessClaimCandidate>> LoadActiveClaimCandidatesAsync(
        ProcessPersistenceDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        return await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state => state.Status == ProcessRuntimeStatus.Active)
            .Join(
                dbContext.RuntimeSteps.AsNoTracking(),
                state => state.RunId,
                step => step.RunId,
                (state, step) => new { state.RunId, Step = step })
            .Where(item =>
                item.Step.ActiveClaimToken != null &&
                (item.Step.Status == ProcessRuntimeStepStatus.Claimed ||
                 item.Step.Status == ProcessRuntimeStepStatus.Running))
            .Join(
                dbContext.DispatchClaims.AsNoTracking(),
                item => item.RunId,
                claim => claim.RunId,
                (item, claim) => new { item.RunId, item.Step, Claim = claim })
            .Where(item =>
                item.Step.ActiveClaimToken == item.Claim.ClaimToken &&
                (item.Claim.Status == DispatchClaimStatus.Claimed ||
                 item.Claim.Status == DispatchClaimStatus.LeaseRenewed ||
                 item.Claim.Status == DispatchClaimStatus.Reclaimed))
            .OrderBy(item => item.Claim.ExpiresAtUtc)
            .Select(item => new ActiveProcessClaimCandidate(
                item.RunId,
                item.Step.StepInstanceId,
                item.Claim.ClaimToken,
                item.Claim.OwnerId,
                item.Claim.CreatedAtUtc,
                item.Claim.ExpiresAtUtc))
            .Take(MaxCandidateClaims)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    internal static ExecutionRunRecord? SelectRecoverableExecution(
        IReadOnlyList<ExecutionRunRecord> executionRuns,
        ActiveProcessClaimCandidate candidate)
        => SelectRecoverableExecution(executionRuns, candidate, DateTimeOffset.UtcNow);

    internal static ExecutionRunRecord? SelectRecoverableExecution(
        IReadOnlyList<ExecutionRunRecord> executionRuns,
        ActiveProcessClaimCandidate candidate,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(executionRuns);

        var runId = candidate.RunId.ToString("D");
        var stepId = candidate.StepInstanceId.ToString("D");
        var earliestCreatedAtUtc = candidate.CreatedAtUtc - ExecutionCreationSkew;
        var latestExecution = executionRuns
            .Where(executionRun =>
                executionRun.CreatedAtUtc >= earliestCreatedAtUtc &&
                AgentFrameworkProcessExecutionClaimRecoveryCoordinator.IsExecutionBoundToClaim(
                    executionRun,
                    candidate.ClaimToken) &&
                AgentFrameworkProcessExecutionClaimRecoveryCoordinator.CanAssociateClaimWithRecoveredExecution(
                    candidate.CreatedAtUtc,
                    candidate.ExpiresAtUtc,
                    executionRun.CreatedAtUtc) &&
                string.Equals(executionRun.ProcessRunId, runId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(executionRun.ProcessStepId, stepId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(executionRun => executionRun.CreatedAtUtc)
            .ThenByDescending(executionRun => executionRun.UpdatedAtUtc)
            .FirstOrDefault();

        var terminalAtUtc = latestExecution?.CompletedAtUtc ?? latestExecution?.UpdatedAtUtc;
        return latestExecution is not null &&
               terminalAtUtc <= observedAtUtc - TerminalExecutionRecoveryGrace &&
               (AgentFrameworkProcessExecutionClaimRecoveryCoordinator.IsRecoverableExecutionFailure(
                    latestExecution.State,
                    latestExecution.Outcome) ||
                AgentFrameworkProcessExecutionClaimRecoveryCoordinator.IsRecoverableExecutionCompletion(
                    latestExecution.State,
                    latestExecution.Outcome))
            ? latestExecution
            : null;
    }

    private async Task<IReadOnlyList<ExecutionRunRecord>> LoadExecutionRunsForClaimAsync(
        ActiveProcessClaimCandidate candidate,
        CancellationToken cancellationToken)
    {
        return await workspaceService.ListExecutionRunsAsync(
            CreateExecutionRunQuery(candidate),
            cancellationToken).ConfigureAwait(false);
    }

    internal static ExecutionRunQuery CreateExecutionRunQuery(
        ActiveProcessClaimCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new ExecutionRunQuery(
            Take: ExecutionTakePerClaim,
            ProcessRunId: candidate.RunId.ToString("D"),
            ProcessStepId: candidate.StepInstanceId.ToString("D"),
            CreatedFromUtc: candidate.CreatedAtUtc - ExecutionCreationSkew)
        {
            MetadataStringEquals = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessDispatchClaimExecutionMetadata.MetadataKey] =
                    candidate.ClaimToken.ToString("D")
            }
        };
    }

    internal sealed record ActiveProcessClaimCandidate(
        Guid RunId,
        Guid StepInstanceId,
        Guid ClaimToken,
        string OwnerId,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset ExpiresAtUtc);

}
