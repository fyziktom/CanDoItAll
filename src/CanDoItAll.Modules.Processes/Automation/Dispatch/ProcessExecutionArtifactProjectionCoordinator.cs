using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using System.Text;

using DispatchArtifactExpectation = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchArtifactExpectation;
using ProcessMockArtifactProjection = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ProcessMockArtifactProjection;
using SessionFileContent = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.SessionFileContent;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessExecutionArtifactProjectionCoordinator : IProcessArtifactProjectionSourceCoordinator
{
    private readonly IProcessArtifactProjectionHost host;

    public ProcessExecutionArtifactProjectionCoordinator(IProcessArtifactProjectionHost host)
    {
        this.host = host;
    }

    public async Task ProjectAsync(ProcessArtifactProjectionContext context)
    {
        foreach (var artifact in context.Detail.Artifacts)
        {
            await host.EnsureStepDispatchClaimHeldAsync(context.DispatchClaim, context.CancellationToken);
            if (host.IsTransientExecutionArtifact(artifact))
            {
                context.Logger.LogDebug(
                    "Skipping transient execution artifact projection for run {RunId}, step {StepRunId}, artifact {ArtifactId}, path {RelativePath}.",
                    context.Candidate.Run.Id,
                    context.Candidate.StepRun.Id,
                    artifact.Id,
                    artifact.RelativePath);
                continue;
            }

            var sourceExternalReferenceKey = ProcessArtifactProjectionPlanner.BuildExecutionArtifactExternalReferenceKey(artifact.Id);
            var externalReferenceKey = ProcessArtifactProjectionLineageBuilder.ApplyRecoveryLineage(
                sourceExternalReferenceKey,
                context.Detail.Run.Id,
                context.RecoveryContext);
            if (context.Candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            if (!host.TryResolveArtifactFullPath(context.WorkspaceRoot, artifact.RelativePath, out var fullPath, out var pathResolutionFailure) ||
                !File.Exists(fullPath))
            {
                context.Logger.LogDebug(
                    "Skipping execution artifact projection for run {RunId}, step {StepRunId}, artifact {ArtifactId} because the file path is unavailable. Reason: {Reason}",
                    context.Candidate.Run.Id,
                    context.Candidate.StepRun.Id,
                    artifact.Id,
                    string.IsNullOrWhiteSpace(pathResolutionFailure) ? "File does not exist." : pathResolutionFailure);
                continue;
            }

            byte[] content;
            try
            {
                content = await File.ReadAllBytesAsync(fullPath, context.CancellationToken);
            }
            catch (Exception exception)
            {
                context.Logger.LogWarning(
                    exception,
                    "Execution artifact {ArtifactId} could not be read for process run {RunId}.",
                    artifact.Id,
                    context.Candidate.Run.Id);
                continue;
            }

            var matchedExpectation = host.ResolveArtifactExpectation(
                context.Candidate,
                context.Detail.Run.InputSummary,
                artifact,
                host.TryDecodeTextArtifactContent(artifact, fullPath, content));
            var projectionPlan = ProcessArtifactProjectionPlanner.PlanExecutionArtifact(
                context.Detail.Run.Id,
                artifact,
                matchedExpectation is null ? null : ProcessArtifactValidationSnapshotBuilder.ToProjectionExpectation(matchedExpectation),
                host.ResolveProcessArtifactKind(context.Candidate, artifact),
                context.CompletionStatus,
                context.Detail.Run.ResultSummary,
                context.RecoveryContext);

            var writeResult = await context.WriteCoordinator.WriteAsync(
                new ProcessArtifactProjectionWriteRequest(
                    context.Candidate.Run.Id,
                    context.Candidate.StepRun.Id,
                    context.Candidate.Run.ProjectId,
                    projectionPlan,
                    Path.GetFileName(fullPath),
                    string.IsNullOrWhiteSpace(artifact.ContentType)
                        ? "application/octet-stream"
                        : artifact.ContentType,
                    content,
                    host.ResolveStorageContentKind(artifact.ContentType, fullPath),
                    host.BuildStorageRelativePath(context.Candidate, artifact)),
                context.CancellationToken);
            if (!ProcessArtifactProjectionCandidateState.TryApplyWriteOutcome(
                    context.Candidate,
                    writeResult,
                    projectionPlan.ArtifactExpectationId,
                    out var errorSummary))
            {
                context.Logger.LogWarning(
                    "Process artifact projection failed for run {RunId}, step {StepRunId}, artifact {ArtifactId}. Errors: {Errors}",
                    context.Candidate.Run.Id,
                    context.Candidate.StepRun.Id,
                    artifact.Id,
                    errorSummary);
            }
        }
    }
}
