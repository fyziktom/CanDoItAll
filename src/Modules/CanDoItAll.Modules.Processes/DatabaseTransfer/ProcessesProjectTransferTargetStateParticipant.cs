using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessesProjectTransferTargetStateParticipant
    : IProjectTransferTargetStateParticipant
{
    private static readonly string ProjectIdJsonPropertyToken =
        $"\"{ProcessRuntimeLaunchVariables.ProjectId}\"";
    private static readonly string ProjectNodeIdJsonPropertyToken =
        $"\"{ProcessRuntimeLaunchVariables.ProjectNodeId}\"";

    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.Processes;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(ProcessRunRecordEntity),
        typeof(ProcessInstancePlanEntity),
        typeof(ProcessRuntimeStateEntity),
        typeof(ProcessRuntimeStepAssignmentEntity)
    ];

    public async Task<IReadOnlyList<ProjectTransferTargetStateResidue>>
        FindResiduesAsync(
            AppDbContext dbContext,
            CancellationToken cancellationToken)
    {
        var residues = new List<ProjectTransferTargetStateResidue>();
        if (await dbContext.Set<ProcessRunRecordEntity>()
                .AsNoTracking()
                .AnyAsync(item => item.ProjectId.HasValue, cancellationToken))
        {
            residues.Add(new("process runs linked to projects"));
        }

        if (await dbContext.Set<ProcessRuntimeStateEntity>()
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.Status != ProcessRuntimeStatus.Completed &&
                        item.Status != ProcessRuntimeStatus.Failed &&
                        item.Status != ProcessRuntimeStatus.Cancelled,
                    cancellationToken))
        {
            residues.Add(new("nonterminal process runtime state"));
        }

        var launchVariables = await dbContext
            .Set<ProcessRuntimeStepAssignmentEntity>()
            .AsNoTracking()
            .Where(item =>
                item.LaunchVariablesJson.Contains(ProjectIdJsonPropertyToken) ||
                item.LaunchVariablesJson.Contains(ProjectNodeIdJsonPropertyToken))
            .Select(item => item.LaunchVariablesJson)
            .ToListAsync(cancellationToken);
        var hasProjectReference = false;
        var hasUnverifiableProjectReference = false;
        foreach (var launchVariablesJson in launchVariables)
        {
            try
            {
                var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    launchVariablesJson);
                if (variables is null)
                {
                    hasUnverifiableProjectReference = true;
                    continue;
                }

                var hasProjectId = variables.ContainsKey(
                    ProcessRuntimeLaunchVariables.ProjectId);
                var hasProjectNodeId = variables.ContainsKey(
                    ProcessRuntimeLaunchVariables.ProjectNodeId);
                if (!hasProjectId && !hasProjectNodeId)
                {
                    continue;
                }

                var hasValidProjectId = hasProjectId &&
                    ProcessRuntimeLaunchVariables.TryReadProjectId(
                        variables,
                        out _);
                var hasValidProjectNodeId = hasProjectNodeId &&
                    ProcessRuntimeLaunchVariables.TryReadProjectNodeId(
                        variables,
                        out _);
                if (hasValidProjectId || hasValidProjectNodeId)
                {
                    hasProjectReference = true;
                }

                if ((hasProjectId && !hasValidProjectId) ||
                    (hasProjectNodeId && !hasValidProjectNodeId))
                {
                    hasUnverifiableProjectReference = true;
                }
            }
            catch (JsonException)
            {
                hasUnverifiableProjectReference = true;
            }
        }

        if (hasProjectReference)
        {
            residues.Add(new("process step assignments linked to projects"));
        }

        if (hasUnverifiableProjectReference)
        {
            residues.Add(new(
                "process step assignments with malformed project launch state"));
        }

        return residues;
    }
}
