using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Web.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ProcessPlanningAccountingApiSubmission(Guid ActionId, AgentPlanningDecisionExpectation Expected,
    ProcessPlanningReviewApiExpectation Process) {
    internal ProjectTaskPlanProcessReconciliationRequest ToDomain(Guid runId, Guid stepInstanceId, Guid executionRunId) {
        ArgumentNullException.ThrowIfNull(Expected);
        ArgumentNullException.ThrowIfNull(Process);
        if (ActionId == Guid.Empty || runId == Guid.Empty || stepInstanceId == Guid.Empty || executionRunId == Guid.Empty ||
                Process.RunId != runId || Process.StepInstanceId != stepInstanceId || Process.ExecutionRunId != executionRunId ||
                Expected.ExecutionRunId != executionRunId) {
            throw new ArgumentException("Accounting requires a retained action ID and the exact displayed Process run, step and Agent child.");
        }
        ProjectTaskPlanProcessReconciliationRequest request = new(Expected, Process.ToDomain(ActionId));
        request.Validate();
        return request;
    }
}

internal sealed record ProcessPlanningAccountingApiResult(Guid ActionId, ProcessPlanningChildApiView Agent,
    ProcessRuntimeTransitionOutcome Completion, IReadOnlyList<string> RefusalCodes, bool RequiresRuntimeReload) {
    internal static ProcessPlanningAccountingApiResult From(ProjectTaskPlanProcessReconciliationResult value)
        => new(value.ActionId, ProcessPlanningChildApiView.From(value.Agent), value.Completion.Outcome,
            value.Completion.Diagnostics.Select(item => item.Code).ToArray(), true);
}
