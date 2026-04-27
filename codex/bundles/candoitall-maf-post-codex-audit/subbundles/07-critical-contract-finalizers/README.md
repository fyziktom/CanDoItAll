# Subbundle 07 — Finalizers for Critical DTOs

## Goal

Extend or explicitly scope finalizer tools beyond `ProcessStepOutcomeResult`.

## Current problem

Only process-step outcome has a finalizer. Other critical decision contracts are still structured-output-only.

## Implementation tasks

1. Classify contracts by criticality.

Suggested classification:

| Contract | Critical? | Finalizer required? |
|---|---:|---:|
| ProcessStepOutcomeResult | yes | yes |
| CodeReviewResult | yes | yes for pass/fail gating |
| ArchitectureReviewResult | yes | yes for approval/rejection |
| ImplementationPlanResult | medium/high | yes when it drives execution automatically |
| TestPlanResult | medium | optional/required by workflow policy |
| ToolExecutionDecisionResult | yes | yes |
| ProcessStatePatch | yes | yes + approval |

2. Implement finalizer policies for selected critical contracts.

Example names:

```text
submit_process_step_outcome
submit_code_review_result
submit_architecture_review_result
submit_implementation_plan
submit_tool_execution_decision
submit_process_state_patch
```

3. Avoid generic catch-all finalizer tools.

Each finalizer should have a clear typed signature and description.

4. Update capture logic.

`CreateFinalizerCapture(...)` should attach the correct finalizer tool for the contract, not only `ProcessStepOutcomeResult`.

5. Tests.

Required tests:

- Exact-one finalizer per critical contract.
- Wrong finalizer for contract fails.
- Multiple finalizers fail in required mode.
- Finalizer JSON validates with the matching validator.
- Finalizer output replaces machine response only in required mode.

## Acceptance gate

Every contract that can approve, reject, patch, deploy, write, or advance a workflow automatically must either have a required finalizer or a documented exception.

## Execution Result

Status: Complete. Typed finalizer policies and MAF capture tools now cover the registered critical DTO contracts.
