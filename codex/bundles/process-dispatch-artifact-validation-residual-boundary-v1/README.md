# Process Dispatch Artifact Validation Residual Boundary v1

Status: Prepared for Codex implementation.

## Mission

Continue the module-local dispatcher isolation work after `process-dispatch-artifact-satisfaction-evidence-boundary-v1`.

The previous bundle moved required-artifact satisfaction and evidence validation decisions into helper boundaries, but `ProcessRunAutomationDispatchService.ArtifactValidation.cs` remains a large residual partial. This bundle must reduce that file safely by extracting residual classification, browser-output, critical-tool-failure, storage/content-kind, and diagnostic helper families.

## Explicit Non-Goals

- Do **not** create `CanDoItAll.Processes.Core`.
- Do **not** introduce production process driver APIs, `IProcessDriverPack`, driver registries, driver packages, or driver discovery.
- Do **not** move EF writes, storage writes, workspace file writes, or external tool calls into helpers that look pure.
- Do **not** change process behavior, artifact branch order, retry behavior, transition behavior, evidence semantics, or existing tool names.
- Do **not** add small, medium, mobile, phone, tablet, Android, iPhone, or responsive proof artifacts. This is runtime/service refactor only; browser validation is expected to be `N/A`.

## Current Reviewed Branch Signals

- Last completed bundle: `process-dispatch-artifact-satisfaction-evidence-boundary-v1`.
- `ArtifactValidation.cs` was reduced from 2695 to 2483 lines but remains a hotspot.
- Existing source assertions confirm helper delegation to:
  - `ProcessArtifactSatisfactionSnapshotBuilder.From`
  - `ProcessArtifactRecordedSatisfactionRules.HasRecordedExpectedArtifact`
  - `ProcessFreshImplementationArtifactSatisfactionRules.HasFreshCurrentAttemptImplementationArtifact`
  - `ProcessRequiredArtifactAutoSatisfactionRules.CanAutoSatisfyRequiredArtifact`
  - `ProcessQualityValidationEvidenceAggregator.ResolveEvidenceTexts`
  - `ProcessIncompleteImplementationSignalRules.ResolveIncompleteImplementationSummary`
  - `ProcessExternalTargetReferenceGuard.ResolveOutOfScopeReferenceSummary`
  - `ProcessShallowManagedArtifactReferenceGuard.ResolveSummary`
  - `ProcessManagedArtifactPathClassificationRules.IsTextReadableManagedArtifactPath`

## Strategy

This is still not the Process Core split. It is a deeper module-local boundary pass that prepares a future core/driver architecture by stabilizing vocabulary and helper seams first. Helper contracts should remain internal to `CanDoItAll.Modules.Processes`.

## Expected Final State

- `ProcessRunAutomationDispatchService.ArtifactValidation.cs` remains an orchestration partial, not a dumping ground for classification and browser-output heuristics.
- New helper files are module-local and side-effect scoped.
- `ArtifactValidation.cs` line count should decrease materially; target: below 2200 lines unless a documented blocker explains why.
- Existing behavior is proven by focused artifact-contract, recovery-routing, critical-failure, browser-output, and line-count/source scans.
- Driver readiness is updated only as documentation.
