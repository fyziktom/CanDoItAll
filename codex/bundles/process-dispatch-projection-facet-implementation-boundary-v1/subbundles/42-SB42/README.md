# SB42 - Delete or shrink `ProcessArtifactProjectionServices`

## Status

- Completed

## Objective

Delete all-facet implementation or reduce it below 80 lines as a factory only.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- Requirement IDs: see `traceability/02-requirement-to-subbundle-map.md`

## Prerequisites

- Previous subbundle completed.
- If this follows a critical gate, that gate must have passed and have proof artifacts.
- Prepared-stage validation must have passed before SB01 production movement.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacetImplementations.cs
## Deliverables

- Deleted/shrunk file.
## Dependency Impact

- Phase: P6 Remove transitional adapter.
- Downstream phases may rely on this subbundle. If this subbundle changes source-family order, candidate mutation, or facet dependency boundaries, reopen all downstream projection proof.
## Validation Depth

- Source scan checks no class implements more than three projection facets.
## Implementation Steps

1. Open the exact source references above.
2. Make the smallest behavior-preserving change that satisfies the objective.
3. Do not broaden the scope into Process Core, driver APIs, UI work, or public contracts.
4. Run the local/focused checks required for this subbundle.
5. Record proof under `proof/SB42/` during implementation.
6. Update `reviews/01-execution-report.md`.

## Scope Exceptions

- Process Core extraction is explicitly out of scope.
- Production driver APIs are explicitly out of scope.
- UI/mobile/small/medium proof is explicitly out of scope.

## Do Not Do

- Do not create `CanDoItAll.Processes.Core`.
- Do not create `IProcessDriverPack`, `IProcessDriverRegistry`, or driver packages.
- Do not touch `.razor`, `.css`, `.js`, `.ts`, `.tsx`, `.jsx`, or screenshot proof paths.
- Do not change projection source-family order.
- Do not remove or weaken existing tests.

## Acceptance Checklist

- [x] Objective completed.
- [x] No behavior-changing production intent introduced.
- [x] Build/focused tests/source scans appropriate for this subbundle pass.
- [x] No Core/driver/UI/stub drift.
- [x] Execution report updated.
- [x] Downstream dependencies checked.

## Proof Required

- Source assertion transcript.
- Focused test transcript when source moved.
- Critical manifest and semantic invariants if this is a critical gate.
- Anti-stub scan for touched production files.

## Browser Validation Logging

- N/A. Runtime/service refactor only. If UI files are touched, stop and reopen scope. Do not create small/medium/mobile proof.
## Progression Gate

- Standard progression gate. Continue only after proof is recorded.
## Suggested Agent Prompt

Implement SB42 only. Preserve behavior. Do not start downstream work early. Record proof before continuing.




