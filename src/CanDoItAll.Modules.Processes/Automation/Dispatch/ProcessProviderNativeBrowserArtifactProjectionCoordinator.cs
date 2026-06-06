using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using System.Text;


namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessProviderNativeBrowserArtifactProjectionCoordinator : IProcessArtifactProjectionSourceCoordinator
{
    private readonly IProcessProjectionPathResolver pathResolver;
    private readonly IProcessProjectionFileIo fileIo;
    private readonly IProcessProjectionArtifactClassifier artifactClassifier;
    private readonly IProcessProjectionExpectationMatcher expectationMatcher;
    private readonly IProcessProjectionSessionObservationSource sessionObservationSource;
    private readonly IProcessProjectionBrowserOutputRules browserOutputRules;
    private readonly IProcessProjectionCandidateStateUpdater candidateState;

    public ProcessProviderNativeBrowserArtifactProjectionCoordinator(
        IProcessProjectionPathResolver pathResolver,
        IProcessProjectionFileIo fileIo,
        IProcessProjectionArtifactClassifier artifactClassifier,
        IProcessProjectionExpectationMatcher expectationMatcher,
        IProcessProjectionSessionObservationSource sessionObservationSource,
        IProcessProjectionBrowserOutputRules browserOutputRules,
        IProcessProjectionCandidateStateUpdater candidateState)
    {
        this.pathResolver = pathResolver;
        this.fileIo = fileIo;
        this.artifactClassifier = artifactClassifier;
        this.expectationMatcher = expectationMatcher;
        this.sessionObservationSource = sessionObservationSource;
        this.browserOutputRules = browserOutputRules;
        this.candidateState = candidateState;
    }

    public async Task ProjectAsync(ProcessArtifactProjectionContext context)
    {
        var browserOutputsByToolName = sessionObservationSource.ResolveSuccessfulBrowserToolOutputFiles(context.Run);
        if (browserOutputsByToolName.Count == 0)
        {
            return;
        }

        var browserWorkingDirectory = sessionObservationSource.ResolveProviderNativeBrowserWorkingDirectory(context.Run) ?? context.WorkspaceRoot;
        if (string.IsNullOrWhiteSpace(browserWorkingDirectory))
        {
            return;
        }

        await ProjectExpectedOutputsAsync(context, browserOutputsByToolName, browserWorkingDirectory);
        await ProjectDiscoveredOutputsAsync(context, browserOutputsByToolName, browserWorkingDirectory);
    }

    private async Task ProjectExpectedOutputsAsync(
        ProcessArtifactProjectionContext context,
        IReadOnlyDictionary<string, IReadOnlyList<string>> browserOutputsByToolName,
        string browserWorkingDirectory)
    {
        foreach (var expectedArtifact in context.Candidate.ExpectedArtifacts)
        {
            if (!browserOutputRules.TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var expectedRelativePath))
            {
                continue;
            }

            if (context.Candidate.MutableState.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                context.Run.Artifacts.Any(artifact => expectationMatcher.ResolveArtifactExpectationId(context.Candidate, context.Run, artifact) == expectedArtifact.Id))
            {
                continue;
            }

            var requiredToolName = browserOutputRules.ResolveProviderNativeBrowserToolName(expectedRelativePath);
            if (string.IsNullOrWhiteSpace(requiredToolName) ||
                !browserOutputsByToolName.TryGetValue(requiredToolName, out var outputFileNames))
            {
                continue;
            }

            var matchedOutputFileName = outputFileNames.FirstOrDefault(outputFileName =>
                browserOutputRules.MatchesExpectedBrowserOutputFile(expectedRelativePath, outputFileName));
            if (string.IsNullOrWhiteSpace(matchedOutputFileName))
            {
                continue;
            }

            var sourceFullPath = Path.GetFullPath(Path.Combine(
                browserWorkingDirectory,
                matchedOutputFileName.Replace('/', Path.DirectorySeparatorChar)));
            if (!pathResolver.IsWithinWorkspace(context.WorkspaceRoot, sourceFullPath) || !fileIo.FileExists(sourceFullPath))
            {
                context.Logger.LogDebug(
                    "Skipping provider-native browser artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because source file {SourcePath} is unavailable.",
                    context.Candidate.RunId,
                    context.Candidate.Step.Id,
                    expectedArtifact.Title,
                    sourceFullPath);
                continue;
            }

            var projectedRelativePath = pathResolver.ResolveScopedManagedRelativePath(context.WorkspaceScope, expectedRelativePath);
            var targetFullPath = Path.GetFullPath(Path.Combine(
                context.WorkspaceRoot,
                projectedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!pathResolver.IsWithinWorkspace(context.WorkspaceRoot, targetFullPath))
            {
                context.Logger.LogWarning(
                    "Skipping provider-native browser artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because target path '{ExpectedPath}' resolves outside the workspace root.",
                    context.Candidate.RunId,
                    context.Candidate.Step.Id,
                    expectedArtifact.Title,
                    projectedRelativePath);
                continue;
            }

            try
            {
                fileIo.EnsureDirectoryForFile(targetFullPath);

                if (!string.Equals(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    fileIo.CopyFile(sourceFullPath, targetFullPath, overwrite: true);
                }

                var content = await fileIo.ReadAllBytesAsync(targetFullPath, context.CancellationToken);
                var syntheticArtifact = new ProcessAutomationExecutionArtifact(
                    Guid.NewGuid(),
                    context.Run.Id,
                    "generated-output",
                    expectedArtifact.Title,
                    projectedRelativePath,
                    artifactClassifier.GuessContentTypeFromPath(targetFullPath),
                    requiredToolName,
                    $"Projected provider-native browser output '{matchedOutputFileName}' into the required managed artifact path.",
                    DateTimeOffset.UtcNow);
                var projectionSource = new ProviderNativeBrowserArtifactProjectionSource(
                    context.Run.Id,
                    projectedRelativePath,
                    matchedOutputFileName,
                    requiredToolName);
                var projectionPlan = ProviderNativeBrowserArtifactProjectionSourceAdapter.PlanExpectedOutput(
                    projectionSource,
                    expectedArtifact,
                    context.CompletionStatus,
                    context.RecoveryContext);
                if (context.Candidate.MutableState.ExternalReferenceKeys.Contains(projectionPlan.ExternalReferenceKey))
                {
                    continue;
                }

                var writeResult = await context.WriteCoordinator.WriteAsync(
                    new ProcessArtifactProjectionWriteRequest(
                        context.Candidate.RunId,
                        context.Candidate.Step.Id,
                        context.Candidate.ProjectId,
                        projectionPlan,
                        Path.GetFileName(targetFullPath),
                        syntheticArtifact.ContentType,
                        content,
                        artifactClassifier.ResolveStorageContentKind(syntheticArtifact.ContentType, targetFullPath),
                        artifactClassifier.BuildStorageRelativePath(context.Candidate, syntheticArtifact)),
                    context.CancellationToken);

                if (!candidateState.TryApplyExpectedWriteOutcome(
                        context.Candidate.MutableState,
                        expectedArtifact,
                        writeResult,
                        out var errorSummary))
                {
                    context.Logger.LogWarning(
                        "Provider-native browser artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                        context.Candidate.RunId,
                        context.Candidate.Step.Id,
                        expectedArtifact.Title,
                        errorSummary);
                }
            }
            catch (Exception exception)
            {
                context.Logger.LogWarning(
                    exception,
                    "Provider-native browser artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}.",
                    context.Candidate.RunId,
                    context.Candidate.Step.Id,
                    expectedArtifact.Title);
            }
        }
    }

    private async Task ProjectDiscoveredOutputsAsync(
        ProcessArtifactProjectionContext context,
        IReadOnlyDictionary<string, IReadOnlyList<string>> browserOutputsByToolName,
        string browserWorkingDirectory)
    {
        foreach (var pair in browserOutputsByToolName)
        {
            foreach (var outputFileName in pair.Value)
            {
                var normalizedOutputPath = WorkspaceScopeDescriptor.NormalizeRelativePath(outputFileName);
                if (!browserOutputRules.IsProviderNativeBrowserArtifactPath(normalizedOutputPath))
                {
                    continue;
                }

                var projectedRelativePath = pathResolver.ResolveProviderNativeBrowserProjectedRelativePath(
                    context.Candidate,
                    context.WorkspaceScope,
                    normalizedOutputPath);
                var projectionSource = new ProviderNativeBrowserArtifactProjectionSource(
                    context.Run.Id,
                    projectedRelativePath,
                    normalizedOutputPath,
                    pair.Key);
                var externalReferenceKey = ProviderNativeBrowserArtifactProjectionSourceAdapter.BuildExternalReferenceKey(
                    projectionSource,
                    context.RecoveryContext);
                if (context.Candidate.MutableState.ExternalReferenceKeys.Contains(externalReferenceKey))
                {
                    continue;
                }

                var sourceFullPath = Path.GetFullPath(Path.Combine(
                    browserWorkingDirectory,
                    normalizedOutputPath.Replace('/', Path.DirectorySeparatorChar)));
                if (!pathResolver.IsWithinWorkspace(context.WorkspaceRoot, sourceFullPath) || !fileIo.FileExists(sourceFullPath))
                {
                    context.Logger.LogDebug(
                        "Skipping provider-native browser output projection for run {RunId}, step {StepRunId} because source file {SourcePath} is unavailable.",
                        context.Candidate.RunId,
                        context.Candidate.Step.Id,
                        sourceFullPath);
                    continue;
                }

                var targetFullPath = Path.GetFullPath(Path.Combine(
                    context.WorkspaceRoot,
                    projectedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!pathResolver.IsWithinWorkspace(context.WorkspaceRoot, targetFullPath))
                {
                    context.Logger.LogWarning(
                        "Skipping provider-native browser output projection for run {RunId}, step {StepRunId} because target path '{ProjectedPath}' resolves outside the workspace root.",
                        context.Candidate.RunId,
                        context.Candidate.Step.Id,
                        projectedRelativePath);
                    continue;
                }

                try
                {
                    fileIo.EnsureDirectoryForFile(targetFullPath);

                    if (!string.Equals(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        fileIo.CopyFile(sourceFullPath, targetFullPath, overwrite: true);
                    }

                    var content = await fileIo.ReadAllBytesAsync(targetFullPath, context.CancellationToken);
                    var contentType = artifactClassifier.GuessContentTypeFromPath(targetFullPath);
                    var syntheticArtifact = new ProcessAutomationExecutionArtifact(
                        Guid.NewGuid(),
                        context.Run.Id,
                        "generated-output",
                        Path.GetFileName(projectedRelativePath),
                        projectedRelativePath,
                        contentType,
                        pair.Key,
                        $"Projected provider-native browser output '{normalizedOutputPath}' into the scoped managed artifact path.",
                        DateTimeOffset.UtcNow);
                    var matchedExpectation = expectationMatcher.ResolveArtifactExpectation(
                        context.Candidate,
                        context.Run.InputSummary,
                        syntheticArtifact);
                    var recordExpectation = matchedExpectation is not null &&
                                            !context.Candidate.MutableState.RecordedArtifactExpectationIds.Contains(matchedExpectation.Id)
                        ? matchedExpectation
                        : null;
                    var projectionPlan = ProviderNativeBrowserArtifactProjectionSourceAdapter.PlanDiscoveredOutput(
                        projectionSource,
                        recordExpectation is null ? null : recordExpectation,
                        ProcessArtifactKind.Evidence,
                        browserOutputRules.BuildProviderNativeBrowserArtifactTitle(syntheticArtifact),
                        context.CompletionStatus,
                        context.RecoveryContext);

                    var writeResult = await context.WriteCoordinator.WriteAsync(
                        new ProcessArtifactProjectionWriteRequest(
                            context.Candidate.RunId,
                            context.Candidate.Step.Id,
                            context.Candidate.ProjectId,
                            projectionPlan,
                            Path.GetFileName(targetFullPath),
                            contentType,
                            content,
                            artifactClassifier.ResolveStorageContentKind(contentType, targetFullPath),
                            projectedRelativePath),
                        context.CancellationToken);

                    if (!candidateState.TryApplyWriteOutcome(
                            context.Candidate.MutableState,
                            writeResult,
                            recordExpectation?.Id,
                            out var errorSummary))
                    {
                        context.Logger.LogWarning(
                            "Provider-native browser output projection failed for run {RunId}, step {StepRunId}, output {OutputPath}. Errors: {Errors}",
                            context.Candidate.RunId,
                            context.Candidate.Step.Id,
                            normalizedOutputPath,
                            errorSummary);
                    }
                }
                catch (Exception exception)
                {
                    context.Logger.LogWarning(
                        exception,
                        "Provider-native browser output projection failed for run {RunId}, step {StepRunId}, output {OutputPath}.",
                        context.Candidate.RunId,
                        context.Candidate.Step.Id,
                        normalizedOutputPath);
                }
            }
        }
    }
}
