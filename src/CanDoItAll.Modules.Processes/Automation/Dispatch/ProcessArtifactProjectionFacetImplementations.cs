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

internal delegate Task ProcessProjectionClaimGuardHandler(
    ProcessStepDispatchClaim dispatchClaim,
    CancellationToken cancellationToken);

internal static class ProcessArtifactProjectionFacetFactory
{
    public static ProcessArtifactProjectionFacetSet Create(ProcessProjectionClaimGuardHandler ensureClaimHeldAsync)
    {
        ArgumentNullException.ThrowIfNull(ensureClaimHeldAsync);

        var fileIo = new ProcessProjectionFileIo();
        return new ProcessArtifactProjectionFacetSet(
            ClaimGuard: new ProcessProjectionClaimGuard(ensureClaimHeldAsync),
            PathResolver: new ProcessProjectionPathResolver(),
            FileIo: fileIo,
            ArtifactClassifier: new ProcessProjectionArtifactClassifier(),
            ExpectationMatcher: new ProcessProjectionExpectationMatcher(fileIo),
            ProcessMockRules: new ProcessProjectionProcessMockRules(),
            ProjectStructureMatcher: new ProcessProjectionProjectStructureMatcher(),
            SessionObservationSource: new ProcessProjectionSessionObservationSource(),
            ResponseTextRules: new ProcessProjectionResponseTextRules(),
            BrowserOutputRules: new ProcessProjectionBrowserOutputRules(),
            DecisionArtifactRules: new ProcessProjectionDecisionArtifactRules(),
            LineageFactory: new ProcessProjectionLineageFactory(),
            CandidateState: new ProcessProjectionCandidateStateUpdater());
    }
}

internal sealed class ProcessProjectionClaimGuard(ProcessProjectionClaimGuardHandler ensureClaimHeldAsync) :
    IProcessProjectionClaimGuard
{
    public Task EnsureStepDispatchClaimHeldAsync(
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        return ensureClaimHeldAsync(dispatchClaim, cancellationToken);
    }
}

internal sealed class ProcessProjectionPathResolver : IProcessProjectionPathResolver
{
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

    public string ResolveScopedManagedRelativePath(
        WorkspaceScopeDescriptor workspaceScope,
        string relativePath)
    {
        return ProcessRunAutomationDispatchService.ResolveScopedManagedRelativePath(workspaceScope, relativePath);
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

    public bool IsWithinWorkspace(string workspaceRoot, string fullPath)
    {
        return ProcessRunAutomationDispatchService.IsWithinWorkspace(workspaceRoot, fullPath);
    }
}

internal sealed class ProcessProjectionFileIo : IProcessProjectionFileIo
{
    public bool FileExists(string fullPath)
    {
        return File.Exists(fullPath);
    }

    public long GetFileLength(string fullPath)
    {
        return new FileInfo(fullPath).Length;
    }

    public byte[] ReadAllBytes(string fullPath)
    {
        return File.ReadAllBytes(fullPath);
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
}

internal sealed class ProcessProjectionArtifactClassifier : IProcessProjectionArtifactClassifier
{
    public bool IsTransientExecutionArtifact(ProcessAutomationExecutionArtifact artifact)
    {
        return ProcessRunAutomationDispatchService.IsTransientExecutionArtifact(artifact);
    }

