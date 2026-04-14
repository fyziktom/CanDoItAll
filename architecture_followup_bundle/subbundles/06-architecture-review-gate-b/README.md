# Architecture review gate B

## Status

- Completed

## Objective

- Stop after schema FK hardening and dependency uniqueness repair, then verify that the DB now protects the model strongly enough to justify lifecycle work.

## Covered Inputs

- See `C:\repositories\CanDoItAll\architecture_followup_bundle\02-open-findings.md`, `C:\repositories\CanDoItAll\architecture_followup_bundle\requirements\01-normalized-requirements.md`, and `C:\repositories\CanDoItAll\architecture_followup_bundle\traceability\01-finding-to-subbundle-map.md` for the owning findings and requirements.

## Prerequisites

- Follow the dependencies recorded in `C:\repositories\CanDoItAll\architecture_followup_bundle\codex\TASKS.json` and `C:\repositories\CanDoItAll\architecture_followup_bundle\plan\01-phase-plan.md`.

## Exact Source References

- C:\repositories\CanDoItAll\architecture_followup_bundle\02-open-findings.md
- C:\repositories\CanDoItAll\architecture_followup_bundle\templates\review-gate-memo-template.md
- C:\repositories\CanDoItAll\architecture_followup_bundle\subbundles\04-process-schema-referential-integrity-hardening\README.md
- C:\repositories\CanDoItAll\architecture_followup_bundle\subbundles\05-null-safe-dependency-uniqueness-and-db-invariants\README.md

## Dependency Impact

- Downstream work remains blocked until this subbundle's progression gate is satisfied from fresh proof.

## Validation Depth

- `Critical gate`

## Implementation Steps

1. Audit the listed source references against the current live repository state.
2. Implement only the smallest correct change set for this subbundle.
3. Run the required proof commands and capture the resulting artifacts while the state is fresh.
4. Update `C:\repositories\CanDoItAll\architecture_followup_bundle\reviews\01-execution-report.md` and any gate log or follow-up artifact before allowing downstream work to continue.

## Scope Exceptions

- Do not widen this subbundle beyond the stated objective. If the work uncovers a later-phase defect, record it and stop at the correct boundary.

## Do Not Do

- Do not widen scope into later numbered phases just because the same files are nearby.
- If any answer is no, stop and open the DB-integrity corrective playbook before continuing.
- Do not mark the subbundle complete until the progression gate can be answered explicitly from real proof.

## Acceptance Checklist

- Satisfy the deliverables and review questions preserved below.

## Proof Required

- Run the validation commands preserved below and record the resulting artifacts in the live execution report.

## Browser Validation Logging

- N/A unless this phase unexpectedly changes visible `/processes` behavior. If it does, capture fresh Playwright proof before closure.

## Progression Gate

- This phase is complete only when its acceptance checklist and proof artifacts are satisfied strongly enough for the next dependency to proceed without borrowing trust.

## Suggested Agent Prompt

```text
Implement only subbundle 06-architecture-review-gate-b. Stop after schema FK hardening and dependency uniqueness repair, then verify that the DB now protects the model strongly enough to justify lifecycle work. Respect the prerequisites, stop rules, and proof contract, update the live execution report from fresh evidence, and do not continue downstream until the progression gate is explicitly satisfied.
```

## Preserved Bundle Notes

## Purpose

Stop after schema FK hardening and dependency uniqueness repair, then verify that the DB now protects the model strongly enough to justify lifecycle work.

## Required deliverables

- A written Gate B memo with explicit pass/fail decision.
- A statement of whether the DB now enforces the Process graph strongly enough.
- A corrective subbundle if FK/invariant hardening still leaks through application-only assumptions.

## Repository touchpoints

- `02-open-findings.md`
- `templates/review-gate-memo-template.md`
- `subbundles/04-process-schema-referential-integrity-hardening/README.md`
- `subbundles/05-null-safe-dependency-uniqueness-and-db-invariants/README.md`

## Validation commands

- `Review the live repository, migrations, and new schema/invariant tests before continuing.`

## Review questions

1. Are the remaining Process graph invariants now backed by the DB rather than only by service code?
2. Did FK and uniqueness hardening preserve differential save behavior without reopening major graph corruption risk?
3. Is it now safe to proceed to lifecycle hardening without a corrective persistence redesign?

## Corrective trigger

If any answer is no, stop and open the DB-integrity corrective playbook before continuing.

## Corrective template

- `subbundles/_corrective-db-integrity-reset`

## Gate notes

This gate should fail if the only way to keep the save path working is to leave the schema materially under-enforced.
