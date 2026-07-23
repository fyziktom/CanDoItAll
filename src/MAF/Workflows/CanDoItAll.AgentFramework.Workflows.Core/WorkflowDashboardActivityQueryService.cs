using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowDashboardActivityQueryService(
    IWorkflowDashboardActivityStore activityStore,
    IWorkflowCatalogLookupService catalogLookupService) : IWorkflowDashboardActivityQueryService
{
    public async Task<WorkflowDashboardActivityResult> QueryAsync(
        WorkflowDashboardActivityQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var activity = await activityStore
            .QueryActivityAsync(query, cancellationToken)
            .ConfigureAwait(false);
        if (activity.Runs.Count == 0)
        {
            return new WorkflowDashboardActivityResult(activity.Mode, []);
        }

        var workflowIds = activity.Runs
            .Select(run => run.WorkflowId)
            .Distinct()
            .ToArray();
        var definitions = await catalogLookupService
            .LookupDefinitionsAsync(new WorkflowCatalogLookupQuery(workflowIds), cancellationToken)
            .ConfigureAwait(false);
        var namesByWorkflowId = definitions.ToDictionary(definition => definition.Id, definition => definition.Name);
        var items = activity.Runs
            .Select(run => new WorkflowDashboardActivityItem(
                run,
                namesByWorkflowId.GetValueOrDefault(run.WorkflowId) ?? DeletedWorkflowName(run.WorkflowId)))
            .ToArray();

        return new WorkflowDashboardActivityResult(activity.Mode, items);
    }

    private static string DeletedWorkflowName(WorkflowId workflowId)
        => $"Deleted workflow {workflowId.Value:D}";
}
