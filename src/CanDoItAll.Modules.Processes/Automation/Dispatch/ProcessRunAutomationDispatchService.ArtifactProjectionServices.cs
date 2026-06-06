using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private sealed class ProcessArtifactProjectionServices :
        IProcessProjectionClaimGuard,
        IProcessProjectionPathResolver,
        IProcessProjectionFileIo,
        IProcessProjectionArtifactClassifier,
        IProcessProjectionExpectationMatcher,
        IProcessProjectionProcessMockRules,
        IProcessProjectionProjectStructureMatcher,
        IProcessProjectionSessionObservationSource,
        IProcessProjectionResponseTextRules,
        IProcessProjectionBrowserOutputRules,
        IProcessProjectionDecisionArtifactRules,
        IProcessProjectionLineageFactory,
        IProcessProjectionCandidateStateUpdater
    {
        private readonly ProcessRunAutomationDispatchService dispatchService;

        public ProcessArtifactProjectionServices(ProcessRunAutomationDispatchService dispatchService)
        {
            this.dispatchService = dispatchService;
        }

        public bool FileExists(string fullPath)
        {
            return File.Exists(fullPath);
        }

        public Task<byte[]> ReadAllBytesAsync(string fullPath, CancellationToken cancellationToken)
        {
            return File.ReadAllBytesAsync(fullPath, cancellationToken);
        }

        public Task WriteAllTextAsync(
            string fullPath,
            string contents,
            Encoding encoding,
            CancellationToken cancellationToken)
        {
            return File.WriteAllTextAsync(fullPath, contents, encoding, cancellationToken);
        }

        public void CopyFile(string sourceFullPath, string targetFullPath, bool overwrite)
        {
            File.Copy(sourceFullPath, targetFullPath, overwrite);
        }

        public void EnsureDirectoryForFile(string fullPath)
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public Task EnsureStepDispatchClaimHeldAsync(
            ProcessStepDispatchClaim dispatchClaim,
            CancellationToken cancellationToken)
        {
            return dispatchService.EnsureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
        }

        public IReadOnlyList<string> ResolveSuccessfulWorkspaceFileMutationReceiptPaths(
            ProcessAutomationExecutionRunDetail detail)
        {
            return ProcessRunAutomationDispatchService.ResolveSuccessfulWorkspaceFileMutationReceiptPaths(detail);
        }

        public IReadOnlyList<string> ResolveExpectedManagedArtifactRelativePaths(
            DispatchCandidate candidate,
            WorkspaceScopeDescriptor workspaceScope,
            DispatchArtifactExpectation expectedArtifact)
        {
            return ProcessRunAutomationDispatchService.ResolveExpectedManagedArtifactRelativePaths(
                candidate,
                workspaceScope,
                expectedArtifact);
        }

        public bool ExistingManagedArtifactFileMatches(
            IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
            DispatchArtifactExpectation expectedArtifact,
            string workspaceRoot,
            string relativePath)
        {
            return ProcessRunAutomationDispatchService.ExistingManagedArtifactFileMatches(
                expectedArtifacts,
                expectedArtifact,
                workspaceRoot,
                relativePath);
        }

        public bool IsTransientExecutionArtifact(ProcessAutomationExecutionArtifact artifact)
        {
            return ProcessRunAutomationDispatchService.IsTransientExecutionArtifact(artifact);
        }

        public bool HasProjectedArtifactExpectationExternalReference(
            IEnumerable<string> externalReferenceKeys,
            Guid artifactExpectationId)
        {
            return ProcessRunAutomationDispatchService.HasProjectedArtifactExpectationExternalReference(
                externalReferenceKeys,
                artifactExpectationId);
        }

        public string ResolveProviderNativeBrowserProjectedRelativePath(
            DispatchCandidate candidate,
            WorkspaceScopeDescriptor workspaceScope,
            string normalizedOutputPath)
        {
            return ProcessRunAutomationDispatchService.ResolveProviderNativeBrowserProjectedRelativePath(
                candidate,
                workspaceScope,
                normalizedOutputPath);
        }

        public string BuildProviderNativeBrowserArtifactTitle(ProcessAutomationExecutionArtifact artifact)
        {
            return ProcessRunAutomationDispatchService.BuildProviderNativeBrowserArtifactTitle(artifact);
        }

        public bool IsProviderNativeBrowserArtifactPath(string relativePath)
        {
            return ProcessRunAutomationDispatchService.IsProviderNativeBrowserArtifactPath(relativePath);
        }

        public bool TryResolveArtifactFullPath(
            string workspaceRoot,
            string relativePath,
            out string fullPath,
            out string failureReason)
        {
            return ProcessRunAutomationDispatchService.TryResolveArtifactFullPath(
                workspaceRoot,
                relativePath,
                out fullPath,
                out failureReason);
        }

        public string? TryDecodeTextArtifactContent(
            ProcessAutomationExecutionArtifact artifact,
            string fullPath,
            byte[] content)
        {
            return ProcessRunAutomationDispatchService.TryDecodeTextArtifactContent(artifact, fullPath, content);
        }

        public DispatchArtifactExpectation? ResolveArtifactExpectation(
            DispatchCandidate candidate,
            ProcessAutomationExecutionArtifact artifact)
        {
            return ProcessRunAutomationDispatchService.ResolveArtifactExpectation(candidate, artifact);
        }

        public DispatchArtifactExpectation? ResolveArtifactExpectation(
            DispatchCandidate candidate,
            string projectStructureContractText,
            ProcessAutomationExecutionArtifact artifact)
        {
            return ProcessRunAutomationDispatchService.ResolveArtifactExpectation(
                candidate,
                projectStructureContractText,
                artifact);
        }

        public DispatchArtifactExpectation? ResolveArtifactExpectation(
            DispatchCandidate candidate,
            string projectStructureContractText,
            ProcessAutomationExecutionArtifact artifact,
            string? artifactTextContent)
        {
            return ProcessRunAutomationDispatchService.ResolveArtifactExpectation(
                candidate,
                projectStructureContractText,
                artifact,
                artifactTextContent);
        }

        public Guid? ResolveArtifactExpectationId(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail,
            ProcessAutomationExecutionArtifact artifact)
        {
            return ProcessRunAutomationDispatchService.ResolveArtifactExpectationId(candidate, detail, artifact);
        }

        public ProcessArtifactKind ResolveProcessArtifactKind(
            DispatchCandidate candidate,
            ProcessAutomationExecutionArtifact artifact)
        {
            return ProcessRunAutomationDispatchService.ResolveProcessArtifactKind(candidate, artifact);
        }

        public StorageContentKind ResolveStorageContentKind(string contentType, string fullPath)
        {
            return ProcessRunAutomationDispatchService.ResolveStorageContentKind(contentType, fullPath);
        }

        public string BuildStorageRelativePath(
            DispatchCandidate candidate,
            ProcessAutomationExecutionArtifact artifact)
        {
            return ProcessRunAutomationDispatchService.BuildStorageRelativePath(candidate, artifact);
        }

        public string GuessContentTypeFromPath(string fullPath)
        {
            return ProcessRunAutomationDispatchService.GuessContentTypeFromPath(fullPath);
        }

        public IReadOnlyList<ProcessMockArtifactProjection> ResolveProcessMockArtifactProjections(
            string? serializedSessionStateJson)
        {
            return ProcessRunAutomationDispatchService.ResolveProcessMockArtifactProjections(serializedSessionStateJson);
        }

        public bool ProcessMockArtifactMatchesExpectation(
            DispatchArtifactExpectation expectedArtifact,
            ProcessMockArtifactProjection projection)
        {
            return ProcessRunAutomationDispatchService.ProcessMockArtifactMatchesExpectation(
                expectedArtifact,
                projection);
        }

        public string ResolveScopedManagedRelativePath(
            WorkspaceScopeDescriptor workspaceScope,
            string relativePath)
        {
            return ProcessRunAutomationDispatchService.ResolveScopedManagedRelativePath(workspaceScope, relativePath);
        }

        public IReadOnlyList<SessionFileContent> ResolveSuccessfulSessionFileWrites(string? serializedSessionStateJson)
        {
            return ProcessRunAutomationDispatchService.ResolveSuccessfulSessionFileWrites(serializedSessionStateJson);
        }

        public bool TryResolveProjectStructureExpectedArtifactPath(
            DispatchCandidate candidate,
            DispatchArtifactExpectation expectedArtifact,
            string projectStructureContractText,
            out string governedPath)
        {
            return ProcessRunAutomationDispatchService.TryResolveProjectStructureExpectedArtifactPath(
                candidate,
                expectedArtifact,
                projectStructureContractText,
                out governedPath);
        }

        public bool ArtifactPathMatchesGovernedProjectStructurePath(string artifactPath, string governedPath)
        {
            return ProcessRunAutomationDispatchService.ArtifactPathMatchesGovernedProjectStructurePath(
                artifactPath,
                governedPath);
        }

        public bool WorkspaceWrittenFileMatchesExpectedArtifact(
            IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
            DispatchArtifactExpectation expectedArtifact,
            string path,
            string content)
        {
            return ProcessRunAutomationDispatchService.WorkspaceWrittenFileMatchesExpectedArtifact(
                expectedArtifacts,
                expectedArtifact,
                path,
                content);
        }

        public string ResolveWorkspaceWrittenArtifactRelativePath(
            WorkspaceScopeDescriptor workspaceScope,
            string writtenPath)
        {
            return ProcessRunAutomationDispatchService.ResolveWorkspaceWrittenArtifactRelativePath(
                workspaceScope,
                writtenPath);
        }

        public bool TryResolveWorkspaceWrittenArtifactSourceFullPath(
            string workspaceRoot,
            WorkspaceScopeDescriptor workspaceScope,
            string writtenPath,
            string projectedRelativePath,
            out string fullPath,
            out string sourceRelativePath,
            out string failureReason)
        {
            return ProcessRunAutomationDispatchService.TryResolveWorkspaceWrittenArtifactSourceFullPath(
                workspaceRoot,
                workspaceScope,
                writtenPath,
                projectedRelativePath,
                out fullPath,
                out sourceRelativePath,
                out failureReason);
        }

        public bool ShouldProjectResponseTextArtifacts(
            ProcessAutomationExecutionRunRecord run,
            ProcessStepRunStatus completionStatus)
        {
            return ProcessRunAutomationDispatchService.ShouldProjectResponseTextArtifacts(run, completionStatus);
        }

        public string ResolveProjectableResponseArtifactText(string? responseText)
        {
            return ProcessRunAutomationDispatchService.ResolveProjectableResponseArtifactText(responseText);
        }

        public bool IsUsableProjectedResponseArtifactContent(
            DispatchArtifactExpectation expectedArtifact,
            string responseText)
        {
            return ProcessRunAutomationDispatchService.IsUsableProjectedResponseArtifactContent(
                expectedArtifact,
                responseText);
        }

        public bool TryResolveResponseTextArtifactRelativePath(
            DispatchCandidate candidate,
            WorkspaceScopeDescriptor workspaceScope,
            DispatchArtifactExpectation expectedArtifact,
            out string relativePath)
        {
            return ProcessRunAutomationDispatchService.TryResolveResponseTextArtifactRelativePath(
                candidate,
                workspaceScope,
                expectedArtifact,
                out relativePath);
        }

        public bool IsWithinWorkspace(string workspaceRoot, string fullPath)
        {
            return ProcessRunAutomationDispatchService.IsWithinWorkspace(workspaceRoot, fullPath);
        }

        public IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveSuccessfulBrowserToolOutputFiles(
            ProcessAutomationExecutionRunDetail detail)
        {
            return ProcessRunAutomationDispatchService.ResolveSuccessfulBrowserToolOutputFiles(detail);
        }

        public string? ResolveProviderNativeBrowserWorkingDirectory(ProcessAutomationExecutionRunDetail detail)
        {
            return ProcessRunAutomationDispatchService.ResolveProviderNativeBrowserWorkingDirectory(detail);
        }

        public bool TryExtractExpectedArtifactRelativePath(
            string validationRequirementSummary,
            out string relativePath)
        {
            return ProcessRunAutomationDispatchService.TryExtractExpectedArtifactRelativePath(
                validationRequirementSummary,
                out relativePath);
        }

        public string ResolveProviderNativeBrowserToolName(string expectedRelativePath)
        {
            return ProcessRunAutomationDispatchService.ResolveProviderNativeBrowserToolName(expectedRelativePath);
        }

        public bool MatchesExpectedBrowserOutputFile(string expectedRelativePath, string outputFileName)
        {
            return ProcessRunAutomationDispatchService.MatchesExpectedBrowserOutputFile(
                expectedRelativePath,
                outputFileName);
        }

        public bool ShouldAutoRecordCompletedDecisionArtifact(DispatchArtifactExpectation expectedArtifact)
        {
            return ProcessRunAutomationDispatchService.ShouldAutoRecordCompletedDecisionArtifact(expectedArtifact);
        }

        public string BuildCompletedDecisionArtifactExternalReferenceKey(Guid stepRunId, Guid artifactExpectationId)
        {
            return ProcessRunAutomationDispatchService.BuildCompletedDecisionArtifactExternalReferenceKey(
                stepRunId,
                artifactExpectationId);
        }

        public ProcessArtifactTrustStatus ResolveCompletedDecisionArtifactTrustStatus(
            ProcessArtifactTrustRequirement trustRequirement)
        {
            return ProcessRunAutomationDispatchService.ResolveCompletedDecisionArtifactTrustStatus(trustRequirement);
        }

        public string BuildCompletedDecisionArtifactProvenanceSummary(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail)
        {
            return ProcessRunAutomationDispatchService.BuildCompletedDecisionArtifactProvenanceSummary(
                candidate,
                detail);
        }

        public string BuildCompletedDecisionArtifactReviewSummary(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail,
            string responseText,
            DispatchArtifactExpectation expectedArtifact)
        {
            return ProcessRunAutomationDispatchService.BuildCompletedDecisionArtifactReviewSummary(
                candidate,
                detail,
                responseText,
                expectedArtifact);
        }

        public ProcessArtifactProjectionLineage BuildArtifactProjectionLineage(
            ProcessArtifactProjectionSourceKind sourceKind,
            Guid? sourceExecutionRunId = null,
            ArtifactProjectionLineage? lineage = null,
            Guid? sourceArtifactId = null,
            string sourceExternalReferenceKey = "")
        {
            return ProcessRunAutomationDispatchService.BuildArtifactProjectionLineage(
                sourceKind,
                sourceExecutionRunId,
                lineage,
                sourceArtifactId,
                sourceExternalReferenceKey);
        }

        public bool TryApplyExpectedWriteOutcome(
            DispatchCandidate candidate,
            DispatchArtifactExpectation expectedArtifact,
            Result<ProcessArtifactProjectionWriteResult> writeResult,
            out string errorSummary)
        {
            return ProcessArtifactProjectionCandidateState.TryApplyExpectedWriteOutcome(
                candidate,
                expectedArtifact,
                writeResult,
                out errorSummary);
        }

        public bool TryApplyWriteOutcome(
            DispatchCandidate candidate,
            Result<ProcessArtifactProjectionWriteResult> writeResult,
            Guid? expectedArtifactId,
            out string errorSummary)
        {
            return ProcessArtifactProjectionCandidateState.TryApplyWriteOutcome(
                candidate,
                writeResult,
                expectedArtifactId,
                out errorSummary);
        }

        public bool TryApplyExpectedRecordOnlyOutcome(
            DispatchCandidate candidate,
            DispatchArtifactExpectation expectedArtifact,
            Result<ProcessArtifactProjectionRecordOnlyResult> recordResult,
            out string errorSummary)
        {
            return ProcessArtifactProjectionCandidateState.TryApplyExpectedRecordOnlyOutcome(
                candidate,
                expectedArtifact,
                recordResult,
                out errorSummary);
        }
    }
}
