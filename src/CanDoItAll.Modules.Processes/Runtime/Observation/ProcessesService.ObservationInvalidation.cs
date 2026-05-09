namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private void NotifyDefinitionObservationChanged(Guid? projectId, Guid definitionId)
    {
        processObservationInvalidator.NotifyDefinitionChanged(new ProcessDefinitionObservationKey(projectId, definitionId));
    }

    private void NotifyRunObservationChanged(Guid? projectId, Guid definitionId, Guid runId)
    {
        processObservationInvalidator.NotifyRunChanged(new ProcessRunObservationKey(projectId, definitionId, runId));
    }
}
