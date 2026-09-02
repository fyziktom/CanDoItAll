using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal sealed record InMemoryWorkflowUsageAppendPlan(
    IReadOnlyList<WorkflowUsageObservation> Observations);

public sealed class InMemoryWorkflowUsageObservationStore : IWorkflowUsageObservationStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<WorkflowUsageObservationId, WorkflowUsageObservation> observations = [];

    public Task AppendAsync(
        WorkflowUsageObservation observation,
        CancellationToken cancellationToken = default)
        => AppendRangeAsync([observation], cancellationToken);

    public Task AppendRangeAsync(
        IReadOnlyList<WorkflowUsageObservation> observationsToAppend,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observationsToAppend);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var observation in observationsToAppend)
        {
            WorkflowUsageObservationValidator.ThrowIfNotPersistable(observation);
        }

        lock (gate)
        {
            var pending = new Dictionary<WorkflowUsageObservationId, WorkflowUsageObservation>();
            foreach (var observation in observationsToAppend)
            {
                if (observations.TryGetValue(observation.Id, out var stored))
                {
                    EnsureSameFact(stored, observation);
                    continue;
                }

                if (pending.TryGetValue(observation.Id, out var staged))
                {
                    EnsureSameFact(staged, observation);
                    continue;
                }

                pending.Add(observation.Id, observation);
            }

            foreach (var observation in pending.Values)
            {
                observations.Add(observation.Id, observation);
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkflowUsageObservation>> ListAsync(
        WorkflowUsageObservationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<WorkflowUsageObservation> result;
        lock (gate)
        {
            result = ApplyQuery(observations.Values, query)
                .OrderBy(observation => observation.RecordedAtUtc)
                .ThenBy(observation => observation.Id.Value)
                .ToArray();
        }

        return Task.FromResult(result);
    }

    internal InMemoryWorkflowUsageAppendPlan PrepareAppend(
        IReadOnlyList<WorkflowUsageObservation> observationsToAppend)
    {
        ArgumentNullException.ThrowIfNull(observationsToAppend);
        foreach (var observation in observationsToAppend)
        {
            WorkflowUsageObservationValidator.ThrowIfNotPersistable(observation);
        }

        return new InMemoryWorkflowUsageAppendPlan(observationsToAppend.ToArray());
    }

    internal bool TryCommitPrepared(
        InMemoryWorkflowUsageAppendPlan plan,
        Func<bool> commit)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(commit);
        lock (gate)
        {
            var pending = new Dictionary<WorkflowUsageObservationId, WorkflowUsageObservation>();
            foreach (var observation in plan.Observations)
            {
                if (observations.TryGetValue(observation.Id, out var stored))
                {
                    EnsureSameFact(stored, observation);
                    continue;
                }

                if (pending.TryGetValue(observation.Id, out var staged))
                {
                    EnsureSameFact(staged, observation);
                    continue;
                }

                pending.Add(observation.Id, observation);
            }

            if (!commit())
            {
                return false;
            }

            foreach (var observation in pending.Values)
            {
                observations.Add(observation.Id, observation);
            }

            return true;
        }
    }

    public Task<WorkflowListPage<WorkflowUsageObservation>> ListPageAsync(
        WorkflowUsageObservationPageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Query);
        if (request.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Workflow usage page index cannot be negative.");
        }

        if (request.PageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Workflow usage page size must be greater than zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        WorkflowListPage<WorkflowUsageObservation> result;
        lock (gate)
        {
            var filtered = ApplyQuery(observations.Values, request.Query)
                .OrderByDescending(observation => observation.RecordedAtUtc)
                .ThenBy(observation => observation.Id.Value)
                .ToArray();
            var items = filtered
                .Skip(request.PageIndex * request.PageSize)
                .Take(request.PageSize)
                .ToArray();
            result = new WorkflowListPage<WorkflowUsageObservation>(
                items,
                request.PageIndex,
                request.PageSize,
                filtered.Length);
        }

        return Task.FromResult(result);
    }

    private static IEnumerable<WorkflowUsageObservation> ApplyQuery(
        IEnumerable<WorkflowUsageObservation> source,
        WorkflowUsageObservationQuery query)
    {
        var filtered = source;
        if (query.RunIds.Count > 0)
        {
            var runIds = query.RunIds.ToHashSet();
            filtered = filtered.Where(observation =>
                observation.RunId is { } runId && runIds.Contains(runId));
        }

        if (query.OriginProcessRunIds.Count > 0)
        {
            var processRunIds = query.OriginProcessRunIds.ToHashSet();
            filtered = filtered.Where(observation =>
                observation.Origin is WorkflowLaunchOrigin.ProcessAssignment processOrigin &&
                processRunIds.Contains(processOrigin.ProcessRun));
        }

        if (query.WorkflowId is { } workflowId)
        {
            filtered = filtered.Where(observation => observation.WorkflowId == workflowId);
        }

        if (query.VersionId is { } versionId)
        {
            filtered = filtered.Where(observation => observation.VersionId == versionId);
        }

        if (query.NodeId is { } nodeId)
        {
            filtered = filtered.Where(observation => observation.NodeId == nodeId);
        }

        if (query.ExecutorId is { } executorId)
        {
            filtered = filtered.Where(observation => observation.ExecutorId == executorId);
        }

        if (!string.IsNullOrWhiteSpace(query.ProviderName))
        {
            filtered = filtered.Where(observation => string.Equals(
                observation.ProviderName,
                query.ProviderName.Trim(),
                StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Model))
        {
            filtered = filtered.Where(observation => string.Equals(
                observation.Model,
                query.Model.Trim(),
                StringComparison.OrdinalIgnoreCase));
        }

        if (query.RecordedFromUtc is { } recordedFromUtc)
        {
            filtered = filtered.Where(observation => observation.RecordedAtUtc >= recordedFromUtc);
        }

        if (query.RecordedToUtc is { } recordedToUtc)
        {
            filtered = filtered.Where(observation => observation.RecordedAtUtc <= recordedToUtc);
        }

        return filtered;
    }

    private static void EnsureSameFact(
        WorkflowUsageObservation stored,
        WorkflowUsageObservation candidate)
    {
        if (stored != candidate)
        {
            throw new WorkflowUsageObservationConflictException(candidate.Id);
        }
    }
}
