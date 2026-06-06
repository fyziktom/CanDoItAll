# Current host method inventory

Codex must update this table in SB02 with exact method counts and consumers before source movement.

Initial known categories:

- Claim guard: `EnsureStepDispatchClaimHeldAsync`.
- Path and workspace safety: `TryResolveArtifactFullPath`, `ResolveScopedManagedRelativePath`, `IsWithinWorkspace`, `ResolveProviderNativeBrowserProjectedRelativePath`, `ResolveExpectedManagedArtifactRelativePaths`, `ResolveWorkspaceWrittenArtifactRelativePath`, `TryResolveWorkspaceWrittenArtifactSourceFullPath`.
- Matching and identity: `ResolveArtifactExpectation`, `ResolveArtifactExpectationId`, `ProcessMockArtifactMatchesExpectation`, `WorkspaceWrittenFileMatchesExpectedArtifact`, `ExistingManagedArtifactFileMatches`, `HasProjectedArtifactExpectationExternalReference`.
- Classification: `ResolveProcessArtifactKind`, `ResolveStorageContentKind`, `GuessContentTypeFromPath`, `BuildStorageRelativePath`.
- Session/observation: `ResolveSuccessfulSessionFileWrites`, `ResolveSuccessfulWorkspaceFileMutationReceiptPaths`, `ResolveSuccessfulBrowserToolOutputFiles`, `ResolveProviderNativeBrowserWorkingDirectory`.
- Browser: `ResolveProviderNativeBrowserToolName`, `MatchesExpectedBrowserOutputFile`, `IsProviderNativeBrowserArtifactPath`, `BuildProviderNativeBrowserArtifactTitle`.
- Response and decision: `ShouldProjectResponseTextArtifacts`, `ResolveProjectableResponseArtifactText`, `IsUsableProjectedResponseArtifactContent`, `TryResolveResponseTextArtifactRelativePath`, completed-decision helpers.
- Lineage: `BuildArtifactProjectionLineage` and recovery context helpers.
