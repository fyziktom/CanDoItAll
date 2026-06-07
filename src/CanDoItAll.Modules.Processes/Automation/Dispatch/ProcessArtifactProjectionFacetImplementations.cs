using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal delegate Task ProcessProjectionClaimGuardHandler(
    ProcessProjectionStepDispatchClaim dispatchClaim,
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
        ProcessProjectionStepDispatchClaim dispatchClaim,
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
        return TryResolveArtifactFullPathCore(workspaceRoot, relativePath, out fullPath, out failureReason);
    }

    public string ResolveScopedManagedRelativePath(
        WorkspaceScopeDescriptor workspaceScope,
        string relativePath)
    {
        return ProcessScopedManagedArtifactPathRules.ResolveScopedManagedRelativePath(workspaceScope, relativePath);
    }

    public IReadOnlyList<string> ResolveExpectedManagedArtifactRelativePaths(
        ProcessProjectionCandidateSnapshot candidate,
        WorkspaceScopeDescriptor workspaceScope,
        ProcessArtifactExpectationSnapshot expectedArtifact)
    {
        var paths = new List<string>();
        if (ProcessArtifactPathValidationRules.TryExtractExpectedArtifactRelativePath(
                expectedArtifact.ValidationRequirementSummary,
                out var declaredRelativePath))
        {
            AddManagedArtifactPath(paths, workspaceScope, declaredRelativePath);
        }

        if (ProcessResponseTextArtifactSatisfactionRules.CanProjectResponseTextArtifactWithoutDeclaredPath(expectedArtifact))
        {
            AddManagedArtifactPath(
                paths,
                workspaceScope,
                ProcessResponseTextArtifactSatisfactionRules.BuildFallbackResponseTextArtifactRelativePath(
                    candidate.CurrentRunManagedArtifactRoot,
                    candidate.Step.Sequence,
                    expectedArtifact));
        }

        return paths;
    }

    public string ResolveProviderNativeBrowserProjectedRelativePath(
        ProcessProjectionCandidateSnapshot candidate,
        WorkspaceScopeDescriptor workspaceScope,
        string normalizedOutputPath)
    {
        if (IsManagedBrowserArtifactPath(normalizedOutputPath))
        {
            return ResolveScopedManagedRelativePath(workspaceScope, normalizedOutputPath);
        }

        var fileName = Path.GetFileName(normalizedOutputPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "browser-proof";
        }

        return ResolveScopedManagedRelativePath(
            workspaceScope,
            WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(
                candidate.CurrentRunManagedArtifactRoot,
                "browser",
                fileName)));
    }

    public string ResolveWorkspaceWrittenArtifactRelativePath(
        WorkspaceScopeDescriptor workspaceScope,
        string writtenPath)
    {
        return ProcessExecutionArtifactMetadataRules.ResolveWorkspaceWrittenArtifactRelativePath(
            workspaceScope,
            writtenPath,
            ProcessConcreteProductPathRules.IsExternalTargetAliasPath,
            ProcessConcreteProductPathRules.TryMapWorkspacePathForPrompt,
            ProcessScopedManagedArtifactPathRules.ResolveScopedManagedRelativePath);
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
        return ProcessExecutionArtifactMetadataRules.TryResolveWorkspaceWrittenArtifactSourceFullPath(
            workspaceRoot,
            workspaceScope,
            writtenPath,
            projectedRelativePath,
            ProcessConcreteProductPathRules.IsExternalTargetAliasPath,
            ProcessConcreteProductPathRules.TryMapWorkspacePathForPrompt,
            ProcessScopedManagedArtifactPathRules.ResolveScopedManagedRelativePath,
            TryResolveArtifactFullPathCore,
            out fullPath,
            out sourceRelativePath,
            out failureReason);
    }

    public bool IsWithinWorkspace(string workspaceRoot, string fullPath)
    {
        var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var normalizedFullPath = Path.GetFullPath(fullPath);
        return string.Equals(normalizedFullPath, normalizedWorkspaceRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedFullPath.StartsWith(EnsureTrailingDirectorySeparator(normalizedWorkspaceRoot), StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryResolveArtifactFullPathCore(
        string workspaceRoot,
        string relativePath,
        out string fullPath,
        out string failureReason)
    {
        fullPath = string.Empty;
        failureReason = string.Empty;

        var normalizedRelativePath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalizedRelativePath))
        {
            failureReason = "Artifact relative path is empty.";
            return false;
        }

        if (ProcessConcreteProductPathRules.IsExternalTargetAliasPath(normalizedRelativePath))
        {
            return TryResolveExternalTargetArtifactFullPath(normalizedRelativePath, out fullPath, out failureReason);
        }

        fullPath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (new ProcessProjectionPathResolver().IsWithinWorkspace(workspaceRoot, fullPath))
        {
            return true;
        }

        failureReason = $"Artifact path '{normalizedRelativePath}' resolves outside the workspace root.";
        fullPath = string.Empty;
        return false;
    }

    private static void AddManagedArtifactPath(
        ICollection<string> paths,
        WorkspaceScopeDescriptor workspaceScope,
        string relativePath)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalizedPath) ||
            ProcessConcreteProductPathRules.IsExternalTargetAliasPath(normalizedPath))
        {
            return;
        }

        var scopedPath = ProcessScopedManagedArtifactPathRules.ResolveScopedManagedRelativePath(workspaceScope, normalizedPath);
        if (string.IsNullOrWhiteSpace(scopedPath) ||
            paths.Contains(scopedPath, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        paths.Add(scopedPath);
    }

    private static bool TryResolveExternalTargetArtifactFullPath(
        string normalizedRelativePath,
        out string fullPath,
        out string failureReason)
    {
        const string externalTargetAliasRoot = "external-target";
        fullPath = string.Empty;
        failureReason = string.Empty;

        var suffix = normalizedRelativePath.Length == externalTargetAliasRoot.Length
            ? string.Empty
            : normalizedRelativePath[(externalTargetAliasRoot.Length + 1)..];
        var segments = suffix.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0 ||
            segments[0].Length != 1 ||
            !char.IsLetter(segments[0][0]))
        {
            failureReason = $"Artifact path '{normalizedRelativePath}' uses invalid external-target syntax.";
            return false;
        }

        var driveRoot = $"{char.ToUpperInvariant(segments[0][0])}:{Path.DirectorySeparatorChar}";
        var remainingSegments = segments.Skip(1).ToArray();
        fullPath = Path.GetFullPath(
            remainingSegments.Length == 0
                ? driveRoot
                : Path.Combine(driveRoot, Path.Combine(remainingSegments)));
        return true;
    }

    private static bool IsManagedBrowserArtifactPath(string relativePath)
    {
        var comparablePath = ProcessArtifactPathValidationRules.NormalizeManagedRelativePathForComparison(
            WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath));
        return comparablePath.StartsWith("artifacts/process-runs/", StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}

