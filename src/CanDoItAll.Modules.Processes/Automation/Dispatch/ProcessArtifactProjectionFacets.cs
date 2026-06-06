using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using System.Text;

using ArtifactProjectionLineage = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ArtifactProjectionLineage;
using DispatchArtifactExpectation = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchArtifactExpectation;
using DispatchCandidate = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchCandidate;
using ProcessMockArtifactProjection = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ProcessMockArtifactProjection;
using ProcessStepDispatchClaim = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ProcessStepDispatchClaim;
using SessionFileContent = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.SessionFileContent;

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
    Task EnsureStepDispatchClaimHeldAsync(ProcessStepDispatchClaim dispatchClaim, CancellationToken cancellationToken);
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
        DispatchCandidate candidate,
        WorkspaceScopeDescriptor workspaceScope,
        DispatchArtifactExpectation expectedArtifact);

    string ResolveProviderNativeBrowserProjectedRelativePath(
        DispatchCandidate candidate,
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
        DispatchCandidate candidate,
        ProcessAutomationExecutionArtifact artifact);

    StorageContentKind ResolveStorageContentKind(string contentType, string fullPath);

    string BuildStorageRelativePath(
        DispatchCandidate candidate,
        ProcessAutomationExecutionArtifact artifact);

    string GuessContentTypeFromPath(string fullPath);
}

internal interface IProcessProjectionExpectationMatcher
{
    bool ExistingManagedArtifactFileMatches(
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        DispatchArtifactExpectation expectedArtifact,
        string workspaceRoot,
        string relativePath);

    bool HasProjectedArtifactExpectationExternalReference(
        IEnumerable<string> externalReferenceKeys,
        Guid artifactExpectationId);

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

    bool WorkspaceWrittenFileMatchesExpectedArtifact(
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        DispatchArtifactExpectation expectedArtifact,
        string path,
        string content);
}

internal interface IProcessProjectionProcessMockRules
{
    IReadOnlyList<ProcessMockArtifactProjection> ResolveProcessMockArtifactProjections(string? serializedSessionStateJson);

    bool ProcessMockArtifactMatchesExpectation(
        DispatchArtifactExpectation expectedArtifact,
        ProcessMockArtifactProjection projection);
}

internal interface IProcessProjectionProjectStructureMatcher
{
    bool TryResolveProjectStructureExpectedArtifactPath(
        DispatchCandidate candidate,
        DispatchArtifactExpectation expectedArtifact,
        string projectStructureContractText,
        out string governedPath);

    bool ArtifactPathMatchesGovernedProjectStructurePath(string artifactPath, string governedPath);
}

internal interface IProcessProjectionSessionObservationSource
{
    IReadOnlyList<string> ResolveSuccessfulWorkspaceFileMutationReceiptPaths(ProcessAutomationExecutionRunDetail detail);

    IReadOnlyList<SessionFileContent> ResolveSuccessfulSessionFileWrites(string? serializedSessionStateJson);

    IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveSuccessfulBrowserToolOutputFiles(
        ProcessAutomationExecutionRunDetail detail);

    string? ResolveProviderNativeBrowserWorkingDirectory(ProcessAutomationExecutionRunDetail detail);
}

internal interface IProcessProjectionResponseTextRules
{
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
}

internal interface IProcessProjectionLineageFactory
{
    ProcessArtifactProjectionLineage BuildArtifactProjectionLineage(
        ProcessArtifactProjectionSourceKind sourceKind,
        Guid? sourceExecutionRunId = null,
        ArtifactProjectionLineage? lineage = null,
        Guid? sourceArtifactId = null,
        string sourceExternalReferenceKey = "");
}

internal interface IProcessProjectionCandidateStateUpdater
{
    bool TryApplyExpectedWriteOutcome(
        DispatchCandidate candidate,
        DispatchArtifactExpectation expectedArtifact,
        Result<ProcessArtifactProjectionWriteResult> writeResult,
        out string errorSummary);

    bool TryApplyWriteOutcome(
        DispatchCandidate candidate,
        Result<ProcessArtifactProjectionWriteResult> writeResult,
        Guid? expectedArtifactId,
        out string errorSummary);

    bool TryApplyExpectedRecordOnlyOutcome(
        DispatchCandidate candidate,
        DispatchArtifactExpectation expectedArtifact,
        Result<ProcessArtifactProjectionRecordOnlyResult> recordResult,
        out string errorSummary);
}
