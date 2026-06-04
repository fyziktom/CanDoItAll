# Process Dispatch Artifact Write Coordinator Expansion v1

Bundle preparation status: `Ready`
Bundle readiness gate: `Ready for Codex execution after repo-root validation`
Execution status: `Completed`
Profile: `initiative`

## Validation Summary

- Bundle preparation status: Ready after structural repair and prepared-stage validator rerun.
- Bundle readiness gate: Passed live prepared-stage validator on 2026-06-04.
- Execution status: SB01-SB14 completed.
- Subbundle gate review: SB01-SB14 entry and closure passed; Gate A, Gate B, Gate C, and final red-team closure passed.
- Final closure gate: Passed completed-stage validator after SB14.
- Browser validation analytics: N/A expected for runtime/service refactor; only large desktop/PC proof allowed if UI proof becomes unavoidable.
- Prepared-stage validation must pass from the repo root before production implementation starts.
- Browser validation is expected to be N/A for this runtime/service refactor unless an unexpected UI-visible change is introduced.
- Final closure completed with completed-stage bundle validation, focused tests, source scans, and note-by-note closure in `reviews/01-execution-report.md`.

## Purpose

Continue the `maf-processes-refactor` dispatcher decomposition without starting Process Core extraction. The previous artifact source-adapter bundle introduced typed projection source adapters and a first write coordinator, but only the execution-artifact path uses the coordinator end-to-end. This bundle expands the write boundary across the remaining artifact projection paths in small, proof-backed steps.

The goal is to make artifact write side effects explicit and reusable while preserving every existing behavior: external-reference keys, projection lineage, duplicate skipping, managed storage placement, DB artifact records, trust status, required-artifact satisfaction, receipt/provider metadata, and recovery lineage.

## Current State Summary

Source-backed current facts this bundle assumes:

- `ProcessArtifactProjectionSourceAdapters.cs` now owns typed planning for process mock, workspace-written, existing-managed, response-text, and provider-native browser projection sources.
- `ProcessArtifactProjectionWriteCoordinator.cs` exists, but currently coordinates only storage placement + artifact recording for the execution-artifact projection path.
- `ProcessRunAutomationDispatchService.ArtifactProjection.cs` now routes all storage-backed artifact projection writes through the write coordinator and the completed-decision record-only path through a record-only coordinator. The only remaining `RecordArtifactAsync` reference in `ArtifactProjection.cs` is the service delegate helper.
- `ArtifactProjection.cs` remains large; the last proof reported `ArtifactProjection.cs` at 1526 lines and `ArtifactValidation.cs` at 3434 lines after the source-adapter step.

## Scope

In scope:

- Harden `ProcessArtifactProjectionWriteCoordinator` so all storage-backed projection paths can use the same write result model.
- Migrate one projection source at a time through the coordinator.
- Add a record-only coordinator/helper for completed-decision artifacts without forcing storage placement.
- Keep source semantics in source adapters/planners; keep write side effects in the write coordinator.
- Add focused tests and source scans for every migrated path.
- Keep all work inside the existing Processes module unless a neutral contract already exists.

Out of scope:

- No `CanDoItAll.Processes.Core` project.
- No process driver packs.
- No EF entity movement.
- No UI rewrite.
- No MAF/Tooling product dependency changes except guardrail scans.
- No process tool renames or access-policy changes.

## Large-screen-only proof policy

This is runtime/service refactoring. Browser proof is expected to be `N/A`. Do **not** run small, medium, mobile, phone, tablet, iPhone, Android, or responsive screenshots. If a visual proof unexpectedly becomes necessary, use only a PC/desktop large-screen viewport and record why UI proof was unavoidable.

## Refactor Gate Rhythm

- Gate A after SB04: coordinator contract is hardened and guarded before more production movement.
- Gate B after SB08: process mock, workspace-written, and existing-managed write paths are migrated and parity-tested.
- Gate C after SB12: response-text, provider-native browser, and record-only decision paths are migrated or explicitly cut-lined.
- Final closure after SB14.

## Expected Next Cutline After This Bundle

If this bundle completes cleanly, the next safe dispatcher isolation candidate is either:

1. artifact validation rule extraction from `ArtifactValidation.cs`, or
2. required-tool/tool-validation boundary extraction from `ToolValidation.cs`.

Do not start Process Core extraction until dispatcher artifact/write/validation boundaries are smaller and better covered.
