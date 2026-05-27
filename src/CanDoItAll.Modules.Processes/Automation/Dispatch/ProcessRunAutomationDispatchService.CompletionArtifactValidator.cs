namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal static class ProcessCompletionArtifactValidator
    {
        public static ProcessArtifactExpectationValidationResult ValidateArtifactExpectationForRecordedArtifacts(
            Guid processRunId,
            Guid stepRunId,
            DispatchArtifactExpectation expectation,
            IReadOnlyList<ProcessArtifactRecord> artifacts,
            ProcessStepCompletionExecutorKind executorKind,
            Guid? executionRunId = null,
            Guid? workflowRunId = null,
            Guid? subprocessRunId = null,
            Guid? recoveryExecutionRunId = null,
            Guid? recoveredForExecutionRunId = null,
            IProcessArtifactContentReader? managedArtifactContentReader = null)
        {
            ArgumentNullException.ThrowIfNull(expectation);
            ArgumentNullException.ThrowIfNull(artifacts);

            var mode = ResolveArtifactExpectationMode(expectation);
            var candidateArtifacts = artifacts
                .Where(artifact => IsArtifactCandidateForExpectation(expectation, artifact))
                .OrderBy(artifact => ResolveArtifactCandidatePriority(expectation, artifact))
                .ThenByDescending(artifact => artifact.CreatedAtUtc)
                .ToList();

            if (candidateArtifacts.Count == 0)
            {
                return CreateArtifactValidationResult(
                    processRunId,
                    stepRunId,
                    expectation,
                    mode,
                    ProcessArtifactValidationStatus.Missing,
                    ProcessArtifactProducerKind.Unknown,
                    null,
                    string.Empty,
                    "No current step artifact record matches the required expectation.",
                    "Recover or block with the exact missing artifact.",
                    executorKind,
                    executionRunId,
                    workflowRunId,
                    subprocessRunId,
                    recoveryExecutionRunId,
                    recoveredForExecutionRunId);
            }

            ProcessArtifactExpectationValidationResult? firstFailure = null;
            foreach (var artifact in candidateArtifacts)
            {
                var result = ValidateArtifactCandidate(
                    processRunId,
                    stepRunId,
                    expectation,
                    mode,
                    artifact,
                    executorKind,
                    executionRunId,
                    workflowRunId,
                    subprocessRunId,
                    recoveryExecutionRunId,
                    recoveredForExecutionRunId,
                    managedArtifactContentReader);
                if (result.IsSatisfied)
                {
                    return result;
                }

                firstFailure ??= result;
            }

            return firstFailure!;
        }

        private static ProcessArtifactExpectationValidationResult ValidateArtifactCandidate(
            Guid processRunId,
            Guid stepRunId,
            DispatchArtifactExpectation expectation,
            ProcessArtifactExpectationMode mode,
            ProcessArtifactRecord artifact,
            ProcessStepCompletionExecutorKind executorKind,
            Guid? executionRunId,
            Guid? workflowRunId,
            Guid? subprocessRunId,
            Guid? recoveryExecutionRunId,
            Guid? recoveredForExecutionRunId,
            IProcessArtifactContentReader? managedArtifactContentReader)
        {
            var producerKind = ResolveArtifactProducerKind(artifact);
            if (ContainsPlaceholderArtifactSignal(artifact, mode))
            {
                return CreateArtifactValidationResult(
                    processRunId,
                    stepRunId,
                    expectation,
                    mode,
                    ProcessArtifactValidationStatus.PlaceholderOnly,
                    producerKind,
                    artifact,
                    artifact.ManagedStoragePath,
                    "The candidate artifact is a placeholder, gap marker, or missing-artifact diagnostic.",
                    "Produce a real current-run artifact or block with the evidence gap.",
                    executorKind,
                    executionRunId,
                    workflowRunId,
                    subprocessRunId,
                    recoveryExecutionRunId,
                    recoveredForExecutionRunId);
            }

            if (!IsProducerAllowedForMode(mode, producerKind, expectation))
            {
                return CreateArtifactValidationResult(
                    processRunId,
                    stepRunId,
                    expectation,
                    mode,
                    ProcessArtifactValidationStatus.WrongProducerMode,
                    producerKind,
                    artifact,
                    artifact.ManagedStoragePath,
                    $"Producer {producerKind} is not allowed to satisfy {mode} artifact expectations.",
                    "Recover from an allowed producer or block with an exact producer-mode diagnostic.",
                    executorKind,
                    executionRunId,
                    workflowRunId,
                    subprocessRunId,
                    recoveryExecutionRunId,
                    recoveredForExecutionRunId);
            }

            if (!IsCurrentRunArtifact(
                    artifact,
                    producerKind,
                    processRunId,
                    stepRunId,
                    executionRunId,
                    workflowRunId,
                    subprocessRunId,
                    recoveryExecutionRunId,
                    recoveredForExecutionRunId))
            {
                return CreateArtifactValidationResult(
                    processRunId,
                    stepRunId,
                    expectation,
                    mode,
                    ProcessArtifactValidationStatus.StaleOrWrongRun,
                    producerKind,
                    artifact,
                    artifact.ManagedStoragePath,
                    "The candidate artifact is not bound to the current process run, step, execution run, or workflow run.",
                    "Recover using current-run evidence or block instead of carrying stale artifacts forward.",
                    executorKind,
                    executionRunId,
                    workflowRunId,
                    subprocessRunId,
                    recoveryExecutionRunId,
                    recoveredForExecutionRunId);
            }

            if (RequiresManagedEvidencePath(mode, producerKind) && string.IsNullOrWhiteSpace(artifact.ManagedStoragePath))
            {
                return CreateArtifactValidationResult(
                    processRunId,
                    stepRunId,
                    expectation,
                    mode,
                    ProcessArtifactValidationStatus.InsufficientEvidence,
                    producerKind,
                    artifact,
                    artifact.ManagedStoragePath,
                    "The candidate artifact has no managed storage path for a file-backed expectation.",
                    "Write or recover a durable managed artifact with current-run provenance.",
                    executorKind,
                    executionRunId,
                    workflowRunId,
                    subprocessRunId,
                    recoveryExecutionRunId,
                    recoveredForExecutionRunId);
            }

            var requiresStoredContent = RequiresStoredArtifactContent(expectation, artifact, mode, producerKind);
            if (managedArtifactContentReader is not null &&
                requiresStoredContent &&
                !TryValidateManagedArtifactContent(
                    artifact,
                    managedArtifactContentReader,
                    out var contentDiagnostic,
                    out var contentValidationStatus))
            {
                return CreateArtifactValidationResult(
                    processRunId,
                    stepRunId,
                    expectation,
                    mode,
                    contentValidationStatus,
                    producerKind,
                    artifact,
                    artifact.ManagedStoragePath,
                    contentDiagnostic,
                    "Recover a durable managed artifact with readable current-run content and matching lineage.",
                    executorKind,
                    executionRunId,
                    workflowRunId,
                    subprocessRunId,
                    recoveryExecutionRunId,
                    recoveredForExecutionRunId);
            }

            if (!MatchesDeclaredFormat(expectation, artifact, mode, producerKind, requiresStoredContent, managedArtifactContentReader, out var formatDiagnostic))
            {
                return CreateArtifactValidationResult(
                    processRunId,
                    stepRunId,
                    expectation,
                    mode,
                    ProcessArtifactValidationStatus.InvalidFormat,
                    producerKind,
                    artifact,
                    artifact.ManagedStoragePath,
                    formatDiagnostic,
                    "Regenerate the artifact in the declared format or block with the format mismatch.",
                    executorKind,
                    executionRunId,
                    workflowRunId,
                    subprocessRunId,
                    recoveryExecutionRunId,
                    recoveredForExecutionRunId);
            }

            return CreateArtifactValidationResult(
                processRunId,
                stepRunId,
                expectation,
                mode,
                ProcessArtifactValidationStatus.Satisfied,
                producerKind,
                artifact,
                artifact.ManagedStoragePath,
                "Required artifact expectation is satisfied by a current-run, mode-compatible artifact.",
                "Complete",
                executorKind,
                executionRunId,
                workflowRunId,
                subprocessRunId,
                recoveryExecutionRunId,
                recoveredForExecutionRunId);
        }
    }
}
