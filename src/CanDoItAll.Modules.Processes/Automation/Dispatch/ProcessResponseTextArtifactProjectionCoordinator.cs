using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using System.Text;


namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessResponseTextArtifactProjectionCoordinator : IProcessArtifactProjectionSourceCoordinator
{
    private readonly IProcessProjectionPathResolver pathResolver;
    private readonly IProcessProjectionFileIo fileIo;
    private readonly IProcessProjectionArtifactClassifier artifactClassifier;
    private readonly IProcessProjectionResponseTextRules responseTextRules;
    private readonly IProcessProjectionExpectationMatcher expectationMatcher;
    private readonly ProcessExistingManagedArtifactProjectionCoordinator existingManagedCoordinator;
    private readonly IProcessProjectionCandidateStateUpdater candidateState;

    public ProcessResponseTextArtifactProjectionCoordinator(
        IProcessProjectionPathResolver pathResolver,
        IProcessProjectionFileIo fileIo,
        IProcessProjectionArtifactClassifier artifactClassifier,
        IProcessProjectionResponseTextRules responseTextRules,
        IProcessProjectionExpectationMatcher expectationMatcher,
        ProcessExistingManagedArtifactProjectionCoordinator existingManagedCoordinator,
        IProcessProjectionCandidateStateUpdater candidateState)
    {
        this.pathResolver = pathResolver;
        this.fileIo = fileIo;
        this.artifactClassifier = artifactClassifier;
        this.responseTextRules = responseTextRules;
        this.expectationMatcher = expectationMatcher;
        this.existingManagedCoordinator = existingManagedCoordinator;
        this.candidateState = candidateState;
    }

    public async Task ProjectAsync(ProcessArtifactProjectionContext context)
    {
        if (!responseTextRules.ShouldProjectResponseTextArtifacts(context.Run, context.CompletionStatus) ||
            context.Candidate.ExpectedArtifacts.Count == 0 ||
            string.IsNullOrWhiteSpace(context.ResponseText))
        {
            return;
        }

        var normalizedResponseText = responseTextRules.ResolveProjectableResponseArtifactText(context.ResponseText).ReplaceLineEndings(Environment.NewLine);
        if (string.IsNullOrWhiteSpace(normalizedResponseText))
        {
            return;
        }

        foreach (var expectedArtifact in context.Candidate.ExpectedArtifacts)
        {
            if (!responseTextRules.IsUsableProjectedResponseArtifactContent(expectedArtifact, normalizedResponseText))
            {
                context.Logger.LogInformation(
                    "Skipping response-text artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because the assistant response is not usable artifact content.",
                    context.Candidate.RunId,
                    context.Candidate.Step.Id,
                    expectedArtifact.Title);
                continue;
            }

            if (!responseTextRules.TryResolveResponseTextArtifactRelativePath(
                    context.Candidate,
                    context.WorkspaceScope,
                    expectedArtifact,
                    out var projectedRelativePath))
            {
                continue;
            }

            if (context.Candidate.MutableState.RecordedArtifactExpectationIds.Contains(expectedArtifact.Id) ||
                context.Run.Artifacts.Any(artifact => expectationMatcher.ResolveArtifactExpectationId(context.Candidate, context.Run, artifact) == expectedArtifact.Id))
            {
                continue;
            }

            var projectionSource = new ResponseTextArtifactProjectionSource(
                context.Run.Id,
                projectedRelativePath);
            var externalReferenceKey = ResponseTextArtifactProjectionSourceAdapter.BuildExternalReferenceKey(
                projectionSource,
                context.RecoveryContext);
            if (context.Candidate.MutableState.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            var targetFullPath = Path.GetFullPath(Path.Combine(
                context.WorkspaceRoot,
                projectedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!pathResolver.IsWithinWorkspace(context.WorkspaceRoot, targetFullPath))
            {
                context.Logger.LogWarning(
                    "Skipping response-text artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because target path '{ExpectedPath}' resolves outside the workspace root.",
                    context.Candidate.RunId,
                    context.Candidate.Step.Id,
                    expectedArtifact.Title,
                    projectedRelativePath);
                continue;
            }

            try
            {
                if (fileIo.FileExists(targetFullPath) &&
                    await existingManagedCoordinator.TryRecordForResponseProjectionAsync(
                        context,
                        expectedArtifact,
                        projectedRelativePath,
                        targetFullPath))
                {
                    continue;
                }

                fileIo.EnsureDirectoryForFile(targetFullPath);

                var persistedResponseText = normalizedResponseText.EndsWith(Environment.NewLine, StringComparison.Ordinal)
                    ? normalizedResponseText
                    : normalizedResponseText + Environment.NewLine;
                await fileIo.WriteAllTextAsync(targetFullPath, persistedResponseText, Encoding.UTF8, context.CancellationToken);

                var content = Encoding.UTF8.GetBytes(persistedResponseText);
                var syntheticArtifact = new ProcessAutomationExecutionArtifact(
                    Guid.NewGuid(),
                    context.Run.Id,
                    "generated-output",
                    expectedArtifact.Title,
                    projectedRelativePath,
                    artifactClassifier.GuessContentTypeFromPath(targetFullPath),
                    "assistant-response",
                    "Projected the final assistant response into the required managed text artifact path.",
                    DateTimeOffset.UtcNow);
                var projectionPlan = ResponseTextArtifactProjectionSourceAdapter.Plan(
                    projectionSource,
                    expectedArtifact,
                    context.CompletionStatus,
                    context.RecoveryContext);

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
                        "Response-text artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
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
                    "Response-text artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}.",
                    context.Candidate.RunId,
                    context.Candidate.Step.Id,
                    expectedArtifact.Title);
            }
        }
    }
}
