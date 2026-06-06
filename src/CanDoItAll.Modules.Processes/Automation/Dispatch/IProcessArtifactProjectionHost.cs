using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;

using ArtifactProjectionLineage = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ArtifactProjectionLineage;
using DispatchArtifactExpectation = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchArtifactExpectation;
using DispatchCandidate = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchCandidate;
using ProcessMockArtifactProjection = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ProcessMockArtifactProjection;
using ProcessStepDispatchClaim = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ProcessStepDispatchClaim;
using SessionFileContent = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.SessionFileContent;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessArtifactProjectionHost
{
    Task EnsureStepDispatchClaimHeldAsync(ProcessStepDispatchClaim dispatchClaim, CancellationToken cancellationToken);

    IReadOnlyList<string> ResolveSuccessfulWorkspaceFileMutationReceiptPaths(ProcessAutomationExecutionRunDetail detail);

    IReadOnlyList<string> ResolveExpectedManagedArtifactRelativePaths(
        DispatchCandidate candidate,
        WorkspaceScopeDescriptor workspaceScope,
        DispatchArtifactExpectation expectedArtifact);

    bool ExistingManagedArtifactFileMatches(
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        DispatchArtifactExpectation expectedArtifact,
        string workspaceRoot,
        string relativePath);

    bool IsTransientExecutionArtifact(ProcessAutomationExecutionArtifact artifact);

    bool HasProjectedArtifactExpectationExternalReference(
        IEnumerable<string> externalReferenceKeys,
        Guid artifactExpectationId);

    string ResolveProviderNativeBrowserProjectedRelativePath(
        DispatchCandidate candidate,
        WorkspaceScopeDescriptor workspaceScope,
        string normalizedOutputPath);

    string BuildProviderNativeBrowserArtifactTitle(ProcessAutomationExecutionArtifact artifact);

    bool IsProviderNativeBrowserArtifactPath(string relativePath);

    bool TryResolveArtifactFullPath(
        string workspaceRoot,
        string relativePath,
        out string fullPath,
        out string failureReason);

    string? TryDecodeTextArtifactContent(
        ProcessAutomationExecutionArtifact artifact,
        string fullPath,
        byte[] content);

    DispatchArtifactExpectation? ResolveArtifactExpectation(
        DispatchCandidate candidate,
        ProcessAutomationExecutionArtifact artifact);

    DispatchArtifactExpectation? ResolveArtifactExpectation(
        DispatchCandidate candidate,
        string projectStructureContractText,
        ProcessAutomationExecutionArtifact artifact);

    DispatchArtifactExpectation? ResolveArtifactExpectation(
        DispatchCandidate candidate,
        string projectStructureContractText,
        ProcessAutomationExecutionArtifact artifact,
        string? artifactTextContent);

    Guid? ResolveArtifactExpectationId(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        ProcessAutomationExecutionArtifact artifact);

    ProcessArtifactKind ResolveProcessArtifactKind(
        DispatchCandidate candidate,
        ProcessAutomationExecutionArtifact artifact);

    StorageContentKind ResolveStorageContentKind(string contentType, string fullPath);

    string BuildStorageRelativePath(
        DispatchCandidate candidate,
        ProcessAutomationExecutionArtifact artifact);

    string GuessContentTypeFromPath(string fullPath);

    IReadOnlyList<ProcessMockArtifactProjection> ResolveProcessMockArtifactProjections(string? serializedSessionStateJson);

    bool ProcessMockArtifactMatchesExpectation(
        DispatchArtifactExpectation expectedArtifact,
        ProcessMockArtifactProjection projection);

    string ResolveScopedManagedRelativePath(
        WorkspaceScopeDescriptor workspaceScope,
        string relativePath);

    IReadOnlyList<SessionFileContent> ResolveSuccessfulSessionFileWrites(string? serializedSessionStateJson);

    bool TryResolveProjectStructureExpectedArtifactPath(
        DispatchCandidate candidate,
        DispatchArtifactExpectation expectedArtifact,
        string projectStructureContractText,
        out string governedPath);

    bool ArtifactPathMatchesGovernedProjectStructurePath(string artifactPath, string governedPath);

    bool WorkspaceWrittenFileMatchesExpectedArtifact(
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        DispatchArtifactExpectation expectedArtifact,
        string path,
        string content);

    string ResolveWorkspaceWrittenArtifactRelativePath(
        WorkspaceScopeDescriptor workspaceScope,
        string writtenPath);

    bool TryResolveWorkspaceWrittenArtifactSourceFullPath(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        string writtenPath,
        string projectedRelativePath,
        out string fullPath,
        out string sourceRelativePath,
        out string failureReason);

    bool ShouldProjectResponseTextArtifacts(
        ProcessAutomationExecutionRunRecord run,
        ProcessStepRunStatus completionStatus);

    string ResolveProjectableResponseArtifactText(string? responseText);

    bool IsUsableProjectedResponseArtifactContent(
        DispatchArtifactExpectation expectedArtifact,
        string responseText);

    bool TryResolveResponseTextArtifactRelativePath(
        DispatchCandidate candidate,
        WorkspaceScopeDescriptor workspaceScope,
        DispatchArtifactExpectation expectedArtifact,
        out string relativePath);

    bool IsWithinWorkspace(string workspaceRoot, string fullPath);

    IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveSuccessfulBrowserToolOutputFiles(
        ProcessAutomationExecutionRunDetail detail);

    string? ResolveProviderNativeBrowserWorkingDirectory(ProcessAutomationExecutionRunDetail detail);

    bool TryExtractExpectedArtifactRelativePath(string validationRequirementSummary, out string relativePath);

    string ResolveProviderNativeBrowserToolName(string expectedRelativePath);

    bool MatchesExpectedBrowserOutputFile(string expectedRelativePath, string outputFileName);

    bool ShouldAutoRecordCompletedDecisionArtifact(DispatchArtifactExpectation expectedArtifact);

    string BuildCompletedDecisionArtifactExternalReferenceKey(Guid stepRunId, Guid artifactExpectationId);

    ProcessArtifactTrustStatus ResolveCompletedDecisionArtifactTrustStatus(ProcessArtifactTrustRequirement trustRequirement);

    string BuildCompletedDecisionArtifactProvenanceSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail);

    string BuildCompletedDecisionArtifactReviewSummary(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string responseText,
        DispatchArtifactExpectation expectedArtifact);

    ProcessArtifactProjectionLineage BuildArtifactProjectionLineage(
        ProcessArtifactProjectionSourceKind sourceKind,
        Guid? sourceExecutionRunId = null,
        ArtifactProjectionLineage? lineage = null,
        Guid? sourceArtifactId = null,
        string sourceExternalReferenceKey = "");
}
