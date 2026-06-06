namespace CanDoItAll.Modules.Processes;

internal interface IProcessArtifactProjectionSourceCoordinator
{
    Task ProjectAsync(ProcessArtifactProjectionContext context);
}
