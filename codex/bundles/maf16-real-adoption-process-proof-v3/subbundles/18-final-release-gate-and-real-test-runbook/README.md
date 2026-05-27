# SB18: 18-final-release-gate-and-real-test-runbook

## Goal

Final gate before user performs real tests.

## Required work

- Run full focused validation.
- Write a short release-readiness report.
- Create exact runbook for the next real live process test.
- Include abort criteria and expected diagnostics for each process step.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB18` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Close the bundle and provide the next live-run gate.

## Covered Inputs

- RQ10 final release gate and runbook.

## Prerequisites

- SB02, SB10, SB11, and SB13 focused proof passes.

## Exact Source References

- `repo://codex/bundles/maf16-real-adoption-process-proof-v3/reviews/01-execution-report.md`
- `repo://codex/bundles/maf16-real-adoption-process-proof-v3/scripts/validation-commands.md`

## Deliverables

- Completed execution report, proof manifest, semantic invariant, and runbook notes.

## Dependency Impact

- This is the final closure gate for the bundle.

## Validation Depth

- Focused tests plus prepared and completed bundle validators.

## Implementation Steps

- Run focused validations.
- Run bundle validators.
- Record residual risks and next live-run abort criteria.

## Do Not Do

- Do not report final readiness while bundle validators fail.

## Acceptance Checklist

- Completed-stage bundle validation passes.

## Proof Required

- `proof/SB18/manifest.md` and `proof/SB18/semantic-invariants.md`.

## Browser Validation Logging

- No browser route is changed by this bundle.

## Progression Gate

- Final closure requires validator and focused test proof.

## Suggested Agent Prompt

Close the bundle only after focused tests and bundle validators pass with proof artifacts recorded.
