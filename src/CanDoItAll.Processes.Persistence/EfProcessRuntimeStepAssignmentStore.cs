using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessRuntimeStepAssignmentStore(ProcessPersistenceDbContext dbContext) : IProcessRuntimeStepAssignmentStore
{
    public async ValueTask SaveAsync(
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        foreach (var assignment in assignments)
        {
            var existing = await dbContext.RuntimeStepAssignments
                .FindAsync(new object[] { assignment.RunId.Value, assignment.StepInstanceId.Value }, cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                dbContext.RuntimeStepAssignments.Add(ToEntity(assignment));
                continue;
            }

            Update(existing, assignment);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

        var query = dbContext.RuntimeStepAssignments.AsNoTracking();
        foreach (var snippet in normalized.Select(item => BuildLaunchVariableJsonSnippet(item.Key, item.Value)))
        {
            query = query.Where(assignment => assignment.LaunchVariablesJson.Contains(snippet));
        }

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

    private static ProcessRuntimeStepAssignmentEntity ToEntity(ProcessRuntimeStepAssignment assignment)
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
            Prompt = assignment.Prompt,
            ReadinessHash = assignment.ReadinessHash,
            AssignmentReason = assignment.AssignmentReason,
            ProducedArtifactSlotIds = JoinGuids(assignment.ProducedArtifactSlotIds),
            RequiredArtifactSlotIds = JoinGuids(assignment.RequiredArtifactSlotIds),
            AllowedOperations = JoinStrings(assignment.AllowedOperations),
            OperationTargetScope = assignment.OperationTargetScope,
            LaunchVariablesJson = SerializeLaunchVariables(assignment.LaunchVariables),
            BranchGateSourceStepKey = assignment.BranchGate?.SourceStepKey,
            BranchGateRequiredOutcomeKey = assignment.BranchGate?.RequiredOutcomeKey,
            CreatedAtUtc = assignment.CreatedAtUtc
        };
    }

    private static void Update(
        ProcessRuntimeStepAssignmentEntity entity,
        ProcessRuntimeStepAssignment assignment)
    {
        entity.PlanId = assignment.PlanId.Value;
        entity.StepKey = assignment.StepKey;
        entity.RoleKey = assignment.RoleKey;
        entity.RoleResourceKey = assignment.RoleResourceKey;
        entity.RoleDisplayName = assignment.RoleDisplayName;
        entity.ExecutorKind = assignment.ExecutorKind;
        entity.ExecutorId = assignment.ExecutorId;
        entity.ExecutorDisplayName = assignment.ExecutorDisplayName;
        entity.Prompt = assignment.Prompt;
        entity.ReadinessHash = assignment.ReadinessHash;
        entity.AssignmentReason = assignment.AssignmentReason;
        entity.ProducedArtifactSlotIds = JoinGuids(assignment.ProducedArtifactSlotIds);
        entity.RequiredArtifactSlotIds = JoinGuids(assignment.RequiredArtifactSlotIds);
        entity.AllowedOperations = JoinStrings(assignment.AllowedOperations);
        entity.OperationTargetScope = assignment.OperationTargetScope;
        entity.LaunchVariablesJson = SerializeLaunchVariables(assignment.LaunchVariables);
        entity.BranchGateSourceStepKey = assignment.BranchGate?.SourceStepKey;
        entity.BranchGateRequiredOutcomeKey = assignment.BranchGate?.RequiredOutcomeKey;
        entity.CreatedAtUtc = assignment.CreatedAtUtc;
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
            entity.CreatedAtUtc);
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
