# Architecture review gate B

## Status

- Prepared

## Objective

- Stop after runtime singularity and workspace quiescence work to verify that the runtime now has both DB-backed singularity and deterministic UI action ordering before query/cohesion work begins.

## Covered Inputs

- See `02-open-findings.md`, `requirements/01-normalized-requirements.md`, and `traceability/01-finding-to-subbundle-map.md` for the owning findings and requirements.

## Prerequisites

- Follow the dependencies recorded in `codex/TASKS.json` and `plan/01-phase-plan.md`.

## Exact Source References

- C:\repositories\CanDoItAll\arch_post_followup_bundle\02-open-findings.md
- C:\repositories\CanDoItAll\arch_post_followup_bundle\templates\review-gate-memo-template.md
- C:\repositories\CanDoItAll\arch_post_followup_bundle\subbundles\04-runtime-row-singularity-and-db-uniqueness-hardening\README.md
- C:\repositories\CanDoItAll\arch_post_followup_bundle\subbundles\05-workspace-pending-persistence-quiescence-and-action-ordering\README.md

## Dependency Impact

- Downstream work remains blocked until this subbundle's progression gate is satisfied from fresh proof.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Audit the listed source references against the current live repository state.
2. Implement only the smallest correct change set for this subbundle.
3. Add or update the narrowest test surface that proves the stated invariant.
4. Run the required proof commands and capture fresh artifacts.
5. Update `reviews/01-execution-report.md` or the live execution report and the gate memo log before allowing downstream work to continue.

## Scope Exceptions

- Do not widen this subbundle beyond the stated objective. If the work uncovers a later-phase defect, record it and stop at the correct boundary.

## Do Not Do

- Do not continue into downstream numbered phases just because nearby files are already open.
- Do not mark this subbundle complete until the progression gate can be answered explicitly from real proof.
- If any answer is no, stop and open the runtime-uniqueness or workspace-quiescence corrective playbook before continuing.

## Acceptance Checklist

- Satisfy the deliverables and review questions preserved below.

## Proof Required

- Run the validation commands preserved below and record the resulting artifacts in the live execution report.

## Browser Validation Logging

- Only required if this subbundle changes visible `/processes` UI behavior beyond what component proof already covers.

## Progression Gate

- This phase is complete only when its acceptance checklist and proof artifacts are satisfied strongly enough for the next dependency to proceed without borrowed trust.

## Suggested Agent Prompt

```text
Implement only subbundle 06-architecture-review-gate-b. Stop after runtime singularity and workspace quiescence work to verify that the runtime now has both DB-backed singularity and deterministic UI action ordering before query/cohesion work begins. Respect the prerequisites, stop rules, and proof contract, update the live execution report from fresh evidence, and do not continue downstream until the progression gate is explicitly satisfied.
```

## Preserved Bundle Notes

### Purpose
Stop after runtime singularity and workspace quiescence work to verify that the runtime now has both DB-backed singularity and deterministic UI action ordering before query/cohesion work begins.

### Required deliverables
- A written Gate B memo with an explicit pass/fail decision.
- An explicit statement on whether runtime singularity and workspace action ordering are now materially safe.
- A corrective subbundle if either runtime uniqueness or workspace quiescence still leaks through assumptions.

### Repository touchpoints
- `02-open-findings.md`
- `templates/review-gate-memo-template.md`
- `subbundles/04-runtime-row-singularity-and-db-uniqueness-hardening/README.md`
- `subbundles/05-workspace-pending-persistence-quiescence-and-action-ordering/README.md`

### Validation commands
- `Review the live repository, migrations, and fresh tests for subbundles 04-05.`

### Review questions
1. Does the database now protect runtime singularity strongly enough to match the service code’s assumptions?
2. Can the workspace still publish, delete, or export against stale or racing definition state?
3. Do the new tests prove both the DB uniqueness side and the UI quiescence side?

### Corrective trigger
If any answer is no, stop and open the runtime-uniqueness or workspace-quiescence corrective playbook before continuing.

### Corrective template
- `subbundles/_corrective-runtime-uniqueness-reset`

### Detailed execution notes
- Do not move on to read-side or helper-isolation work while runtime correctness is still conditional.

