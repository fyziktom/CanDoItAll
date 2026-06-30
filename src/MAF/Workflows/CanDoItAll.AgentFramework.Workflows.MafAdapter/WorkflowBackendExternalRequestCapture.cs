using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class WorkflowBackendExternalRequestCapture : IWorkflowExternalRequestCapture
{
    private readonly List<WorkflowExternalRequestRecord> requests = [];

    public IReadOnlyList<WorkflowExternalRequestRecord> Requests => requests;

    public void Record(WorkflowExternalRequestRecord request)
    {
        requests.Add(request);
    }
}
