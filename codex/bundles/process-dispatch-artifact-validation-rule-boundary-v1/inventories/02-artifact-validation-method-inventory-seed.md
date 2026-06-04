# Artifact Validation Method Inventory

Status: refreshed from live source in SB02.

## Source Snapshot

- Source file: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- Current line count: 3931
- Method declaration rows found: 188
- Side-effect indicator rows found: 57
- Method proof: `bundle://proof/SB02/transcripts/method-inventory.txt`
- Side-effect proof: `bundle://proof/SB02/transcripts/side-effect-scan.txt`
- Test-surface proof: `bundle://proof/SB02/transcripts/test-surface-scan.txt`

## Validation Rule Families

| Method/Region | Current category | Side effects? | Candidate helper |
| --- | --- | --- | --- |
| `ResolveArtifactExpectation*`, `ResolveArtifactExpectationId*` | expectation resolution against candidate state | No external side effects; reads provided collections | typed validation snapshot + expectation resolver |
| `MatchExpectedArtifactId*` | matching orchestration | No external side effects | expectation matcher facade or matcher rules |
| `MatchesExpectedArtifact`, `MatchesExpectedArtifactByTitleTokens` | path/title/slug matching | No external side effects; uses `Path` parsing only | title/path rules |
| `MatchExpectedArtifactIdByTextContent`, `HasExpectedArtifactContentSignals`, `TokenizeArtifactContentSignalText` | text-content matching | No external side effects; consumes in-memory text/content | content signal rules |
| `WorkspaceWrittenFileMatchesExpectedArtifact`, `WorkspaceMutationReceiptMatchesExpectedArtifact` | workspace write receipt matching | No file writes; path normalization and receipt inspection | path/managed-artifact rules |
| `ScoreProviderNativeVisualArtifactExpectation`, visual/screenshot token helpers | provider-native visual proof scoring | No external side effects | provider-native visual rules |
| `ResolveMissingConcreteProofSummary`, `ContainsConcreteBrowserProofSignal`, browser proof signal helpers | concrete browser proof validation | No external side effects | quality validation rules |
| `ResolveInvalidQualityValidationProofSummary`, quality evidence text helpers | build/test/browser quality validation | No external side effects | quality validation rules |
| `ResolveIncompleteImplementationSummary`, placeholder/punt helpers | placeholder and incomplete-implementation validation | No external side effects | placeholder rules |
| `ResolveDowngradedProjectStructureRequirementSummary`, `ResolveProjectStructureRequiredArtifactPaths`, `ScoreProjectStructureArtifactPathMatch` | project-structure requirement preservation | No external side effects | project-structure preservation rules |
| `Build*ExternalReferenceKey`, `ResolveProjectedArtifactTrustStatus`, completed-decision helpers | projection identity/trust helpers | No external side effects | keep stable or move only when projection contract requires it |
| `PrepareManagedArtifactPathForPrompt`, `TryResolve*FullPath`, `BuildResolvedArtifactInputs` | prompt preparation and artifact input orchestration | File-system read/copy/path side effects exist | dispatcher orchestration; do not move as pure rule helpers |

## Side-Effect Ownership

| Side-effect indicator | Current owner | Extraction decision |
| --- | --- | --- |
| `File.Exists`, `Directory.CreateDirectory`, `File.Copy` around managed prompt path preparation | `ProcessRunAutomationDispatchService.ArtifactValidation.cs` orchestration | Keep in dispatcher for this bundle. Rule helpers may accept normalized path facts, not perform I/O. |
| `Path.GetFullPath`, `Path.Combine`, `Path.GetFileName*`, extension checks | Mixed path normalization and pure matching | Pure normalization can move when it is not coupled to file-system existence or copy operations. |
| `DateTimeOffset.MinValue` freshness comparisons | matching/record freshness rules | Move only through typed snapshots that preserve current-run timing semantics. |
| `JsonSerializer` and artifact content parsing | validation/content inspection | Keep parsing boundary explicit; pure content classification may move later. |
| `RecordArtifactAsync`, `SaveChangesAsync`, `storagePlacementService.PlaceAsync` | None in this file's validation-rule candidates | Out of scope for validation helper extraction. |

## Existing Regression Anchors

| Rule area | Existing tests |
| --- | --- |
| Title/path/text matching | `MatchExpectedArtifactId_*`, `WorkspaceWrittenFileMatchesExpectedArtifact_*` in `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` |
| Managed/shallow artifact path validation | `IsShallowSharedManagedArtifactPath_*`, `ResolveShallowSharedManagedArtifactReferenceSummary_*` |
| Provider-native browser visual evidence | `MatchExpectedArtifactId_*provider_native_browser*`, `ResolveMissingRequiredArtifactSummary_*browser*`, `IsProviderNativeBrowserArtifactPath_*` |
| Placeholder and quality validation | `ArtifactContractValidation_rejects_placeholder_record_for_required_artifact`, `ResolveInvalidQualityValidationProofSummary_*`, placeholder repair tests |
| Project-structure preservation | `ResolveDowngradedProjectStructureRequirementSummary_*`, `ResolveProjectStructureRequiredArtifactPaths_*`, project-structure grounding tests |
| Boundary guardrails | `Artifact_boundary_helpers_stay_inside_processes_module_without_core_project`, `Artifact_boundary_bundle_proof_paths_do_not_contain_mobile_or_small_screen_artifacts` |

## SB02 Cutline

- SB03 may design typed snapshots because the live inventory separates pure validation facts from dispatcher-owned file-system orchestration.
- Do not move methods that read, copy, or probe files until a later orchestration boundary owns those effects explicitly.
- Treat the 3931-line count as the current baseline for later line-count and parity gates.
