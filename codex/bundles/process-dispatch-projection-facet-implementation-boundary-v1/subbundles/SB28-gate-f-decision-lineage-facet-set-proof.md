# SB28 - Gate F: decision/lineage/facet-set proof

## Status

Prepared.

## Objective

Validate decision, lineage, and facet-set construction.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- Requirement IDs: see `traceability/02-requirement-to-subbundle-map.md`

## Prerequisites

- Previous subbundle completed.
- If this follows a critical gate, that gate must have passed and have proof artifacts.
- Prepared-stage validation must have passed before SB01 production movement.

## Exact Source References

source scans; tests

## Deliverables

Critical proof manifest.

## Dependency Impact

Phase: P4 Specialized facets.  
Downstream phases may rely on this subbundle. If this subbundle changes source-family order, candidate mutation, or facet dependency boundaries, reopen all downstream projection proof.

## Validation Depth

No `new ProcessArtifactProjectionServices(this)` usage unless it is an allowed tiny factory shim.

## Implementation Steps

1. Open the exact source references above.
2. Make the smallest behavior-preserving change that satisfies the objective.
3. Do not broaden the scope into Process Core, driver APIs, UI work, or public contracts.
4. Run the local/focused checks required for this subbundle.
5. Record proof under `proof/SB28/` during implementation.
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

- [ ] Objective completed.
- [ ] No behavior-changing production intent introduced.
- [ ] Build/focused tests/source scans appropriate for this subbundle pass.
- [ ] No Core/driver/UI/stub drift.
- [ ] Execution report updated.
- [ ] Downstream dependencies checked.

## Proof Required

- Source assertion transcript.
- Focused test transcript when source moved.
- Critical manifest and semantic invariants if this is a critical gate.
- Anti-stub scan for touched production files.

## Browser Validation Logging

N/A. Runtime/service refactor only. If UI files are touched, stop and reopen scope. Do not create small/medium/mobile proof.

## Progression Gate

Critical foundation gate. Downstream subbundles must not continue until this gate passes.

## Suggested Agent Prompt

Implement SB28 only. Preserve behavior. Do not start downstream work early. Record proof before continuing.