internal sealed class ProcessProjectionFileIo : IProcessProjectionFileIo
{
    public bool FileExists(string fullPath) => File.Exists(fullPath);

    public long GetFileLength(string fullPath) => new FileInfo(fullPath).Length;

    public byte[] ReadAllBytes(string fullPath) => File.ReadAllBytes(fullPath);

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
        return ProcessArtifactKindClassificationRules.IsTransientExecutionArtifact(artifact);
    }

    public string? TryDecodeTextArtifactContent(
        ProcessAutomationExecutionArtifact artifact,
        string fullPath,
        byte[] content)
    {
        return ProcessExecutionArtifactTextContentRules.TryDecodeTextArtifactContent(artifact, fullPath, content);
    }

    public ProcessArtifactKind ResolveProcessArtifactKind(
        ProcessProjectionCandidateSnapshot candidate,
        ProcessAutomationExecutionArtifact artifact)
    {
        return ProcessArtifactKindClassificationRules.ResolveProcessArtifactKind(
            artifact,
            ProcessArtifactExpectationResolver.ResolveArtifactExpectation(
                candidate.ExpectedArtifacts,
                projectStructureContractText: null,
                artifact)?.ArtifactKind);
    }

    public StorageContentKind ResolveStorageContentKind(string contentType, string fullPath)
    {
        return ProcessStorageContentKindRules.ResolveStorageContentKind(contentType, fullPath);
    }

    public string BuildStorageRelativePath(
        ProcessProjectionCandidateSnapshot candidate,
        ProcessAutomationExecutionArtifact artifact)
    {
        return ProcessExecutionArtifactMetadataRules.BuildStorageRelativePath(
            candidate.RunId,
            candidate.Step.Id,
            artifact.RelativePath);
    }

    public string GuessContentTypeFromPath(string fullPath)
    {
        return ProcessArtifactKindClassificationRules.GuessContentTypeFromPath(fullPath);
    }
}

