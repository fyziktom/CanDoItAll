# Task 02 – Replace first-failure completion validation with an aggregate gate evaluator

## Problem

`AgentFrameworkProcessExecutionAdapter.ResultConversion.cs` currently short-circuits completion validation. In the incident it returned only:

```text
process.adapter.product_required_file_content_missing
```

It did not also persist the missing:

```text
process.adapter.product_required_tool_receipt_missing: workspace_pwsh_run_script
```

because product file content validation runs before required product tool receipt validation.

## Implementation

1. Introduce `IProcessCompletionGateEvaluator`.
2. Move existing private static validation methods behind small gate classes or static adapter methods that return collections.
3. Evaluate all gates for `Completed` output.
4. Return:
   - `IsSatisfied`,
   - `PrimaryIssue`,
   - `Issues`,
   - stable evidence hash/fingerprint.
5. Keep the current diagnostic codes for compatibility but allow a new aggregate code:

```text
process.adapter.completion_gates_unsatisfied
```

6. The aggregate issue should include secondary diagnostic refs in the safe summary or in a structured payload.

## Priority rules for primary issue

Use a deterministic priority order, for example:

1. unsafe/policy/security violations,
2. missing current-run required tool receipt,
3. failed required product readback/content check,
4. missing required product path,
5. missing managed artifact write/materialization,
6. ungrounded evidence refs,
7. declared blockers in Completed output.

For the incident, primary should preferably be missing `workspace_pwsh_run_script`, with file-content failure as secondary, because the repair action is to run the helper.

## Acceptance criteria

Given:

- observed receipts: `workspace_dotnet_new` for sln and app, no `workspace_pwsh_run_script`,
- product file: empty `.slnx`,
- required receipts: `template=sln`, `template=blazorwasm`, `workspace_pwsh_run_script`,
- required content: `src/Calculator/Calculator.csproj`,

completion evaluator returns both:

- `process.adapter.product_required_tool_receipt_missing`,
- `process.adapter.product_required_file_content_missing`.

The result must remain safe/idempotent.

## Regression tests

Add tests such as:

```text
CompletionGateEvaluator_reports_missing_required_script_receipt_and_failed_solution_readback
CompletionGateEvaluator_does_not_short_circuit_after_product_file_content_failure
CompletionGateEvaluator_keeps_existing_diagnostic_codes_for_compatibility
CompletionGateEvaluator_uses_stable_primary_issue_priority
CompletionGateEvaluator_marks_dotnet_solution_membership_failure_safe_and_idempotent
```

## Implementation warning

Do not relax `ValidateRequiredProductFileContentChecks`. It correctly caught the empty solution. The fix is aggregation and recovery behavior, not weaker validation.
