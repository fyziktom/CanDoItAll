using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Web.Api;

/// <summary>
/// Bridges the stable public HTTP approval-response contract (<see cref="PendingApprovalApiRequest"/>,
/// additive <c>Decisions</c> field) onto the per-proposal decision overloads (SB15). New clients
/// send explicit per-proposal decisions; existing clients keep sending only <c>Approved</c>, which
/// this mapper expands against the run's currently pending approvals (read fresh, right before the
/// continuation call) so the HTTP contract stays backward compatible without any API handler
/// calling the legacy bool overload itself.
/// </summary>
internal static class AgentApprovalDecisionRequestMapper
{
    public static async Task<IReadOnlyList<PendingToolApprovalDecision>> ResolveDecisionsAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        Guid executionRunId,
        PendingApprovalApiRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspaceService);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Decisions is { Count: > 0 } explicitDecisions)
        {
            return explicitDecisions
                .Select(item => new PendingToolApprovalDecision(item.ApprovalId, item.Approved))
                .ToArray();
        }

        var detail = await workspaceService.GetExecutionRunDetailAsync(executionRunId, cancellationToken);
        return detail.Run.PendingApprovals
            .Select(item => new PendingToolApprovalDecision(item.ApprovalId, request.Approved))
            .ToArray();
    }
}
