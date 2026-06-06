namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessArtifactProjectionOrchestrator
{
    private readonly IReadOnlyList<IProcessArtifactProjectionSourceCoordinator> sourceCoordinators;

    private ProcessArtifactProjectionOrchestrator(
        IReadOnlyList<IProcessArtifactProjectionSourceCoordinator> sourceCoordinators)
    {
        this.sourceCoordinators = sourceCoordinators;
    }

    public static ProcessArtifactProjectionOrchestrator CreateDefault(IProcessArtifactProjectionHost host)
    {
        var existingManagedCoordinator = new ProcessExistingManagedArtifactProjectionCoordinator(host);

        return new ProcessArtifactProjectionOrchestrator(
            new IProcessArtifactProjectionSourceCoordinator[]
            {
                new ProcessExecutionArtifactProjectionCoordinator(host),
                new ProcessMockArtifactProjectionCoordinator(host),
                new ProcessWorkspaceWrittenArtifactProjectionCoordinator(host),
                existingManagedCoordinator,
                new ProcessResponseTextArtifactProjectionCoordinator(host, existingManagedCoordinator),
                new ProcessProviderNativeBrowserArtifactProjectionCoordinator(host),
                new ProcessCompletedDecisionArtifactCoordinator(host)
            });
    }

    public async Task ProjectAsync(ProcessArtifactProjectionContext context)
    {
        foreach (var sourceCoordinator in sourceCoordinators)
        {
            await sourceCoordinator.ProjectAsync(context);
        }
    }
}
