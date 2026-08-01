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


internal sealed class AgentFrameworkProcessExecutionObservationReader(
    IAgentReferenceDataProvider agentReferenceDataProvider,
    IAgentFrameworkWorkspaceService workspaceService) : IProcessExecutionObservationReader
{
    private const int ExecutionRunBatchTake = 5_000;

    public async ValueTask<IReadOnlyList<ProcessExecutionObservation>> ListAsync(
        ProcessExecutionObservationQuery query,
        CancellationToken cancellationToken = default)
        => (await ReadAsync(query, cancellationToken).ConfigureAwait(false)).Items;

    public async ValueTask<ProcessExecutionObservationReadResult> ReadAsync(
        ProcessExecutionObservationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.TakePerRun <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.TakePerRun,
                "Process execution observation take per run must be greater than zero.");
        }

        if (query.ToUtc < query.FromUtc)
        {
            throw new ArgumentException(
                "Process execution observation time range must end at or after it starts.",
                nameof(query));
        }

        if (query.RunIds.Count == 0)
        {
            return new ProcessExecutionObservationReadResult([], IsComplete: true);
        }

        var referenceData = await agentReferenceDataProvider
            .GetAsync(new AgentReferenceDataRequest(AgentReferenceDataSections.Agents), cancellationToken)
            .ConfigureAwait(false);
        var agentNameById = referenceData.Agents.ToDictionary(agent => agent.Id, agent => agent.Name);
        var agentAvatarById = referenceData.Agents.ToDictionary(agent => agent.Id, agent => agent.AvatarImageUrl ?? string.Empty);
        var requestedRunIds = query.RunIds.ToHashSet();
        var requestedStepIds = query.StepInstanceIds.ToHashSet();
        var loadDetails = query.DetailLevel switch
        {
            ProcessExecutionObservationDetailLevel.Summary => false,
            ProcessExecutionObservationDetailLevel.Full => true,
            _ => throw new ArgumentOutOfRangeException(
                nameof(query),
                query.DetailLevel,
                "The execution observation detail level is not supported.")
        };
        var executionRunRead = await ListExecutionRunsAsync(query, cancellationToken).ConfigureAwait(false);
        var executionRunSlice = SliceExecutionRuns(executionRunRead.Items, query);
        var observations = new List<ProcessExecutionObservation>();
        var isComplete = executionRunRead.IsComplete &&
            executionRunSlice.IsComplete;

        foreach (var executionRun in executionRunSlice.Items)
        {
            if (!TryParseProcessIdentity(executionRun, out var processRunId, out var stepInstanceId) ||
                !requestedRunIds.Contains(processRunId) ||
                requestedStepIds.Count > 0 && !requestedStepIds.Contains(stepInstanceId))
            {
                continue;
            }

            ExecutionRunDetail? detail = null;
            if (loadDetails)
            {
                try
                {
                    detail = await workspaceService
                        .GetExecutionRunDetailAsync(executionRun.Id, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    isComplete = false;
                }
            }

            var detailRun = detail?.Run ?? executionRun;
            var agentName = agentNameById.GetValueOrDefault(detailRun.AgentId);
            var agentAvatarImageUrl = agentAvatarById.GetValueOrDefault(detailRun.AgentId) ?? string.Empty;
            observations.Add(new ProcessExecutionObservation(
                detailRun.Id,
                processRunId,
                stepInstanceId,
                detailRun.AgentId,
                FirstNonEmpty(agentName, detailRun.RequestedBy, detailRun.AgentId.ToString("D")),
                detailRun.ProviderName,
                detailRun.Model,
                detailRun.State.ToString(),
                detailRun.Outcome?.ToString() ?? string.Empty,
                detailRun.CreatedAtUtc,
                detailRun.UpdatedAtUtc,
                detailRun.StartedAtUtc,
                detailRun.CompletedAtUtc,
                detailRun.InputSummary,
                detailRun.ResultSummary,
                MapActivities(detail),
                MapTools(detail),
                MapArtifacts(detail),
                ResolveLastError(detail))
            {
                AgentAvatarImageUrl = agentAvatarImageUrl
            });
        }

        return new ProcessExecutionObservationReadResult(
            observations
                .OrderByDescending(observation => observation.UpdatedAtUtc)
                .ToArray(),
            isComplete);
    }

    private async Task<ExecutionRunRead> ListExecutionRunsAsync(
        ProcessExecutionObservationQuery query,
        CancellationToken cancellationToken)
    {
        var requestedRunCount = query.RunIds
            .Distinct()
            .Count();
        var requestedStepCount = Math.Max(
            1,
            query.StepInstanceIds
                .Distinct()
                .Count());
        var requestedGroupCount = Math.Min(
            (long)Math.Max(1, requestedRunCount) * requestedStepCount,
            ExecutionRunBatchTake + 1L);
        var perGroupReadTake = Math.Min(
            (long)query.TakePerRun + 1,
            ExecutionRunBatchTake + 1L);
        var requestedTake = Math.Min(
            perGroupReadTake * requestedGroupCount,
            ExecutionRunBatchTake + 1L);
        var take = (int)Math.Clamp(
            requestedTake,
            1,
            ExecutionRunBatchTake + 1L);

        var executionRuns = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                Take: take,
                UpdatedFromUtc: query.FromUtc,
                UpdatedToUtc: query.ToUtc)
            {
                ProcessRunIds = query.RunIds
                    .Distinct()
                    .Select(runId => runId.ToString())
                    .ToArray(),
                ProcessStepIds = query.StepInstanceIds
                    .Distinct()
                    .Select(stepId => stepId.ToString())
                    .ToArray()
            },
            cancellationToken).ConfigureAwait(false);
        var isComplete = executionRuns.Count < take;
        return new ExecutionRunRead(
            executionRuns
                .Take(ExecutionRunBatchTake)
                .ToArray(),
            isComplete);
    }

    private static ExecutionRunRead SliceExecutionRuns(
        IReadOnlyList<ExecutionRunRecord> executionRuns,
        ProcessExecutionObservationQuery query)
    {
        var take = query.TakePerRun;
        var orderedRuns = executionRuns.OrderByDescending(run => run.UpdatedAtUtc);
        var groups = query.StepInstanceIds.Count == 0
            ? orderedRuns.GroupBy(
                run => run.ProcessRunId,
                StringComparer.OrdinalIgnoreCase)
            : orderedRuns.GroupBy(
                run => $"{run.ProcessRunId}\u001f{run.ProcessStepId}",
                StringComparer.OrdinalIgnoreCase);
        var detectionTake = (int)Math.Min(
            (long)take + 1,
            ExecutionRunBatchTake + 1L);
        var materializedGroups = groups
            .Select(group => group.Take(detectionTake).ToArray())
            .ToArray();
        return new ExecutionRunRead(
            materializedGroups
                .SelectMany(group => group.Take(take))
                .ToArray(),
            materializedGroups.All(group => group.Length <= take));
    }

    private sealed record ExecutionRunRead(
        IReadOnlyList<ExecutionRunRecord> Items,
        bool IsComplete);

    private static IReadOnlyList<ProcessExecutionActivityObservation> MapActivities(ExecutionRunDetail? detail)
        => detail?.ExecutionLog
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Take(8)
            .OrderBy(entry => entry.CreatedAtUtc)
            .Select(entry => new ProcessExecutionActivityObservation(
                entry.CreatedAtUtc,
                entry.State.ToString(),
                entry.Phase,
                entry.Message))
            .ToArray() ?? [];

    private static IReadOnlyList<ProcessExecutionToolObservation> MapTools(ExecutionRunDetail? detail)
        => detail?.ToolReceipts
            .OrderByDescending(tool => tool.CompletedAtUtc)
            .Take(8)
            .OrderBy(tool => tool.StartedAtUtc)
            .Select(tool => new ProcessExecutionToolObservation(
                tool.ToolName,
                tool.RuntimeToolProviderKey,
                tool.RequestSummary,
                tool.ExitSummary,
                tool.StartedAtUtc,
                tool.CompletedAtUtc))
            .ToArray() ?? [];

    private static IReadOnlyList<ProcessExecutionArtifactObservation> MapArtifacts(ExecutionRunDetail? detail)
        => detail?.Artifacts
            .OrderByDescending(artifact => artifact.CreatedAtUtc)
            .Take(8)
            .OrderBy(artifact => artifact.CreatedAtUtc)
            .Select(artifact => new ProcessExecutionArtifactObservation(
                artifact.ArtifactKind,
                artifact.DisplayName,
                artifact.RelativePath,
                artifact.Summary,
                artifact.CreatedAtUtc)
            {
                ProducedBy = artifact.ProducedBy
            })
            .ToArray() ?? [];

    private static string ResolveLastError(ExecutionRunDetail? detail)
    {
        if (detail is null)
        {
            return string.Empty;
        }

        return detail.ExecutionLog
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .FirstOrDefault(entry =>
                entry.State == ExecutionState.Failed ||
                entry.Phase.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                entry.Message.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                entry.Message.Contains("exception", StringComparison.OrdinalIgnoreCase))
            ?.Message ?? string.Empty;
    }

    private static bool TryParseProcessIdentity(
        ExecutionRunRecord executionRun,
        out ProcessRunId runId,
        out ProcessStepInstanceId stepInstanceId)
    {
        runId = default;
        stepInstanceId = default;

        return Guid.TryParse(executionRun.ProcessRunId, out var parsedRunId) &&
               parsedRunId != Guid.Empty &&
               Guid.TryParse(executionRun.ProcessStepId, out var parsedStepId) &&
               parsedStepId != Guid.Empty &&
               TryCreateProcessRunId(parsedRunId, out runId) &&
               TryCreateStepInstanceId(parsedStepId, out stepInstanceId);
    }

    private static bool TryCreateProcessRunId(Guid value, out ProcessRunId runId)
    {
        try
        {
            runId = new ProcessRunId(value);
            return true;
        }
        catch (ArgumentException)
        {
            runId = default;
            return false;
        }
    }

    private static bool TryCreateStepInstanceId(Guid value, out ProcessStepInstanceId stepInstanceId)
    {
        try
        {
            stepInstanceId = new ProcessStepInstanceId(value);
            return true;
        }
        catch (ArgumentException)
        {
            stepInstanceId = default;
            return false;
        }
    }

    private static string FirstNonEmpty(string? first, string? second, string? third)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first.Trim();
        }

        if (!string.IsNullOrWhiteSpace(second))
        {
            return second.Trim();
        }

        return string.IsNullOrWhiteSpace(third) ? string.Empty : third.Trim();
    }
}

