# SB01: 01-current-state-source-and-proof-audit

## Goal

Verify current head, claimed proof, and actual source state.

## Required work

- Open current head, previous bundle execution report, package files, MAF adapter files, process artifact files, and live-run profile files.
- Create a short audit table: claim, source proof, test proof, confidence, remaining risk.
- Fail this subbundle if any previous proof references a file/path that does not exist.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB01` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Verify the current source and proof state before accepting runtime claims.

## Covered Inputs

- RQ01 current-state audit.

## Prerequisites

- Bundle analysis files are present.

## Exact Source References

- `repo://codex/bundles/maf16-real-adoption-process-proof-v3/analysis/01-current-state.md`

## Deliverables

- Source and proof audit recorded in the final execution report.

## Dependency Impact

- SB02 through SB18 depend on this audit baseline.

## Validation Depth

- Structural bundle validation and source inspection.

## Implementation Steps

- Review package, runtime, and proof documents.
- Record closure in the execution report.

## Do Not Do

- Do not treat old proof as current without rechecking source.

## Acceptance Checklist

- Current-state analysis exists and is cited.

## Proof Required

- Final report and bundle validator proof.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- Source audit must complete before capability proof.

## Suggested Agent Prompt

Review source and bundle proof before making runtime changes.
