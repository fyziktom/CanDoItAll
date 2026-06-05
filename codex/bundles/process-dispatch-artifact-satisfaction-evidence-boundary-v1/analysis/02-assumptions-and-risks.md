# Assumptions And Risks

## Assumptions

- The current branch already contains all previous helper boundaries.
- Existing integration tests in `ProcessRunAutomationDispatchServiceTests` are the main safety net.
- Any helper extraction must preserve private dispatcher wrapper signatures unless a test proves no caller depends on them.
- `ArtifactValidation.cs` may still contain generic and SW-dev-specific evidence logic; this bundle isolates but does not reclassify into drivers.

## Critical Path Risks

- **Branch ordering drift**: Auto-satisfaction checks are order-sensitive. A helper that changes precedence can falsely satisfy missing artifacts.
- **Provider-native browser false positives**: Screenshot/snapshot detection can accidentally satisfy evidence without actual output file proof.
- **Response text over-acceptance**: Narrative response text must not become a loophole for deliverable evidence.
- **External target false positives**: Allowed alias logic must not block real product files or permit out-of-scope references.
- **Hidden side effects**: Pure helpers must not start reading/writing files, querying DB, or mutating process state.
- **Driver API creep**: Documentation-only evidence vocabulary must not become production driver code in this bundle.

## Validation Risks

- A compile-only refactor could pass while losing artifact branch semantics.
- Focused tests may miss negative cases unless they include false-positive evidence scenarios.
- Existing fixtures may contain .NET-heavy assumptions; include business/document/spreadsheet vocabulary in documentation-only readiness maps.

## Reopen Triggers

Reopen the last movement subbundle if:

- a helper changes artifact satisfaction order;
- `ArtifactValidation.cs` bypasses new helper wrappers inconsistently;
- any no-core/no-driver scan finds production code;
- any helper references UI/Razor/Maf adapter;
- any test shows different `ProcessStepRunStatus`, `ProcessArtifactValidationStatus`, or missing artifact summary;
- browser proof artifacts are created for small/medium/mobile viewports.
