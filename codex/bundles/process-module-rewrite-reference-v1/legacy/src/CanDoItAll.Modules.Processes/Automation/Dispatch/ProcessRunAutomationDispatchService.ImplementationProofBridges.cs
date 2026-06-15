namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static class ProcessMockImplementationProofBridge
    {
        internal static bool MatchesExpectedArtifact(
            DispatchArtifactExpectation expectedArtifact,
            ProcessMockArtifactProjection projection)
        {
            var observedTokens = TokenizeArtifactContentSignalText($"{projection.RelativePath} {projection.ContentSignalText}")
                .ToHashSet(StringComparer.Ordinal);
            var titleTokens = TokenizeArtifactContentSignalText(expectedArtifact.Title)
                .ToList();
            if (observedTokens.Count == 0 || titleTokens.Count == 0)
            {
                return false;
            }

            return titleTokens.All(observedTokens.Contains);
        }

        internal static bool CanSatisfyConcreteImplementationProof(
            bool requiresConcreteImplementationProof,
            IEnumerable<DispatchArtifactExpectation> expectedArtifacts,
            ProcessMockArtifactProjection projection)
        {
            return requiresConcreteImplementationProof &&
                   IsImplementationRole(projection.RoleKey) &&
                   MatchesRequiredArtifact(expectedArtifacts, projection);
        }

        internal static bool IsImplementationRole(string roleKey)
        {
            var normalizedRoleKey = roleKey.Trim().ToLowerInvariant();
            return normalizedRoleKey is ProcessMockDeveloperRoleKey or ProcessMockRepairDeveloperRoleKey;
        }

        internal static bool MatchesRequiredArtifact(
            IEnumerable<DispatchArtifactExpectation> expectedArtifacts,
            ProcessMockArtifactProjection projection)
        {
            return expectedArtifacts
                .Where(item => item.IsRequired)
                .Any(item => MatchesExpectedArtifact(item, projection));
        }
    }

    private static class ProcessImplementationArtifactWriteSatisfactionBridge
    {
        internal static bool CanProjectWorkspaceWrittenArtifact(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail,
            DispatchArtifactExpectation expectedArtifact)
        {
            if (TryResolveProjectStructureExpectedArtifactPath(candidate, expectedArtifact, detail.Run.InputSummary, out var governedPath))
            {
                return ResolveSuccessfulSessionFileWrites(detail.Run.SerializedSessionStateJson)
                    .Any(file => ArtifactPathMatchesGovernedProjectStructurePath(file.Path, governedPath)) ||
                    detail.ToolReceipts
                        .Where(IsSuccessfulWorkspaceFileMutationReceipt)
                        .SelectMany(ResolveManagedWorkspacePathsFromReceipt)
                        .Any(path => ArtifactPathMatchesGovernedProjectStructurePath(path, governedPath));
            }

            if (ResolveSuccessfulSessionFileWrites(detail.Run.SerializedSessionStateJson)
                .Any(file => WorkspaceWrittenFileMatchesExpectedArtifact(
                    candidate.ExpectedArtifacts,
                    expectedArtifact,
                    file.Path,
                    file.Content)))
            {
                return true;
            }

            return detail.ToolReceipts
                .Where(IsSuccessfulWorkspaceFileMutationReceipt)
                .SelectMany(ResolveManagedWorkspacePathsFromReceipt)
                .Any(path => WorkspaceWrittenFileMatchesExpectedArtifact(
                    candidate.ExpectedArtifacts,
                    expectedArtifact,
                    path,
                    content: string.Empty));
        }
    }
}
