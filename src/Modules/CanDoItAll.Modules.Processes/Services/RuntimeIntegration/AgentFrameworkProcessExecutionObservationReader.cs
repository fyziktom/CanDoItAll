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
    public async ValueTask<IReadOnlyList<ProcessExecutionObservation>> ListAsync(
        ProcessExecutionObservationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.RunIds.Count == 0)
        {
            return [];
        }

        var referenceData = await agentReferenceDataProvider
            .GetAsync(new AgentReferenceDataRequest(AgentReferenceDataSections.Agents), cancellationToken)
            .ConfigureAwait(false);
        var agentNameById = referenceData.Agents.ToDictionary(agent => agent.Id, agent => agent.Name);
        var agentAvatarById = referenceData.Agents.ToDictionary(agent => agent.Id, agent => agent.AvatarImageUrl ?? string.Empty);
        var requestedRunIds = query.RunIds.ToHashSet();
        var requestedStepIds = query.StepInstanceIds.ToHashSet();
        var executionRuns = await ListExecutionRunsAsync(query, cancellationToken).ConfigureAwait(false);
        var observations = new List<ProcessExecutionObservation>();

        foreach (var executionRun in SliceExecutionRuns(executionRuns, query))
        {
            if (!TryParseProcessIdentity(executionRun, out var processRunId, out var stepInstanceId) ||
                !requestedRunIds.Contains(processRunId) ||
                requestedStepIds.Count > 0 && !requestedStepIds.Contains(stepInstanceId))
            {
                continue;
            }

            ExecutionRunDetail? detail = null;
            try
            {
                detail = await workspaceService.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
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

        return observations
            .OrderByDescending(observation => observation.UpdatedAtUtc)
            .ToArray();
    }

    private async Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
        ProcessExecutionObservationQuery query,
        CancellationToken cancellationToken)
    {
        var executionRuns = new List<ExecutionRunRecord>();
        var stepIds = query.StepInstanceIds
            .Distinct()
            .ToArray();
        foreach (var runId in query.RunIds.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (stepIds.Length == 0)
            {
                var runRecords = await workspaceService.ListExecutionRunsAsync(
                    new ExecutionRunQuery(
                        Take: Math.Max(1, query.TakePerRun),
                        ProcessRunId: runId.ToString(),
                        UpdatedFromUtc: query.FromUtc,
                        UpdatedToUtc: query.ToUtc),
                    cancellationToken).ConfigureAwait(false);
                executionRuns.AddRange(runRecords);
                continue;
            }

            foreach (var stepId in stepIds)
            {
                var runRecords = await workspaceService.ListExecutionRunsAsync(
                    new ExecutionRunQuery(
                        Take: Math.Max(1, query.TakePerRun),
                        ProcessRunId: runId.ToString(),
                        ProcessStepId: stepId.ToString(),
                        UpdatedFromUtc: query.FromUtc,
                        UpdatedToUtc: query.ToUtc),
                    cancellationToken).ConfigureAwait(false);
                executionRuns.AddRange(runRecords);
            }
        }

        return executionRuns;
    }

    private static IEnumerable<ExecutionRunRecord> SliceExecutionRuns(
        IReadOnlyList<ExecutionRunRecord> executionRuns,
        ProcessExecutionObservationQuery query)
    {
        var take = Math.Max(1, query.TakePerRun);
        var orderedRuns = executionRuns.OrderByDescending(run => run.UpdatedAtUtc);
        if (query.StepInstanceIds.Count == 0)
        {
            return orderedRuns
                .GroupBy(
                    run => run.ProcessRunId,
                    StringComparer.OrdinalIgnoreCase)
                .SelectMany(group => group.Take(take));
        }

        return orderedRuns
            .GroupBy(
                run => $"{run.ProcessRunId}\u001f{run.ProcessStepId}",
                StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.Take(take));
    }

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

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}

