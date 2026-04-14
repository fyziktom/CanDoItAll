# Architecture review gate A

## Status

- Prepared

## Objective

- Stop after proof reset and DAG hardening to verify that the repository no longer permits an illegal dependency graph before runtime/schema follow-up continues.

## Covered Inputs

- See `02-open-findings.md`, `requirements/01-normalized-requirements.md`, and `traceability/01-finding-to-subbundle-map.md` for the owning findings and requirements.

## Prerequisites

- Follow the dependencies recorded in `codex/TASKS.json` and `plan/01-phase-plan.md`.

## Exact Source References

- C:\repositories\CanDoItAll\arch_post_followup_bundle\02-open-findings.md
- C:\repositories\CanDoItAll\arch_post_followup_bundle\reviews\01-architecture-gate-memo-log-template.md
- C:\repositories\CanDoItAll\arch_post_followup_bundle\templates\review-gate-memo-template.md
- C:\repositories\CanDoItAll\arch_post_followup_bundle\subbundles\02-process-graph-dag-invariant-hardening\README.md

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
- If any answer is weaker than an explicit yes, create and finish a corrective graph-invariant subbundle before continuing.

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
Implement only subbundle 03-architecture-review-gate-a. Stop after proof reset and DAG hardening to verify that the repository no longer permits an illegal dependency graph before runtime/schema follow-up continues. Respect the prerequisites, stop rules, and proof contract, update the live execution report from fresh evidence, and do not continue downstream until the progression gate is explicitly satisfied.
```

## Preserved Bundle Notes

### Purpose
Stop after proof reset and DAG hardening to verify that the repository no longer permits an illegal dependency graph before runtime/schema follow-up continues.

### Required deliverables
- A written Gate A memo with an explicit pass/fail decision.
- An explicit answer on whether graph legality is truly enforced at the service boundary.
- An updated queue state that blocks all downstream work if Gate A fails.

### Repository touchpoints
- `02-open-findings.md`
- `reviews/01-architecture-gate-memo-log-template.md`
- `templates/review-gate-memo-template.md`
- `subbundles/02-process-graph-dag-invariant-hardening/README.md`

### Validation commands
- `Review the live repository, new tests, and generated proof artifacts for subbundles 01-02.`

### Review questions
1. Is graph legality now enforced at save/publish time, including self-loops and larger cycles?
2. Are the runtime and canvas paths free of silent root/topological fallbacks for invalid graphs?
3. Do the new tests prove rejection of invalid graphs and preservation of valid DAG behavior?

### Corrective trigger
If any answer is weaker than an explicit yes, create and finish a corrective graph-invariant subbundle before continuing.

### Corrective template
- `subbundles/_corrective-graph-invariant-reset`

### Detailed execution notes
- Do not proceed to runtime/schema singularity work on top of a graph model that is still allowed to be illegal.

