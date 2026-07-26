using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessRuntimeStepAssignmentStore(ProcessPersistenceDbContext dbContext) : IProcessRuntimeStepAssignmentStore
{
    private static readonly JsonSerializerOptions CapabilityScopeSerializerOptions = CreateCapabilityScopeSerializerOptions();

    public async ValueTask SaveAsync(
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        var keys = new HashSet<(Guid RunId, Guid StepInstanceId)>();
        var validated = new List<(
            ProcessRuntimeStepAssignmentEntity Existing,
            ProcessRuntimeStepAssignment Assignment)>(assignments.Count);
        try
        {
            foreach (var assignment in assignments)
            {
                var key = (assignment.RunId.Value, assignment.StepInstanceId.Value);
                if (!keys.Add(key))
                {
                    throw new InvalidOperationException(
                        $"Process assignment '{assignment.RunId}/{assignment.StepInstanceId}' appears more than once in the same update batch.");
                }

                var existing = await dbContext.RuntimeStepAssignments
                    .FindAsync(
                        new object[] { assignment.RunId.Value, assignment.StepInstanceId.Value },
                        cancellationToken)
                    .ConfigureAwait(false);
                if (existing is null)
                {
                    throw new InvalidOperationException(
                        $"New process assignment '{assignment.RunId}/{assignment.StepInstanceId}' must be committed atomically with its initial runtime state.");
                }

                EnsureImmutableLineage(existing, assignment);
                EnsureOnlyRepairFieldsChanged(existing, assignment);
                validated.Add((existing, assignment));
            }

            foreach (var (existing, assignment) in validated)
            {
                Update(existing, assignment);
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    public async ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(
        ProcessRunId runId,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.RuntimeStepAssignments
            .AsNoTracking()
            .Where(assignment => assignment.RunId == runId.Value)
            .OrderBy(assignment => assignment.StepKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToAssignment).ToArray();
    }

    public async ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunsAsync(
        IReadOnlyList<ProcessRunId> runIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runIds);
        if (runIds.Count > IProcessRuntimeStepAssignmentStore.MaximumBatchRunCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runIds),
                runIds.Count,
                $"Step-assignment batch cannot exceed {IProcessRuntimeStepAssignmentStore.MaximumBatchRunCount} runs.");
        }

        var values = runIds
            .Select(runId => runId.Value)
            .Distinct()
            .ToArray();
        if (values.Length == 0)
        {
            return [];
        }

        var rows = await dbContext.RuntimeStepAssignments
            .AsNoTracking()
            .Where(assignment => values.Contains(assignment.RunId))
            .OrderBy(assignment => assignment.RunId)
            .ThenBy(assignment => assignment.StepKey)
            .ThenBy(assignment => assignment.StepInstanceId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .Select(ToAssignment)
            .ToArray();
    }

    public async ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> FindByLaunchVariablesAsync(
        IReadOnlyDictionary<string, string> requiredVariables,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requiredVariables);

        var normalized = NormalizeRequiredVariables(requiredVariables);
        if (normalized.Count == 0)
        {
            return [];
        }

        var query = CreateLaunchVariableQuery(normalized);

        var rows = await query
            .OrderBy(assignment => assignment.CreatedAtUtc)
            .ThenBy(assignment => assignment.RunId)
            .ThenBy(assignment => assignment.StepKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(ToAssignment)
            .Where(assignment => MatchesRequiredVariables(assignment.LaunchVariables, normalized))
            .ToArray();
    }

    public async ValueTask<ProcessRuntimeStepAssignmentBoundedSearchResult>
        FindByLaunchVariablesBoundedAsync(
            IReadOnlyDictionary<string, string> requiredVariables,
            int maximumDistinctRunCount,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requiredVariables);
        if (maximumDistinctRunCount is < 1 or
            > IProcessRuntimeStepAssignmentStore.MaximumBoundedSearchRunCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDistinctRunCount),
                maximumDistinctRunCount,
                $"Bounded assignment search must allow between 1 and {IProcessRuntimeStepAssignmentStore.MaximumBoundedSearchRunCount} distinct runs.");
        }

        var normalized = NormalizeRequiredVariables(requiredVariables);
        if (normalized.Count == 0)
        {
            return new ProcessRuntimeStepAssignmentBoundedSearchResult([], false);
        }

        var query = CreateLaunchVariableQuery(normalized);
        var runIds = await query
            .Select(assignment => assignment.RunId)
            .Distinct()
            .OrderBy(runId => runId)
            .Take(maximumDistinctRunCount + 1)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (runIds.Length > maximumDistinctRunCount)
        {
            return new ProcessRuntimeStepAssignmentBoundedSearchResult([], true);
        }

        var rows = await query
            .Where(assignment => runIds.Contains(assignment.RunId))
            .OrderBy(assignment => assignment.CreatedAtUtc)
            .ThenBy(assignment => assignment.RunId)
            .ThenBy(assignment => assignment.StepKey)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var assignments = rows
            .Select(ToAssignment)
            .Where(assignment => MatchesRequiredVariables(assignment.LaunchVariables, normalized))
            .ToArray();
        return new ProcessRuntimeStepAssignmentBoundedSearchResult(assignments, false);
    }

    public async ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.RuntimeStepAssignments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                assignment => assignment.RunId == runId.Value && assignment.StepInstanceId == stepInstanceId.Value,
                cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : ToAssignment(row);
    }

    internal static ProcessRuntimeStepAssignmentEntity ToEntity(ProcessRuntimeStepAssignment assignment)
    {
        return new ProcessRuntimeStepAssignmentEntity
        {
            RunId = assignment.RunId.Value,
            StepInstanceId = assignment.StepInstanceId.Value,
            PlanId = assignment.PlanId.Value,
            StepKey = assignment.StepKey,
            RoleKey = assignment.RoleKey,
            RoleResourceKey = assignment.RoleResourceKey,
            RoleDisplayName = assignment.RoleDisplayName,
            ExecutorKind = assignment.ExecutorKind,
            ExecutorId = assignment.ExecutorId,
            ExecutorDisplayName = assignment.ExecutorDisplayName,
            WorkflowId = assignment.WorkflowBinding?.WorkflowId.Value,
            WorkflowVersionId = assignment.WorkflowBinding?.WorkflowVersionId?.Value,
            WorkflowOutputMapping = assignment.WorkflowBinding is { } workflowBinding
                ? (int)workflowBinding.OutputMapping
                : null,
            Prompt = assignment.Prompt,
            ReadinessHash = assignment.ReadinessHash,
            AssignmentReason = assignment.AssignmentReason,
            ProducedArtifactSlotIds = JoinGuids(assignment.ProducedArtifactSlotIds),
            RequiredArtifactSlotIds = JoinGuids(assignment.RequiredArtifactSlotIds),
            AllowedOperations = JoinStrings(assignment.AllowedOperations),
            OperationTargetScope = assignment.OperationTargetScope,
            LaunchVariablesJson = SerializeLaunchVariables(assignment.LaunchVariables),
            CapabilityScopeJson = SerializeCapabilityScope(assignment.CapabilityScope),
            BranchGateSourceStepKey = assignment.BranchGate?.SourceStepKey,
            BranchGateRequiredOutcomeKey = assignment.BranchGate?.RequiredOutcomeKey,
            CreatedAtUtc = assignment.CreatedAtUtc
        };
    }

    private static void Update(
        ProcessRuntimeStepAssignmentEntity entity,
        ProcessRuntimeStepAssignment assignment)
    {
        entity.Prompt = assignment.Prompt;
        entity.ExecutorKind = assignment.ExecutorKind;
        entity.ExecutorId = assignment.ExecutorId;
        entity.ExecutorDisplayName = assignment.ExecutorDisplayName;
        entity.ReadinessHash = assignment.ReadinessHash;
        entity.AssignmentReason = assignment.AssignmentReason;
    }

    private static void EnsureImmutableLineage(
        ProcessRuntimeStepAssignmentEntity existing,
        ProcessRuntimeStepAssignment assignment)
    {
        if (existing.PlanId != assignment.PlanId.Value)
        {
            throw new InvalidOperationException(
                $"Process assignment '{assignment.RunId}/{assignment.StepInstanceId}' cannot change immutable '{nameof(assignment.PlanId)}'.");
        }

        if (!string.Equals(existing.StepKey, assignment.StepKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Process assignment '{assignment.RunId}/{assignment.StepInstanceId}' cannot change immutable '{nameof(assignment.StepKey)}'.");
        }

        if (existing.CreatedAtUtc != assignment.CreatedAtUtc)
        {
            throw new InvalidOperationException(
                $"Process assignment '{assignment.RunId}/{assignment.StepInstanceId}' cannot change immutable '{nameof(assignment.CreatedAtUtc)}'.");
        }

        var existingLaunchVariables = DeserializeLaunchVariables(existing.LaunchVariablesJson);
        var nextLaunchVariables = NormalizeLaunchVariables(assignment.LaunchVariables);
        EnsureImmutableLineageKey(
            assignment,
            existingLaunchVariables,
            nextLaunchVariables,
            ProcessRuntimeLaunchVariables.ParentProcessRunId);
        EnsureImmutableLineageKey(
            assignment,
            existingLaunchVariables,
            nextLaunchVariables,
            ProcessRuntimeLaunchVariables.ParentProcessStepId);
    }

    private static void EnsureOnlyRepairFieldsChanged(
        ProcessRuntimeStepAssignmentEntity existing,
        ProcessRuntimeStepAssignment assignment)
    {
        var proposed = ToEntity(assignment);
        var normalizedExistingLaunchVariables =
            SerializeLaunchVariables(DeserializeLaunchVariables(existing.LaunchVariablesJson));
        var normalizedExistingCapabilityScope =
            SerializeCapabilityScope(DeserializeCapabilityScope(existing.CapabilityScopeJson));
        if (existing.RunId == proposed.RunId &&
            existing.StepInstanceId == proposed.StepInstanceId &&
            existing.PlanId == proposed.PlanId &&
            string.Equals(existing.StepKey, proposed.StepKey, StringComparison.Ordinal) &&
            string.Equals(existing.RoleKey, proposed.RoleKey, StringComparison.Ordinal) &&
            string.Equals(existing.RoleResourceKey, proposed.RoleResourceKey, StringComparison.Ordinal) &&
            string.Equals(existing.RoleDisplayName, proposed.RoleDisplayName, StringComparison.Ordinal) &&
            existing.WorkflowId == proposed.WorkflowId &&
            existing.WorkflowVersionId == proposed.WorkflowVersionId &&
            existing.WorkflowOutputMapping == proposed.WorkflowOutputMapping &&
            string.Equals(existing.ProducedArtifactSlotIds, proposed.ProducedArtifactSlotIds, StringComparison.Ordinal) &&
            string.Equals(existing.RequiredArtifactSlotIds, proposed.RequiredArtifactSlotIds, StringComparison.Ordinal) &&
            string.Equals(existing.AllowedOperations, proposed.AllowedOperations, StringComparison.Ordinal) &&
            string.Equals(existing.OperationTargetScope, proposed.OperationTargetScope, StringComparison.Ordinal) &&
            string.Equals(normalizedExistingLaunchVariables, proposed.LaunchVariablesJson, StringComparison.Ordinal) &&
            string.Equals(normalizedExistingCapabilityScope, proposed.CapabilityScopeJson, StringComparison.Ordinal) &&
            string.Equals(existing.BranchGateSourceStepKey, proposed.BranchGateSourceStepKey, StringComparison.Ordinal) &&
            string.Equals(existing.BranchGateRequiredOutcomeKey, proposed.BranchGateRequiredOutcomeKey, StringComparison.Ordinal) &&
            existing.CreatedAtUtc == proposed.CreatedAtUtc)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Process assignment '{assignment.RunId}/{assignment.StepInstanceId}' can only change its recovery prompt or executor-readiness repair fields after initial launch.");
    }

    private static void EnsureImmutableLineageKey(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyDictionary<string, string> existingLaunchVariables,
        IReadOnlyDictionary<string, string> nextLaunchVariables,
        string key)
    {
        var hadExistingValue = existingLaunchVariables.TryGetValue(key, out var existingValue);
        var hasNextValue = nextLaunchVariables.TryGetValue(key, out var nextValue);
        if (hadExistingValue == hasNextValue &&
            (!hadExistingValue ||
             string.Equals(existingValue, nextValue, StringComparison.Ordinal)))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Process assignment '{assignment.RunId}/{assignment.StepInstanceId}' cannot add, remove, or change immutable parent-lineage key '{key}'.");
    }

    private static ProcessRuntimeStepAssignment ToAssignment(ProcessRuntimeStepAssignmentEntity entity)
    {
        var branchGate = string.IsNullOrWhiteSpace(entity.BranchGateSourceStepKey) ||
            string.IsNullOrWhiteSpace(entity.BranchGateRequiredOutcomeKey)
                ? null
                : new ProcessRuntimeBranchGate(entity.BranchGateSourceStepKey, entity.BranchGateRequiredOutcomeKey);

        return new ProcessRuntimeStepAssignment(
            new ProcessRunId(entity.RunId),
            new ProcessInstancePlanId(entity.PlanId),
            new ProcessStepInstanceId(entity.StepInstanceId),
            entity.StepKey,
            entity.RoleKey,
            entity.RoleResourceKey,
            entity.RoleDisplayName,
            entity.ExecutorKind,
            entity.ExecutorId,
            entity.ExecutorDisplayName,
            entity.Prompt,
            entity.ReadinessHash,
            entity.AssignmentReason,
            SplitArtifactSlotIds(entity.ProducedArtifactSlotIds),
            SplitArtifactSlotIds(entity.RequiredArtifactSlotIds),
            SplitStrings(entity.AllowedOperations),
            entity.OperationTargetScope,
            DeserializeLaunchVariables(entity.LaunchVariablesJson),
            branchGate,
            entity.CreatedAtUtc)
        {
            WorkflowBinding = ToWorkflowBinding(entity),
            CapabilityScope = DeserializeCapabilityScope(entity.CapabilityScopeJson)
        };
    }

    private static ProcessWorkflowExecutorBinding? ToWorkflowBinding(
        ProcessRuntimeStepAssignmentEntity entity)
    {
        if (!entity.WorkflowId.HasValue)
        {
            if (entity.WorkflowVersionId.HasValue || entity.WorkflowOutputMapping.HasValue)
            {
                throw new InvalidOperationException(
                    $"Process assignment '{entity.RunId:D}/{entity.StepInstanceId:D}' persisted workflow binding fields without a workflow id.");
            }

            return null;
        }

        if (entity.WorkflowOutputMapping is not { } outputMappingValue ||
            !Enum.IsDefined(typeof(ProcessWorkflowOutputMappingKind), outputMappingValue))
        {
            throw new InvalidOperationException(
                $"Process assignment '{entity.RunId:D}/{entity.StepInstanceId:D}' persisted an invalid workflow output mapping '{entity.WorkflowOutputMapping?.ToString() ?? "missing"}'.");
        }

        return new ProcessWorkflowExecutorBinding(
            new ProcessWorkflowId(entity.WorkflowId.Value),
            entity.WorkflowVersionId is { } versionId
                ? new ProcessWorkflowVersionId(versionId)
                : null,
            (ProcessWorkflowOutputMappingKind)outputMappingValue);
    }

    private static string JoinGuids(IReadOnlyList<ArtifactSlotId> slotIds)
    {
        return string.Join(';', slotIds.Select(slotId => slotId.Value).Order());
    }

    private static IReadOnlyList<ArtifactSlotId> SplitArtifactSlotIds(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => new ArtifactSlotId(Guid.Parse(item)))
            .ToArray();
    }

    private static string JoinStrings(IReadOnlyList<string> values)
    {
        return string.Join(
            ';',
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> SplitStrings(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string SerializeLaunchVariables(IReadOnlyDictionary<string, string> variables)
    {
        return JsonSerializer.Serialize(NormalizeLaunchVariables(variables));
    }

    private static IReadOnlyDictionary<string, string> DeserializeLaunchVariables(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(value)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        return variables
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(
                item => item.Key.Trim(),
                item => item.Value?.Trim() ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static string SerializeCapabilityScope(ProcessCapabilityScope capabilityScope)
    {
        var normalized = ProcessCapabilityScope.Normalize(capabilityScope);
        return normalized.IsEmpty
            ? "{}"
            : JsonSerializer.Serialize(normalized, CapabilityScopeSerializerOptions);
    }

    private static ProcessCapabilityScope DeserializeCapabilityScope(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ProcessCapabilityScope.Empty;
        }

        var scope = JsonSerializer.Deserialize<ProcessCapabilityScope>(value, CapabilityScopeSerializerOptions);
        return ProcessCapabilityScope.Normalize(scope);
    }

    private static JsonSerializerOptions CreateCapabilityScopeSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }

    private static IReadOnlyDictionary<string, string> NormalizeRequiredVariables(
        IReadOnlyDictionary<string, string> variables)
        => NormalizeLaunchVariables(variables);

    private static IReadOnlyDictionary<string, string> NormalizeLaunchVariables(
        IReadOnlyDictionary<string, string> variables)
    {
        return variables
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(
                item => item.Key.Trim(),
                item => item.Value?.Trim() ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static string BuildLaunchVariableJsonSnippet(string key, string value)
    {
        return $"{JsonSerializer.Serialize(key)}:{JsonSerializer.Serialize(value)}";
    }

    private IQueryable<ProcessRuntimeStepAssignmentEntity> CreateLaunchVariableQuery(
        IReadOnlyDictionary<string, string> normalizedRequiredVariables)
    {
        var query = dbContext.RuntimeStepAssignments.AsNoTracking();
        foreach (var snippet in normalizedRequiredVariables.Select(item =>
                     BuildLaunchVariableJsonSnippet(item.Key, item.Value)))
        {
            query = query.Where(assignment =>
                assignment.LaunchVariablesJson.Contains(snippet));
        }

        return query;
    }

    private static bool MatchesRequiredVariables(
        IReadOnlyDictionary<string, string> candidate,
        IReadOnlyDictionary<string, string> required)
    {
        foreach (var item in required)
        {
            if (!candidate.TryGetValue(item.Key, out var candidateValue) ||
                !string.Equals(candidateValue, item.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
