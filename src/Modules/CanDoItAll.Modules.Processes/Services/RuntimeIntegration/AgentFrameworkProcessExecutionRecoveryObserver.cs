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
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;


internal sealed class AgentFrameworkProcessExecutionRecoveryObserver(
    AgentFrameworkProcessExecutionClaimRecoveryCoordinator recoveryCoordinator,
    IAgentFrameworkWorkspaceService workspaceService,
    ILogger<AgentFrameworkProcessExecutionRecoveryObserver> logger) : IAgentExecutionRecoveryObserver
{
    private const string RecoveryRequestedBy = "agent-execution-recovery";

    public async Task OnExecutionRecoveredAsync(
        AgentExecutionRecoveryObservation observation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (!AgentFrameworkProcessExecutionClaimRecoveryCoordinator.IsRecoverableExecutionFailure(observation.State, observation.Outcome) ||
            !Guid.TryParse(observation.ProcessRunId, out var processRunGuid) ||
            !Guid.TryParse(observation.ProcessStepId, out var processStepGuid))
        {
            return;
        }

        var recoveredExecutions = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                Take: 1,
                ProcessRunId: observation.ProcessRunId,
                ProcessStepId: observation.ProcessStepId)
            {
                ExecutionRunId = observation.ExecutionRunId
            },
            cancellationToken).ConfigureAwait(false);
        var recoveredExecution = recoveredExecutions.SingleOrDefault();
        if (recoveredExecution is null)
        {
            logger.LogWarning(
                "Skipping process claim release for recovered AgentFramework execution {ExecutionRunId} because the execution run record was not found. ProcessRunId={ProcessRunId} ProcessStepId={ProcessStepId}",
                observation.ExecutionRunId,
                observation.ProcessRunId,
                observation.ProcessStepId);
            return;
        }

        if (!ProcessDispatchClaimExecutionMetadata.TryRead(
                recoveredExecution,
                out var recoveredClaimIdentity))
        {
            logger.LogWarning(
                "Skipping process claim release for recovered AgentFramework execution {ExecutionRunId} because its durable dispatch claim identity is missing or malformed. ProcessRunId={ProcessRunId} ProcessStepId={ProcessStepId}",
                observation.ExecutionRunId,
                observation.ProcessRunId,
                observation.ProcessStepId);
            return;
        }

        var processStepExecutions = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                Take: int.MaxValue,
                ProcessRunId: observation.ProcessRunId,
                ProcessStepId: observation.ProcessStepId)
            {
                MetadataStringEquals = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessDispatchClaimExecutionMetadata.MetadataKey] =
                        recoveredClaimIdentity.Value.ToString("D")
                }
            },
            cancellationToken).ConfigureAwait(false);
        if (HasNewerActiveExecutionRun(processStepExecutions, recoveredExecution))
        {
            logger.LogInformation(
                "Skipping process claim release for recovered AgentFramework execution {ExecutionRunId} because a newer active execution exists for the same process step. ProcessRunId={ProcessRunId} ProcessStepId={ProcessStepId}",
                observation.ExecutionRunId,
                observation.ProcessRunId,
                observation.ProcessStepId);
            return;
        }

        await recoveryCoordinator.BlockRecoveredExecutionClaimAsync(
            recoveredExecution,
            new ProcessRunId(processRunGuid),
            new ProcessStepInstanceId(processStepGuid),
            RecoveryRequestedBy,
            cancellationToken).ConfigureAwait(false);
    }

    internal static bool HasNewerActiveExecutionRun(
        IReadOnlyList<ExecutionRunRecord> processStepExecutions,
        ExecutionRunRecord recoveredExecution)
    {
        ArgumentNullException.ThrowIfNull(processStepExecutions);
        ArgumentNullException.ThrowIfNull(recoveredExecution);

        if (!ProcessDispatchClaimExecutionMetadata.TryRead(
                recoveredExecution,
                out var recoveredClaimIdentity))
        {
            return false;
        }

        var recoveredCreatedAtUtc = NormalizeUtc(recoveredExecution.CreatedAtUtc);
        return processStepExecutions.Any(run =>
            run.Id != recoveredExecution.Id &&
            run.State is not ExecutionState.Completed and not ExecutionState.Failed &&
            NormalizeUtc(run.CreatedAtUtc) > recoveredCreatedAtUtc &&
            ProcessDispatchClaimExecutionMetadata.Matches(run, recoveredClaimIdentity) &&
            string.Equals(run.ProcessRunId, recoveredExecution.ProcessRunId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(run.ProcessStepId, recoveredExecution.ProcessStepId, StringComparison.OrdinalIgnoreCase));
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
        => value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();
}

