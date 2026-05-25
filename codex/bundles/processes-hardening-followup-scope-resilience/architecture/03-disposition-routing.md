# Disposition Routing

## Problem

A process step can be validly completed even when the product/output is not acceptable, if the step's job is to make a decision or route to repair.

Example:

- QA step finds defect.
- QA step has branch `repair-required`.
- Correct: QA step `Completed` with branch `repair-required`.
- Incorrect: QA step `Blocked`.

## Proposed Router

Add `ProcessDispositionRouter` after finalizer validation:

```text
Inputs:
- executor outcome
- artifact validation results
- scope/tool policy violations
- branch outcomes
- step kind
- requires approval
- missing upstream inputs
- recovery attempts
- no-progress fingerprints

Output:
- target process step status
- branch outcome id
- reason
- diagnostic artifact requirement
- recovery or unblock action
```

## Routing Rules

1. If the step cannot access mandatory input, authority, credential, safe target, or required tool, use `Blocked` or `Failed`.
2. If a review/QA/approval/decision step can make a valid negative disposition, complete the step with the repair/no-go/rework/escalation branch.
3. If required artifacts are missing but can be recovered from existing evidence, use manager recovery.
4. If required artifacts are missing because upstream source step omitted them, use upstream materialization lifecycle.
5. If the same failure repeats with no new evidence, stop retrying and route to manager/escalation.
6. If an implementation step did not mutate or validate required deliverable, do not complete; route to repair/retry/fail based on no-progress fingerprint.

## Branch Matching

Branch matching must remain generic. Use normalized branch tags:

```text
accepted
approved
repair-required
rework-required
rejected
no-go
escalate
blocked-inputs
blocked-environment
```

If existing branch definitions do not have tags, infer from key/title with conservative logic and emit a lint warning.
