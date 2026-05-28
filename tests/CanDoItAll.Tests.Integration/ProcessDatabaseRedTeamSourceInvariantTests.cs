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

    [Fact]
    public async Task Artifact_recording_serializes_projection_identity_and_recovers_unique_conflicts() {
        var source = await ReadRuntimeOperationsSourceAsync();

        var identityHashIndex = FindRequired(source, "var projectionIdentityHash = projectionLineage?.ProjectionIdentityHash ?? string.Empty;");
        var lockIndex = FindRequired(source, "BeginArtifactRecordTransactionAsync", identityHashIndex);
        var existingProjectionIndex = FindRequired(source, "item.ProjectionIdentityHash == projectionIdentityHash", lockIndex);
        var saveIndex = FindRequired(source, "await dbContext.SaveChangesAsync(cancellationToken);", existingProjectionIndex);
        var uniqueCatchIndex = FindRequired(source, "DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception)", saveIndex);
        var resolveConflictIndex = FindRequired(source, "ResolveArtifactRecordUniqueConflictAsync", uniqueCatchIndex);

        Assert.True(
            identityHashIndex < lockIndex &&
            lockIndex < existingProjectionIndex &&
            existingProjectionIndex < saveIndex &&
            saveIndex < uniqueCatchIndex &&
            uniqueCatchIndex < resolveConflictIndex,
            "Artifact recording must lock on projection identity before lookup/insert and resolve concurrent unique conflicts into an idempotent result.");

        Assert.Contains("pg_advisory_xact_lock", source, StringComparison.Ordinal);
        Assert.Contains("processes.artifact.unique-conflict", source, StringComparison.Ordinal);
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

    private static async Task<string> ReadRuntimeOperationsSourceAsync() {
        var repositoryRoot = FindRepositoryRoot();
        var sourcePath = Path.Combine(
            repositoryRoot,
            "src",
            "CanDoItAll.Modules.Processes",
            "Runtime",
            "ProcessesService.Runtime.Operations.cs");

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
