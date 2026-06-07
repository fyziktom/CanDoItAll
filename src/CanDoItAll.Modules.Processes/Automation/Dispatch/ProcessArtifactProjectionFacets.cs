using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessArtifactProjectionFacetSet(
    IProcessProjectionClaimGuard ClaimGuard,
    IProcessProjectionPathResolver PathResolver,
    IProcessProjectionFileIo FileIo,
    IProcessProjectionArtifactClassifier ArtifactClassifier,
    IProcessProjectionExpectationMatcher ExpectationMatcher,
    IProcessProjectionProcessMockRules ProcessMockRules,
    IProcessProjectionProjectStructureMatcher ProjectStructureMatcher,
    IProcessProjectionSessionObservationSource SessionObservationSource,
    IProcessProjectionResponseTextRules ResponseTextRules,
    IProcessProjectionBrowserOutputRules BrowserOutputRules,
    IProcessProjectionDecisionArtifactRules DecisionArtifactRules,
    IProcessProjectionLineageFactory LineageFactory,
    IProcessProjectionCandidateStateUpdater CandidateState);

internal interface IProcessProjectionClaimGuard
{
    Task EnsureStepDispatchClaimHeldAsync(
        ProcessProjectionStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);
}

internal interface IProcessProjectionPathResolver
{
    bool TryResolveArtifactFullPath(
        string workspaceRoot,
        string relativePath,
        out string fullPath,
        out string failureReason);

    string ResolveScopedManagedRelativePath(
        WorkspaceScopeDescriptor workspaceScope,
        string relativePath);

    IReadOnlyList<string> ResolveExpectedManagedArtifactRelativePaths(
        ProcessProjectionCandidateSnapshot candidate,
        WorkspaceScopeDescriptor workspaceScope,
        ProcessArtifactExpectationSnapshot expectedArtifact);

    string ResolveProviderNativeBrowserProjectedRelativePath(
        ProcessProjectionCandidateSnapshot candidate,
        WorkspaceScopeDescriptor workspaceScope,
        string normalizedOutputPath);

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

    bool IsWithinWorkspace(string workspaceRoot, string fullPath);
}

internal interface IProcessProjectionFileIo
{
    bool FileExists(string fullPath);

    long GetFileLength(string fullPath);

    byte[] ReadAllBytes(string fullPath);

    Task<byte[]> ReadAllBytesAsync(string fullPath, CancellationToken cancellationToken);

    Task WriteAllTextAsync(
        string fullPath,
        string contents,
        Encoding encoding,
        CancellationToken cancellationToken);

    void CopyFile(string sourceFullPath, string targetFullPath, bool overwrite);

    void EnsureDirectoryForFile(string fullPath);
}

internal interface IProcessProjectionArtifactClassifier
{
    bool IsTransientExecutionArtifact(ProcessAutomationExecutionArtifact artifact);

    string? TryDecodeTextArtifactContent(
        ProcessAutomationExecutionArtifact artifact,
        string fullPath,
        byte[] content);

    ProcessArtifactKind ResolveProcessArtifactKind(
        ProcessProjectionCandidateSnapshot candidate,
        ProcessAutomationExecutionArtifact artifact);

    StorageContentKind ResolveStorageContentKind(string contentType, string fullPath);

    string BuildStorageRelativePath(
        ProcessProjectionCandidateSnapshot candidate,
        ProcessAutomationExecutionArtifact artifact);

    string GuessContentTypeFromPath(string fullPath);
}

internal interface IProcessProjectionExpectationMatcher
{
    bool ExistingManagedArtifactFileMatches(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string workspaceRoot,
        string relativePath);

    bool HasProjectedArtifactExpectationExternalReference(
        IEnumerable<string> externalReferenceKeys,
        Guid artifactExpectationId);

    ProcessArtifactExpectationSnapshot? ResolveArtifactExpectation(
        ProcessProjectionCandidateSnapshot candidate,
        ProcessAutomationExecutionArtifact artifact);

    ProcessArtifactExpectationSnapshot? ResolveArtifactExpectation(
        ProcessProjectionCandidateSnapshot candidate,
        string projectStructureContractText,
        ProcessAutomationExecutionArtifact artifact);

    ProcessArtifactExpectationSnapshot? ResolveArtifactExpectation(
        ProcessProjectionCandidateSnapshot candidate,
        string projectStructureContractText,
        ProcessAutomationExecutionArtifact artifact,
        string? artifactTextContent);

