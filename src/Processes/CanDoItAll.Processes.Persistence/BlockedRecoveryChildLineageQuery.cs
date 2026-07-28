using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

internal static class BlockedRecoveryChildLineageQuery
{
    public static IQueryable<LinkedChildAssignmentRow> Compose(
        IQueryable<ProcessRuntimeStepAssignmentEntity> assignments,
        ProcessRunId parentRunId,
        ProcessStepInstanceId parentStepInstanceId)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        var parentRunSnippet = BuildLaunchVariableJsonSnippet(
            ProcessRuntimeLaunchVariables.ParentProcessRunId,
            parentRunId.ToString());
        var parentStepSnippet = BuildLaunchVariableJsonSnippet(
            ProcessRuntimeLaunchVariables.ParentProcessStepId,
            parentStepInstanceId.ToString());
        var matchingAssignments = assignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.LaunchVariablesJson.Contains(parentRunSnippet) &&
                assignment.LaunchVariablesJson.Contains(parentStepSnippet));

        return matchingAssignments
            .Select(assignment => assignment.RunId)
            .Distinct()
            .Select(runId => new LinkedChildAssignmentRow
            {
                RunId = runId,
                LaunchVariablesJson = matchingAssignments
                    .Where(assignment => assignment.RunId == runId)
                    .Select(assignment => assignment.LaunchVariablesJson)
                    .First(),
                CreatedAtUtc = matchingAssignments
                    .Where(assignment => assignment.RunId == runId)
                    .Max(assignment => assignment.CreatedAtUtc)
            })
            .OrderByDescending(child => child.CreatedAtUtc)
            .ThenByDescending(child => child.RunId)
            .Take(ProcessRuntimeChildLineageEvidenceRules.MaximumLinkedChildRunCount + 1);
    }

    private static string BuildLaunchVariableJsonSnippet(string key, string value)
    {
        return $"{JsonSerializer.Serialize(key)}:{JsonSerializer.Serialize(value)}";
    }
}

internal sealed class LinkedChildAssignmentRow
{
    public Guid RunId { get; init; }

    public string LaunchVariablesJson { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }
}
