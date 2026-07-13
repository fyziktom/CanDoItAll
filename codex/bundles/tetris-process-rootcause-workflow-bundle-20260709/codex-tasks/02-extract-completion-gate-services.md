# Task 02: Extract completion gate services

## Goal

Make completion gate behavior unit-testable without invoking MAF or a full process run.

## New services

Suggested names:

- `IProcessCompletionGateEvaluator`
- `ProcessCompletionGateEvaluator`
- `IProcessReceiptRuleResolver`
- `ProcessReceiptRuleResolver`
- `IProcessToolReceiptEvaluator`
- `ProcessToolReceiptEvaluator`
- `IProductContentCheckEvaluator`
- `ProductContentCheckEvaluator`
- `IProcessCompletionIssueRouter`
- `ProcessCompletionIssueRouter`

## Constraints

- First extraction should preserve behavior.
- Keep adapter responsible for MAF execution and conversion to `StrategyResultEnvelope`.
- Move pure gate logic out of partial `AgentFrameworkProcessExecutionAdapter` files.
- Use interfaces only where they improve test isolation; avoid over-abstracting simple data transformations.

## Acceptance

- Existing tests pass after extraction.
- New service-level tests can run without MAF service provider, live process runtime, or real tool calls.
- No domain-specific step/tool names introduced in generic service code.