    Guid? ResolveArtifactExpectationId(
        ProcessProjectionCandidateSnapshot candidate,
        ProcessProjectionRunSnapshot run,
        ProcessAutomationExecutionArtifact artifact);

    bool WorkspaceWrittenFileMatchesExpectedArtifact(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string path,
        string content);
}

internal interface IProcessProjectionProcessMockRules
{
    IReadOnlyList<ProcessProjectionProcessMockArtifact> ResolveProcessMockArtifactProjections(string? serializedSessionStateJson);

    bool ProcessMockArtifactMatchesExpectation(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessProjectionProcessMockArtifact projection);
}

internal interface IProcessProjectionProjectStructureMatcher
{
    bool TryResolveProjectStructureExpectedArtifactPath(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string projectStructureContractText,
        out string governedPath);

    bool ArtifactPathMatchesGovernedProjectStructurePath(string artifactPath, string governedPath);
}

internal interface IProcessProjectionSessionObservationSource
{
    IReadOnlyList<ProcessProjectionSessionFileContent> ResolveSuccessfulSessionFileWrites(string? serializedSessionStateJson);
}

internal interface IProcessProjectionResponseTextRules
{
    bool ShouldProjectResponseTextArtifacts(
        ProcessProjectionRunSnapshot run,
        ProcessStepRunStatus completionStatus);

    string ResolveProjectableResponseArtifactText(string? responseText);

    bool IsUsableProjectedResponseArtifactContent(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string responseText);

    bool TryResolveResponseTextArtifactRelativePath(
        ProcessProjectionCandidateSnapshot candidate,
        WorkspaceScopeDescriptor workspaceScope,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        out string relativePath);
}

internal interface IProcessProjectionBrowserOutputRules
{
    bool TryExtractExpectedArtifactRelativePath(string validationRequirementSummary, out string relativePath);

    string ResolveProviderNativeBrowserToolName(string expectedRelativePath);

    bool MatchesExpectedBrowserOutputFile(string expectedRelativePath, string outputFileName);

    bool IsProviderNativeBrowserArtifactPath(string relativePath);

    string BuildProviderNativeBrowserArtifactTitle(ProcessAutomationExecutionArtifact artifact);
}

internal interface IProcessProjectionDecisionArtifactRules
{
    bool ShouldAutoRecordCompletedDecisionArtifact(ProcessArtifactExpectationSnapshot expectedArtifact);

    string BuildCompletedDecisionArtifactExternalReferenceKey(Guid stepRunId, Guid artifactExpectationId);

    ProcessArtifactTrustStatus ResolveCompletedDecisionArtifactTrustStatus(ProcessArtifactTrustRequirement trustRequirement);

    string BuildCompletedDecisionArtifactProvenanceSummary(
        ProcessProjectionCandidateSnapshot candidate,
        ProcessProjectionRunSnapshot run);

    string BuildCompletedDecisionArtifactReviewSummary(
        ProcessProjectionCandidateSnapshot candidate,
        ProcessProjectionRunSnapshot run,
        string responseText,
        ProcessArtifactExpectationSnapshot expectedArtifact);
}

internal interface IProcessProjectionLineageFactory
{
    ProcessArtifactProjectionLineage BuildArtifactProjectionLineage(
        ProcessArtifactProjectionSourceKind sourceKind,
        Guid? sourceExecutionRunId = null,
        ProcessProjectionLineageInput? lineage = null,
        Guid? sourceArtifactId = null,
        string sourceExternalReferenceKey = "");
}

internal interface IProcessProjectionCandidateStateUpdater
{
    bool TryApplyExpectedWriteOutcome(
        ProcessProjectionMutableCandidateState candidateState,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        Result<ProcessArtifactProjectionWriteResult> writeResult,
        out string errorSummary);

    bool TryApplyWriteOutcome(
        ProcessProjectionMutableCandidateState candidateState,
        Result<ProcessArtifactProjectionWriteResult> writeResult,
        Guid? expectedArtifactId,
        out string errorSummary);

    bool TryApplyExpectedRecordOnlyOutcome(
        ProcessProjectionMutableCandidateState candidateState,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        Result<ProcessArtifactProjectionRecordOnlyResult> recordResult,
        out string errorSummary);
}
