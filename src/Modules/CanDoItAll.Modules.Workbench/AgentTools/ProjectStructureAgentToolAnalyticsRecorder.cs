using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureAgentToolAnalyticsRecorder(
    IProjectStructureAnalyticsService analyticsService,
    ILogger logger)
{
    public async Task RecordBestEffortAsync(ProjectStructureAnalyticsWriteRequest request)
    {
        try
        {
            await analyticsService.RecordAsync(request, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Project-structure analytics failed after operation outcome was known. Operation={Operation} ProjectId={ProjectId} NodeId={NodeId} Succeeded={Succeeded} FailureType={FailureType}.",
                request.OperationName,
                request.ProjectId,
                request.NodeKey,
                request.Succeeded,
                exception.GetType().Name);
        }
    }
}