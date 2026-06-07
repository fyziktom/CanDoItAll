namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessArtifactProjectionOrchestrator
{
    private readonly IReadOnlyList<IProcessArtifactProjectionSourceCoordinator> sourceCoordinators;

    private ProcessArtifactProjectionOrchestrator(
        IReadOnlyList<IProcessArtifactProjectionSourceCoordinator> sourceCoordinators)
    {
        this.sourceCoordinators = sourceCoordinators;
    }

    public static ProcessArtifactProjectionOrchestrator CreateDefault(ProcessArtifactProjectionFacetSet facets)
    {
        var existingManagedCoordinator = new ProcessExistingManagedArtifactProjectionCoordinator(
            facets.PathResolver,
            facets.FileIo,
            facets.ArtifactClassifier,
            facets.ExpectationMatcher,
            facets.CandidateState);

        return new ProcessArtifactProjectionOrchestrator(
            new IProcessArtifactProjectionSourceCoordinator[]
            {
                new ProcessExecutionArtifactProjectionCoordinator(
                    facets.ClaimGuard,
                    facets.PathResolver,
                    facets.FileIo,
                    facets.ArtifactClassifier,
                    facets.ExpectationMatcher,
                    facets.CandidateState),
                new ProcessMockArtifactProjectionCoordinator(
                    facets.PathResolver,
                    facets.FileIo,
                    facets.ArtifactClassifier,
                    facets.ProcessMockRules,
                    facets.CandidateState),
                new ProcessWorkspaceWrittenArtifactProjectionCoordinator(
                    facets.PathResolver,
                    facets.FileIo,
                    facets.ArtifactClassifier,
                    facets.ExpectationMatcher,
                    facets.ProjectStructureMatcher,
                    facets.SessionObservationSource,
                    facets.CandidateState),
                existingManagedCoordinator,
                new ProcessResponseTextArtifactProjectionCoordinator(
                    facets.PathResolver,
                    facets.FileIo,
                    facets.ArtifactClassifier,
                    facets.ResponseTextRules,
                    facets.ExpectationMatcher,
                    existingManagedCoordinator,
                    facets.CandidateState),
                new ProcessProviderNativeBrowserArtifactProjectionCoordinator(
                    facets.PathResolver,
                    facets.FileIo,
                    facets.ArtifactClassifier,
                    facets.ExpectationMatcher,
                    facets.BrowserOutputRules,
                    facets.CandidateState),
                new ProcessCompletedDecisionArtifactCoordinator(
                    facets.ExpectationMatcher,
                    facets.DecisionArtifactRules,
                    facets.LineageFactory,
                    facets.CandidateState)
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