    public string? TryDecodeTextArtifactContent(
        ProcessAutomationExecutionArtifact artifact,
        string fullPath,
        byte[] content)
    {
        return ProcessRunAutomationDispatchService.TryDecodeTextArtifactContent(artifact, fullPath, content);
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
}

internal sealed class ProcessProjectionExpectationMatcher(IProcessProjectionFileIo fileIo) :
    IProcessProjectionExpectationMatcher
{
    public bool ExistingManagedArtifactFileMatches(
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        DispatchArtifactExpectation expectedArtifact,
        string workspaceRoot,
        string relativePath)
    {
        if (!ProcessRunAutomationDispatchService.TryResolveArtifactFullPath(workspaceRoot, relativePath, out var fullPath, out _) ||
            !fileIo.FileExists(fullPath))
        {
            return false;
        }

        string? textContent = null;
        try
        {
            if (fileIo.GetFileLength(fullPath) is > 0 and <= 512 * 1024)
            {
                var content = fileIo.ReadAllBytes(fullPath);
                textContent = ProcessRunAutomationDispatchService.TryDecodeTextArtifactContent(
                    CreateExistingManagedSyntheticArtifact(expectedArtifact, relativePath, fullPath),
                    fullPath,
                    content);
            }
        }
        catch (Exception)
        {
            textContent = null;
        }

        var syntheticArtifact = CreateExistingManagedSyntheticArtifact(expectedArtifact, relativePath, fullPath);
        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(
            expectedArtifacts,
            syntheticArtifact,
            textContent);
        return matchedExpectationId == expectedArtifact.Id;
    }

    public bool HasProjectedArtifactExpectationExternalReference(
        IEnumerable<string> externalReferenceKeys,
        Guid artifactExpectationId)
    {
        return ProcessRunAutomationDispatchService.HasProjectedArtifactExpectationExternalReference(
            externalReferenceKeys,
            artifactExpectationId);
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

    private static ProcessAutomationExecutionArtifact CreateExistingManagedSyntheticArtifact(
        DispatchArtifactExpectation expectedArtifact,
        string relativePath,
        string fullPath)
    {
        return new ProcessAutomationExecutionArtifact(
            Guid.Empty,
            Guid.Empty,
            "generated-output",
            expectedArtifact.Title,
            relativePath,
            ProcessRunAutomationDispatchService.GuessContentTypeFromPath(fullPath),
            "managed-workspace-file",
            "Existing managed workspace artifact.",
            DateTimeOffset.MinValue);
    }
}

internal sealed class ProcessProjectionProcessMockRules : IProcessProjectionProcessMockRules
{
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
}

internal sealed class ProcessProjectionProjectStructureMatcher : IProcessProjectionProjectStructureMatcher
{
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
}

internal sealed class ProcessProjectionSessionObservationSource : IProcessProjectionSessionObservationSource
{
    public IReadOnlyList<string> ResolveSuccessfulWorkspaceFileMutationReceiptPaths(
        ProcessAutomationExecutionRunDetail detail)
    {
        return ProcessRunAutomationDispatchService.ResolveSuccessfulWorkspaceFileMutationReceiptPaths(detail);
    }

    public IReadOnlyList<SessionFileContent> ResolveSuccessfulSessionFileWrites(string? serializedSessionStateJson)
    {
        return ProcessRunAutomationDispatchService.ResolveSuccessfulSessionFileWrites(serializedSessionStateJson);
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
}

internal sealed class ProcessProjectionResponseTextRules : IProcessProjectionResponseTextRules
{
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
}

internal sealed class ProcessProjectionBrowserOutputRules : IProcessProjectionBrowserOutputRules
{
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

    public bool IsProviderNativeBrowserArtifactPath(string relativePath)
    {
        return ProcessRunAutomationDispatchService.IsProviderNativeBrowserArtifactPath(relativePath);
    }

    public string BuildProviderNativeBrowserArtifactTitle(ProcessAutomationExecutionArtifact artifact)
    {
        return ProcessRunAutomationDispatchService.BuildProviderNativeBrowserArtifactTitle(artifact);
    }
}

internal sealed class ProcessProjectionDecisionArtifactRules : IProcessProjectionDecisionArtifactRules
{
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
        return ProcessRunAutomationDispatchService.BuildCompletedDecisionArtifactProvenanceSummary(candidate, detail);
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
}

internal sealed class ProcessProjectionLineageFactory : IProcessProjectionLineageFactory
{
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
}

internal sealed class ProcessProjectionCandidateStateUpdater : IProcessProjectionCandidateStateUpdater
{
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
