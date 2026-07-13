# live-run-escalation-diagnosis

## Status

- `Completed`

## Objective

- Identify why the current 5032 Calculator multiteam run escalates and which process contracts, assignments, or artifacts made the loop possible.

## Success Criteria

- Root, parent, and child run ids are recorded.
- Failing step keys, statuses, attempts, roles, allowed operations, and target scopes are captured.
- Diagnosis distinguishes template/contract bugs from external provider or app-specific failures.

## Covered Inputs

- R1, R4, R7.

## Prerequisites

- 5032 development instance reachable or PostgreSQL runtime tables available.

## Exact Source References

- `C:\Users\lucys\AppData\Local\CanDoItAll\workspace\artifacts\scopes\organization\e5df9ad633dbc6974a0678a74976013c\process-runs`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\dotnet-development-slice\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\dotnet-feature-function-implementation\definition.json`

## Deliverables

- Execution-report section with live run evidence and root cause.

## Dependency Impact

- SB02 and SB03 depend on this diagnosis. If the root cause changes, downstream edits must be reopened.

## Validation Depth

- Process-critical diagnosis.

## Implementation Steps

1. Query 5032 live process state and PostgreSQL runtime assignments.
2. Read scoped process artifacts for the blocking parent and child runs.
3. Compare observed assignments to template definitions.
4. Record the minimal root-cause statement and evidence.

## Scope Exceptions

- Do not repair code or templates in this subbundle.

## Do Not Do

- Do not delete process data.
- Do not infer missing data when SQL/API/artifacts can be queried.

## Acceptance Checklist

- Root cause names the exact contract mismatch.
- Evidence includes at least one live runtime assignment row.
- Evidence explains why HR/readiness did not prevent the mismatch.

## Proof Required

- SQL/API output excerpts in `reviews/01-execution-report.md`.
- Relevant artifact paths or absence noted explicitly.

## Browser Validation Logging

- N/A.

## Progression Gate

- SB02/SB03 may start only after the execution report contains the failing run ids and contract mismatch.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
