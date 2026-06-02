# SB02 Proof Manifest

## Subbundle

SB02 - Process Dispatch And Runtime Refactor

## Implementation Summary

Added a dedicated current-run artifact lineage validator and wired it into upstream artifact input resolution, completion artifact validation, and finalization wrong-root checks. The validator rejects stale process-run ids, stale or unrelated managed run roots, product-root paths, and mismatched producer lineage while preserving existing valid scoped current-run output and process-mock paths. Added failing-first and passing integration tests for stale/current upstream artifacts, then reran the existing completion artifact characterization slice and the SB01 drift scanner.

## Changed Files

See `proof/SB02/changed-file-hashes.md`.

## Command Transcripts

| Transcript | Purpose | Result |
| --- | --- | --- |
| `proof/SB02/transcripts/failing-first-stale-artifact-inputs.txt` | Proved stale process-run and product-root upstream artifacts were accepted before production wiring. | Expected failure captured before the validator was wired. |
| `proof/SB02/transcripts/passing-stale-artifact-inputs.txt` | Built and ran the final stale/current upstream artifact input tests. | Exit code 0; 3 passed, 0 failed. |
| `proof/SB02/transcripts/artifact-contract-regression-tests.txt` | Ran existing completion artifact and wrong-root characterization tests. | Exit code 0; 23 passed, 0 failed. |
| `proof/SB02/transcripts/drift-scanner-after-sb02.txt` | Re-ran SB01 contract drift scanner after SB02 changes. | Exit code 0; 6 passed, 0 failed. |
| `proof/SB02/transcripts/cancellation-token-rg.txt` | Captured cancellation-token audit for dispatch/runtime surfaces. | Exit code 0. |
| `proof/SB02/transcripts/source-assertions.txt` | Captured source locations for the validator, call sites, and tests. | Exit code 0. |
| `proof/SB02/transcripts/anti-stub-audit.txt` | Searched touched production files for stubs and Tetris-specific logic. | Exit code 0; no matches. |
| `proof/SB02/transcripts/prepared-validator-after-sb02.txt` | Revalidated prepared bundle structure after SB02 closure docs/proof updates. | Exit code 0. |

## Source Assertions

- `ProcessArtifactLineageValidator` is defined in `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactLineageValidator.cs`.
- `BuildResolvedArtifactInputs` filters candidate upstream records through `IsCurrentRunUpstreamArtifactInput`.
- `ProcessCompletionArtifactValidator` delegates current-run and producer-lineage checks to `ProcessArtifactLineageValidator.ValidateCurrentRunArtifact`.
- `StepCompletionFinalizer.IsWrongRootArtifact` delegates managed-root checks to `ProcessArtifactLineageValidator.ValidateManagedStorageBoundary`.
- `BuildResolvedArtifactInputs_rejects_stale_process_run_artifact`, `BuildResolvedArtifactInputs_rejects_current_run_artifact_outside_managed_root`, and `BuildResolvedArtifactInputs_accepts_current_run_managed_artifact` cover the negative and positive upstream-input behavior.

## Semantic Proof

- Adversarial negative proof: `BuildResolvedArtifactInputs_rejects_stale_process_run_artifact` rejects an otherwise matching required upstream artifact when its `ProcessRunId` and managed path belong to another run.
- Adversarial negative proof: `BuildResolvedArtifactInputs_rejects_current_run_artifact_outside_managed_root` rejects an otherwise matching current-run artifact that points at a product root.
- Semantic positive proof: `BuildResolvedArtifactInputs_accepts_current_run_managed_artifact` accepts a matching current-run artifact under the current run managed root.
- Characterization proof: the existing completion artifact and wrong-root slice preserves stale-run rejection, current-run acceptance, manager recovery handling, scoped current-run output paths, and process-mock output paths.
- Drift proof: the SB01 drift scanner still passes after the new validator and tests.

## Shallow-Pass Trap

This proof does not rely on adding a validator type alone. The failing-first transcript shows the bug before wiring, the passing tests exercise the final call sites, and the characterization slice verifies that the stricter classifier did not regress existing valid current-run output behavior.

## Anti-Stub Audit

`proof/SB02/transcripts/anti-stub-audit.txt` found no production `TODO`, `NotImplemented`, `throw new NotImplementedException`, `workspace_destroy_everything`, or `Tetris` matches in the touched SB02 production files.

## Raw Note Literal Closure

| Raw note area | SB02 closure |
| --- | --- |
| Stale lineage and artifact proof fragility | Upstream artifact input selection and completion validation now require current process-run and producer lineage binding. |
| Wrong root artifact acceptance | Shared managed-boundary validator rejects product roots, stale run roots, and shared output roots without current-run boundaries. |
| Preserve behavior while hardening | Existing completion artifact regression slice passes after the validator fix. |
| CancellationToken.None paths | Audit captured; only remaining occurrences are behind the existing synchronous content-reader interface. |
| Preserve genericity | No production Tetris-specific logic introduced. |

## Dependency Smoke Proof

The focused integration commands build the process module, composition graph, web host dependencies, test support, and integration tests. SB04/SB07/SB08 can depend on the strengthened artifact lineage checks without accepting stale recorded artifacts as current-run proof.

## Production Behavior Artifact Matrix

| Artifact or validator record | Behavior impact | Persistence/API impact | Proof |
| --- | --- | --- | --- |
| `ProcessArtifactLineageValidationContext` | Carries current process, step, execution, workflow, subprocess, and recovery ids into artifact validation. | Internal only; no persisted schema or public DTO change. | Source assertions and integration tests. |
| `ProcessArtifactLineageValidationResult` | Returns explicit invalid diagnostics instead of silently accepting stale lineage. | Internal only; diagnostics flow through existing validation failure paths. | Artifact contract regression tests. |
| `ProcessArtifactLineageValidator.ValidateManagedStorageBoundary` | Centralizes product-root, stale-run-root, scoped output, and process-mock boundary classification. | Internal only; no storage layout migration. | Wrong-root characterization test and stale input tests. |
| `ProcessArtifactLineageValidator.ValidateCurrentRunArtifact` | Requires artifacts to bind to the current run and current producer lineage before satisfying completion expectations. | Internal only; no persisted schema change. | Completion artifact regression tests. |
