# SB02 - Completion Gate Aggregator

## Status

- `Completed`
- Critical foundation: yes

## Objective

Replace first-failure completion validation with an aggregate gate evaluator that preserves all completion issues, selects a deterministic primary diagnostic, and keeps original diagnostic metadata for recovery, rework packets, parent subprocess packets, and tests.

## Covered Inputs

- GPTPro completion gate short-circuit finding.
- REQ-003, REQ-004, REQ-010, REQ-017, REQ-018, REQ-020.
- Incident evidence where failed solution membership hid the more actionable missing `workspace_pwsh_run_script` receipt.

## Prerequisites

- SB01 complete for resolved path values.
- Current adapter source references refreshed.
- Existing product completion diagnostics understood and preserved.

## Exact Source References

- `bundle://codex/02-completion-gate-aggregator.md`
- `bundle://evidence/tool-receipts-summary.md`
- `bundle://evidence/product-readback-empty-solution.md`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.CompletionIssueResults.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`

## Deliverables

- `IProcessCompletionGateEvaluator` or equivalent cohesive evaluator.
- Gate-specific implementations for grounded evidence, managed artifacts, product mutation receipt, required tool receipt, product path, product readback, required product state, and completed-without-blocker checks where currently applicable.
- Aggregate result that contains every gate issue plus a deterministic primary issue.
- Priority policy that surfaces missing required helper receipt ahead of downstream readback failure for the incident.
- Adapter integration that preserves existing diagnostic codes and adds aggregate/container metadata only when useful.

## Dependency Impact

- SB03 classifies recovery based on aggregate diagnostics.
- SB04 builds repair instructions from all gate issues.
- SB05 uses gate results to decide artifact acceptance.
- SB06 uses aggregate child diagnostics in parent packets.
- SB12 validates the end-to-end incident behavior.

## Validation Depth

- Critical foundation with aggregate unit tests, adapter tests, and negative tests for hidden secondary diagnostics.
- Semantic proof must show that validation strength is preserved or increased.

## Implementation Steps

1. Map current validation calls in result conversion and product completion files.
2. Extract gate result records that preserve code, severity, retry safety, idempotency, source gate, path, expected content, receipt, and actionable context.
3. Wrap existing validation methods behind gate adapters first to minimize behavior drift.
4. Execute all safe gates and aggregate their results.
5. Add deterministic primary issue ordering; required missing tool receipts must outrank downstream product readback in the calculator incident.
6. Keep original diagnostic codes available for callers and logs.
7. Update adapter conversion so `Completed` finalizer output is accepted only when aggregate gates pass.
8. Add tests where both missing receipt and product readback fail.
9. Add tests where only one gate fails to prove existing diagnostics remain stable.
10. Add anti-regression tests that prove no gate was removed to make success easier.

## Do Not Do

- Do not weaken or remove required product path/content checks.
- Do not collapse all failures into only `process.adapter.completion_gates_unsatisfied`.
- Do not make the primary diagnostic order depend on dictionary iteration or message text.
- Do not expand the adapter partial cluster with unrelated responsibilities.

## Acceptance Checklist

- [x] Incident aggregate includes missing `workspace_pwsh_run_script`.
- [x] Incident aggregate includes failed `.slnx` membership readback.
- [x] Primary diagnostic is deterministic and actionable.
- [x] Original diagnostic metadata remains available.
- [x] Tests prove multiple gates can fail in one result.
- [x] Passing behavior still requires every required gate to pass.

## Proof Required

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- Failing-first aggregate diagnostic test.
- Passing adapter tests.
- Source assertions showing gate extraction and metadata preservation.
- Anti-stub audit showing gates are not bypassed.

## Browser Validation Logging

- `N/A`; no browser surface is changed.

## Progression Gate

- SB03, SB04, SB05, SB06, and SB12 may proceed only after aggregate gate results are stable and tested.

## C# Architecture Impact

Extracts completion-gate behavior from a large adapter path into cohesive gate/evaluator services.

## Boundary Ownership

Gate implementations stay near adapter/runtime integration unless their records are consumed across runtime, projections, or templates.

## Dependency Direction

Shared records must not pull module adapter dependencies into runtime or contracts.

## Pattern Decision

Use PSR-002: ordered gate strategy list with aggregate result.

## Testability Contract

Each gate must be unit-testable without invoking MAF or the full process engine.

## Partial Class Policy

Prefer service extraction. Adapter partial changes are limited to plumbing calls into the evaluator.

## Architecture Proof Required

- List extracted services and their owning project.
- Explain any new shared contract placement.

## Suggested Agent Prompt

```text
Execute SB02 only. Build aggregate completion gate evaluation while preserving existing diagnostics. Prove the incident emits both missing helper receipt and failed solution membership readback. Do not weaken any gate.
```
