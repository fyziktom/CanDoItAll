namespace CanDoItAll.Tests.Integration;

public sealed class ProcessDatabaseRedTeamSourceInvariantTests {
    [Fact]
    public async Task Dispatch_candidate_hydration_occurs_only_after_durable_step_claim() {
        var source = await ReadDispatchSourceAsync();

        var headerLoadIndex = FindRequired(source, "LoadDispatchCandidateHeadersAsync");
        var claimIndex = FindRequired(source, "TryClaimStepDispatchAsync", headerLoadIndex);
        var hydrationIndex = FindRequired(
            source,
            "LoadDispatchCandidateAsync(processRunId, dispatchClaim.StepRunId",
            claimIndex);

        Assert.True(
            headerLoadIndex < claimIndex && claimIndex < hydrationIndex,
            "Dispatch must load cheap candidate headers, durably claim a step, then hydrate the full candidate.");
    }

    [Fact]
    public async Task Dispatch_claim_loss_is_checked_before_artifact_projection_and_completion_transition() {
        var source = await ReadDispatchSourceAsync();

        var executionIndex = FindRequired(source, "ExecuteUntilSettledAsync");
        var claimLostGuardIndex = FindRequired(source, "dispatchHeartbeat.ThrowIfClaimLost();", executionIndex);
        var artifactProjectionIndex = FindRequired(source, "ProjectExecutionArtifactsAsync", claimLostGuardIndex);
        var transitionIndex = FindRequired(source, "TransitionStepWithClaimAsync", artifactProjectionIndex);

        Assert.True(
            executionIndex < claimLostGuardIndex &&
            claimLostGuardIndex < artifactProjectionIndex &&
            artifactProjectionIndex < transitionIndex,
            "Stale dispatch workers must stop before projecting artifacts or attempting completion transitions.");

        var transitionWindow = source.Substring(transitionIndex, Math.Min(900, source.Length - transitionIndex));
        Assert.Contains("dispatchClaim", transitionWindow, StringComparison.Ordinal);
    }

    private static async Task<string> ReadDispatchSourceAsync() {
        var repositoryRoot = FindRepositoryRoot();
        var sourcePath = Path.Combine(
            repositoryRoot,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.Dispatch.cs");

        return await File.ReadAllTextAsync(sourcePath);
    }

    private static string FindRepositoryRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null) {
            var candidatePath = Path.Combine(
                directory.FullName,
                "src",
                "CanDoItAll.Modules.Processes",
                "Automation",
                "Dispatch",
                "ProcessRunAutomationDispatchService.Dispatch.cs");
            if (File.Exists(candidatePath)) {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root for process dispatch source invariant tests.");
    }

    private static int FindRequired(string source, string value, int startIndex = 0) {
        var index = source.IndexOf(value, startIndex, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Expected to find '{value}' in process dispatch source.");
        return index;
    }
}