internal sealed class ProcessProjectionExpectationMatcher(IProcessProjectionFileIo fileIo) :
    IProcessProjectionExpectationMatcher
{
    public bool ExistingManagedArtifactFileMatches(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string workspaceRoot,
        string relativePath)
    {
        if (!ProcessProjectionPathResolver.TryResolveArtifactFullPathCore(workspaceRoot, relativePath, out var fullPath, out _) ||
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
                textContent = ProcessExecutionArtifactTextContentRules.TryDecodeTextArtifactContent(
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
        var matchedExpectationId = ProcessArtifactExpectationResolver.MatchExpectedArtifactId(
            expectedArtifacts,
            syntheticArtifact,
            textContent);
        return matchedExpectationId == expectedArtifact.Id;
    }

    public bool HasProjectedArtifactExpectationExternalReference(
        IEnumerable<string> externalReferenceKeys,
        Guid artifactExpectationId)
    {
        var marker = $"|{artifactExpectationId:D}|";
        var suffix = $"|{artifactExpectationId:D}";
        return externalReferenceKeys.Any(key =>
            !string.IsNullOrWhiteSpace(key) &&
            (key.Contains(marker, StringComparison.OrdinalIgnoreCase) ||
             key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));
    }

    public ProcessArtifactExpectationSnapshot? ResolveArtifactExpectation(
        ProcessProjectionCandidateSnapshot candidate,
        ProcessAutomationExecutionArtifact artifact)
    {
        return ProcessArtifactExpectationResolver.ResolveArtifactExpectation(
            candidate.ExpectedArtifacts,
            projectStructureContractText: null,
            artifact);
    }

    public ProcessArtifactExpectationSnapshot? ResolveArtifactExpectation(
        ProcessProjectionCandidateSnapshot candidate,
        string projectStructureContractText,
        ProcessAutomationExecutionArtifact artifact)
    {
        return ProcessArtifactExpectationResolver.ResolveArtifactExpectation(
            candidate.ExpectedArtifacts,
            projectStructureContractText,
            artifact);
    }

    public ProcessArtifactExpectationSnapshot? ResolveArtifactExpectation(
        ProcessProjectionCandidateSnapshot candidate,
        string projectStructureContractText,
        ProcessAutomationExecutionArtifact artifact,
        string? artifactTextContent)
    {
        return ProcessArtifactExpectationResolver.ResolveArtifactExpectation(
            candidate.ExpectedArtifacts,
            projectStructureContractText,
            artifact,
            artifactTextContent);
    }

    public Guid? ResolveArtifactExpectationId(
        ProcessProjectionCandidateSnapshot candidate,
        ProcessProjectionRunSnapshot run,
        ProcessAutomationExecutionArtifact artifact)
    {
        return ProcessArtifactExpectationResolver.ResolveArtifactExpectation(
            candidate.ExpectedArtifacts,
            run.InputSummary,
            artifact)?.Id;
    }

    public bool WorkspaceWrittenFileMatchesExpectedArtifact(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string path,
        string content)
    {
        return ProcessArtifactExpectationResolver.WorkspaceWrittenFileMatchesExpectedArtifact(
            expectedArtifacts,
            expectedArtifact,
            path,
            content);
    }

    private static ProcessAutomationExecutionArtifact CreateExistingManagedSyntheticArtifact(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string relativePath,
        string fullPath)
    {
        return new ProcessAutomationExecutionArtifact(
            Guid.Empty,
            Guid.Empty,
            "generated-output",
            expectedArtifact.Title,
            relativePath,
            ProcessArtifactKindClassificationRules.GuessContentTypeFromPath(fullPath),
            "managed-workspace-file",
            "Existing managed workspace artifact.",
            DateTimeOffset.MinValue);
    }
}

internal sealed class ProcessProjectionProcessMockRules : IProcessProjectionProcessMockRules
{
    private const string ProcessMockSessionFlagPropertyName = "processMockAgent";
    private const string ProcessMockRoleKeyPropertyName = "roleKey";
    private const string ProcessMockArtifactRootPropertyName = "artifactRoot";
    private const string ProcessMockBranchOutcomeKeyPropertyName = "branchOutcomeKey";
    private const string ProcessMockProductOwnerRoleKey = "product-owner";
    private const string ProcessMockArchitectRoleKey = "architect";
    private const string ProcessMockDeveloperRoleKey = "developer";
    private const string ProcessMockQaRoleKey = "qa";
    private const string ProcessMockRepairDeveloperRoleKey = "repair-developer";
    private const string ProcessMockReleaseManagerRoleKey = "release-manager";
    private const string ProcessMockBranchRepairsRequired = "repairs-required";
    private const string ProcessMockBranchApproved = "approved";

    public IReadOnlyList<ProcessProjectionProcessMockArtifact> ResolveProcessMockArtifactProjections(
        string? serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            var root = document.RootElement;
            if (!root.TryGetProperty(ProcessMockSessionFlagPropertyName, out var processMockFlag) ||
                processMockFlag.ValueKind != JsonValueKind.True ||
                !TryGetStringProperty(root, ProcessMockRoleKeyPropertyName, out var roleKey) ||
                !TryGetStringProperty(root, ProcessMockArtifactRootPropertyName, out var artifactRoot))
            {
                return [];
            }

            var normalizedRoot = WorkspaceScopeDescriptor.NormalizeRelativePath(artifactRoot);
            if (string.IsNullOrWhiteSpace(normalizedRoot))
            {
                return [];
            }

            var branchOutcomeKey = TryGetStringProperty(root, ProcessMockBranchOutcomeKeyPropertyName, out var resolvedBranchOutcomeKey)
                ? resolvedBranchOutcomeKey
                : null;
            var projections = new List<ProcessProjectionProcessMockArtifact>();
            if (root.TryGetProperty("artifacts", out var artifactsElement) &&
                artifactsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var artifactElement in artifactsElement.EnumerateArray())
                {
                    if ((!TryGetStringProperty(artifactElement, "relativePath", out var relativePath) &&
                         !TryGetStringProperty(artifactElement, "RelativePath", out relativePath)) ||
                        (!TryGetStringProperty(artifactElement, "contentSignalText", out var contentSignalText) &&
                         !TryGetStringProperty(artifactElement, "ContentSignalText", out contentSignalText)))
                    {
                        continue;
                    }

                    projections.Add(new ProcessProjectionProcessMockArtifact(
                        roleKey.Trim(),
                        branchOutcomeKey,
                        WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath),
                        contentSignalText));
                }
            }

            if (projections.Count > 0)
            {
                return projections;
            }

            if (!TryResolveProcessMockArtifactFile(roleKey, branchOutcomeKey, out var fileName, out var fallbackContentSignalText))
            {
                return [];
            }

            return
            [
                new ProcessProjectionProcessMockArtifact(
                    roleKey.Trim(),
                    branchOutcomeKey,
                    WorkspaceScopeDescriptor.NormalizeRelativePath($"{normalizedRoot.TrimEnd('/')}/{fileName}"),
                    fallbackContentSignalText)
            ];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public bool ProcessMockArtifactMatchesExpectation(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessProjectionProcessMockArtifact projection)
    {
        var observedTokens = ProcessArtifactTextMatchRules
            .TokenizeArtifactContentSignalText($"{projection.RelativePath} {projection.ContentSignalText}")
            .ToHashSet(StringComparer.Ordinal);
        var titleTokens = ProcessArtifactTextMatchRules
            .TokenizeArtifactContentSignalText(expectedArtifact.Title)
            .ToList();
        if (observedTokens.Count == 0 || titleTokens.Count == 0)
        {
            return false;
        }

        return titleTokens.All(observedTokens.Contains);
    }

    private static bool TryResolveProcessMockArtifactFile(
        string roleKey,
        string? branchOutcomeKey,
        out string fileName,
        out string contentSignalText)
    {
        var normalizedRoleKey = roleKey.Trim().ToLowerInvariant();
        var normalizedBranchOutcomeKey = branchOutcomeKey?.Trim().ToLowerInvariant() ?? string.Empty;
        (fileName, contentSignalText) = (normalizedRoleKey, normalizedBranchOutcomeKey) switch
        {
            (ProcessMockProductOwnerRoleKey, _) => ("01-scope.md", "scope acceptance criteria requirements"),
            (ProcessMockArchitectRoleKey, _) => ("02-architecture.md", "architecture boundaries implementation qa expectations"),
            (ProcessMockDeveloperRoleKey, _) => ("03-implementation.md", "implementation change set deliverable validation evidence"),
            (ProcessMockQaRoleKey, ProcessMockBranchRepairsRequired) => ("04-qa-finding.md", "qa rejection finding repair branch reason"),
            (ProcessMockRepairDeveloperRoleKey, _) => ("05-repair.md", "repair implementation validation evidence"),
            (ProcessMockQaRoleKey, ProcessMockBranchApproved) => ("06-qa-approval.md", "qa approval implementation release evidence"),
            (ProcessMockReleaseManagerRoleKey, _) => ("07-release-notes.md", "release notes qa approval rollout evidence"),
            _ => (string.Empty, string.Empty)
        };

        return !string.IsNullOrWhiteSpace(fileName);
    }

    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
    {
        if (element.TryGetProperty(propertyName, out var valueElement) &&
            valueElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(valueElement.GetString()))
        {
            value = valueElement.GetString()!.Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }
}

internal sealed class ProcessProjectionProjectStructureMatcher : IProcessProjectionProjectStructureMatcher
{
    public bool TryResolveProjectStructureExpectedArtifactPath(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string projectStructureContractText,
        out string governedPath)
    {
        return ProcessProjectStructureArtifactPathRules.TryResolveProjectStructureExpectedArtifactPath(
            expectedArtifact,
            ProcessProjectStructureArtifactPathRules.ResolveProjectStructureRequiredArtifactPaths(
                projectStructureContractText,
                ProcessConcreteProductPathRules.TryMapWorkspacePathForPrompt),
            out governedPath);
    }

    public bool ArtifactPathMatchesGovernedProjectStructurePath(string artifactPath, string governedPath)
    {
        return ProcessProjectStructureArtifactPathRules.ArtifactPathMatchesGovernedProjectStructurePath(
            artifactPath,
            governedPath,
            ProcessConcreteProductPathRules.TryMapWorkspacePathForPrompt);
    }
}

internal sealed class ProcessProjectionSessionObservationSource : IProcessProjectionSessionObservationSource
{
    public IReadOnlyList<ProcessProjectionSessionFileContent> ResolveSuccessfulSessionFileWrites(string? serializedSessionStateJson)
    {
        return ProcessAutomationSessionObservation
            .Create(serializedSessionStateJson)
            .FileWrites
            .Select(item => new ProcessProjectionSessionFileContent(item.Path, item.Content))
            .ToList();
    }
}

internal sealed class ProcessProjectionResponseTextRules : IProcessProjectionResponseTextRules
{
    public bool ShouldProjectResponseTextArtifacts(
        ProcessProjectionRunSnapshot run,
        ProcessStepRunStatus completionStatus)
    {
        return completionStatus == ProcessStepRunStatus.Completed &&
               run.State == ProcessAutomationExecutionState.Completed &&
               run.Outcome == ProcessAutomationRunOutcome.Succeeded;
    }

    public string ResolveProjectableResponseArtifactText(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return string.Empty;
        }

        if (!ProcessDeclaredStepOutcomeRules.TryResolve(responseText, out _, out var outcome))
        {
            return responseText.Trim();
        }

        if (!string.IsNullOrWhiteSpace(outcome.HumanReadableSummaryMarkdown))
        {
            return outcome.HumanReadableSummaryMarkdown.Trim();
        }

        return outcome.Reason?.Trim() ?? string.Empty;
    }

    public bool IsUsableProjectedResponseArtifactContent(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        var normalizedResponse = CollapsePromptWhitespace(responseText);
        if (normalizedResponse.Length < 160 ||
            ProcessResponseTextArtifactSatisfactionRules.IsConversationalNonArtifactResponse(normalizedResponse))
        {
            return false;
        }

        return ProcessArtifactTextMatchRules.HasExpectedArtifactContentSignals(
            expectedArtifact,
            responseText,
            normalizedResponse,
            containsArtifactResponseSection: responseText.Contains("##", StringComparison.Ordinal));
    }

    public bool TryResolveResponseTextArtifactRelativePath(
        ProcessProjectionCandidateSnapshot candidate,
        WorkspaceScopeDescriptor workspaceScope,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        out string relativePath)
    {
        if (ProcessArtifactPathValidationRules.TryExtractExpectedArtifactRelativePath(
                expectedArtifact.ValidationRequirementSummary,
                out var declaredRelativePath))
        {
            if (!ProcessManagedArtifactPathClassificationRules.IsResponseProjectableTextArtifact(declaredRelativePath))
            {
                relativePath = string.Empty;
                return false;
            }

            relativePath = ProcessScopedManagedArtifactPathRules.ResolveScopedManagedRelativePath(workspaceScope, declaredRelativePath);
            return !string.IsNullOrWhiteSpace(relativePath);
        }

        if (!ProcessResponseTextArtifactSatisfactionRules.CanProjectResponseTextArtifactWithoutDeclaredPath(expectedArtifact))
        {
            relativePath = string.Empty;
            return false;
        }

        relativePath = ProcessScopedManagedArtifactPathRules.ResolveScopedManagedRelativePath(
            workspaceScope,
            ProcessResponseTextArtifactSatisfactionRules.BuildFallbackResponseTextArtifactRelativePath(
                candidate.CurrentRunManagedArtifactRoot,
                candidate.Step.Sequence,
                expectedArtifact));
        return !string.IsNullOrWhiteSpace(relativePath);
    }

    private static string CollapsePromptWhitespace(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

internal sealed class ProcessProjectionBrowserOutputRules : IProcessProjectionBrowserOutputRules
{
    public bool TryExtractExpectedArtifactRelativePath(string validationRequirementSummary, out string relativePath)
    {
        return ProcessArtifactPathValidationRules.TryExtractExpectedArtifactRelativePath(
            validationRequirementSummary,
            out relativePath);
    }

    public string ResolveProviderNativeBrowserToolName(string expectedRelativePath)
    {
        return ProcessArtifactProviderNativeVisualValidationRules.ResolveProviderNativeBrowserToolName(expectedRelativePath);
    }

    public bool MatchesExpectedBrowserOutputFile(string expectedRelativePath, string outputFileName)
    {
        return ProcessArtifactProviderNativeVisualValidationRules.MatchesExpectedBrowserOutputFile(
            expectedRelativePath,
            outputFileName);
    }

    public bool IsProviderNativeBrowserArtifactPath(string relativePath)
    {
        return ProcessArtifactProviderNativeVisualValidationRules.IsProviderNativeBrowserArtifactPath(relativePath);
    }

    public string BuildProviderNativeBrowserArtifactTitle(ProcessAutomationExecutionArtifact artifact)
    {
        var normalizedToolName = ProcessToolReceiptFacts.NormalizeToolToken(artifact.ProducedBy);
        return normalizedToolName switch
        {
            "browser_take_screenshot" => "Browser screenshot",
            "browser_snapshot" => "Browser snapshot",
            "browser_console_messages" => "Browser console log",
            "browser_evaluate" => "Browser DOM or state proof",
            _ => ProcessArtifactProjectionPlanner.BuildArtifactTitle(artifact)
        };
    }
}

internal sealed class ProcessProjectionDecisionArtifactRules : IProcessProjectionDecisionArtifactRules
{
    public bool ShouldAutoRecordCompletedDecisionArtifact(ProcessArtifactExpectationSnapshot expectedArtifact)
    {
        return ProcessExecutionArtifactMetadataRules.ShouldAutoRecordCompletedDecisionArtifact(expectedArtifact);
    }

    public string BuildCompletedDecisionArtifactExternalReferenceKey(Guid stepRunId, Guid artifactExpectationId)
    {
        return ProcessExecutionArtifactMetadataRules.BuildCompletedDecisionArtifactExternalReferenceKey(
            stepRunId,
            artifactExpectationId);
    }

    public ProcessArtifactTrustStatus ResolveCompletedDecisionArtifactTrustStatus(
        ProcessArtifactTrustRequirement trustRequirement)
    {
        return ProcessExecutionArtifactMetadataRules.ResolveCompletedDecisionArtifactTrustStatus(trustRequirement);
    }

    public string BuildCompletedDecisionArtifactProvenanceSummary(
        ProcessProjectionCandidateSnapshot candidate,
        ProcessProjectionRunSnapshot run)
    {
        var executorName = string.IsNullOrWhiteSpace(candidate.Step.CurrentExecutorName)
            ? "the assigned approver"
            : candidate.Step.CurrentExecutorName.Trim();
        return $"Recorded from the governed step outcome for AgentFramework execution run {run.Id:D} by {executorName}.";
    }

    public string BuildCompletedDecisionArtifactReviewSummary(
        ProcessProjectionCandidateSnapshot candidate,
        ProcessProjectionRunSnapshot run,
        string responseText,
        ProcessArtifactExpectationSnapshot expectedArtifact)
    {
        var executorName = string.IsNullOrWhiteSpace(candidate.Step.CurrentExecutorName)
            ? "The assigned approver"
            : candidate.Step.CurrentExecutorName.Trim();
        var summary = ResolveCompletedDecisionArtifactOutcomeSummary(candidate, run, responseText);
        var builder = new StringBuilder();
        builder.Append(executorName);
        builder.Append(" completed step '");
        builder.Append(candidate.Step.Title);
        builder.Append("' and recorded decision artifact '");
        builder.Append(expectedArtifact.Title);
        builder.Append("'.");

        if (!string.IsNullOrWhiteSpace(summary))
        {
            builder.Append(' ');
            builder.Append(EnsureTerminalPunctuation(summary));
        }

        if (!string.IsNullOrWhiteSpace(expectedArtifact.ValidationRequirementSummary))
        {
            builder.Append(" Validation expectation: ");
            builder.Append(EnsureTerminalPunctuation(expectedArtifact.ValidationRequirementSummary.Trim()));
        }

        return builder.ToString();
    }

    private static string ResolveCompletedDecisionArtifactOutcomeSummary(
        ProcessProjectionCandidateSnapshot candidate,
        ProcessProjectionRunSnapshot run,
        string responseText)
    {
        if (ProcessDeclaredStepOutcomeRules.TryResolve(responseText, out var declaredOutcome, out _) &&
            !string.IsNullOrWhiteSpace(declaredOutcome.Reason))
        {
            return declaredOutcome.Reason.Trim();
        }

        if (!string.IsNullOrWhiteSpace(candidate.Step.DecisionSummary))
        {
            return candidate.Step.DecisionSummary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(run.ResultSummary))
        {
            return run.ResultSummary.Trim();
        }

        var normalizedResponse = CollapsePromptWhitespace(responseText);
        if (!string.IsNullOrWhiteSpace(normalizedResponse) &&
            !string.Equals(
                normalizedResponse,
                "The provider completed without returning text.",
                StringComparison.OrdinalIgnoreCase))
        {
            return TrimForPrompt(normalizedResponse, 420);
        }

        return string.Empty;
    }

    private static string EnsureTerminalPunctuation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.EndsWith('.') || trimmed.EndsWith('!') || trimmed.EndsWith('?')
            ? trimmed
            : $"{trimmed}.";
    }

    private static string TrimForPrompt(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength].TrimEnd() + "...";
    }

    private static string CollapsePromptWhitespace(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

internal sealed class ProcessProjectionLineageFactory : IProcessProjectionLineageFactory
{
    public ProcessArtifactProjectionLineage BuildArtifactProjectionLineage(
        ProcessArtifactProjectionSourceKind sourceKind,
        Guid? sourceExecutionRunId = null,
        ProcessProjectionLineageInput? lineage = null,
        Guid? sourceArtifactId = null,
        string sourceExternalReferenceKey = "")
    {
        return ProcessArtifactProjectionLineageBuilder.BuildLineage(
            sourceKind,
            sourceExecutionRunId,
            lineage?.ToRecoveryContext() ?? ProcessArtifactRecoveryProjectionContext.None,
            sourceArtifactId,
            sourceExternalReferenceKey);
    }
}

internal sealed class ProcessProjectionCandidateStateUpdater : IProcessProjectionCandidateStateUpdater
{
    public bool TryApplyExpectedWriteOutcome(
        ProcessProjectionMutableCandidateState candidateState,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        Result<ProcessArtifactProjectionWriteResult> writeResult,
        out string errorSummary)
    {
        return ProcessArtifactProjectionCandidateState.TryApplyExpectedWriteOutcome(
            candidateState,
            expectedArtifact,
            writeResult,
            out errorSummary);
    }

    public bool TryApplyWriteOutcome(
        ProcessProjectionMutableCandidateState candidateState,
        Result<ProcessArtifactProjectionWriteResult> writeResult,
        Guid? expectedArtifactId,
        out string errorSummary)
    {
        return ProcessArtifactProjectionCandidateState.TryApplyWriteOutcome(
            candidateState,
            writeResult,
            expectedArtifactId,
            out errorSummary);
    }

    public bool TryApplyExpectedRecordOnlyOutcome(
        ProcessProjectionMutableCandidateState candidateState,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        Result<ProcessArtifactProjectionRecordOnlyResult> recordResult,
        out string errorSummary)
    {
        return ProcessArtifactProjectionCandidateState.TryApplyExpectedRecordOnlyOutcome(
            candidateState,
            expectedArtifact,
            recordResult,
            out errorSummary);
    }
}
