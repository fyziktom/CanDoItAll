# Task 01: Add incident regression fixture

## Goal

Reproduce the Tetris QA escalation without an LLM call. This creates a safety net before changing runtime behavior.

## Implementation notes

Add tests in `tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs` or a new focused test class if the existing file is too large.

Create synthetic assignments for a QA branch step with:

- branch outcomes: accepted and repair branch as data,
- product completion file content checks with acceptance branch enforcement,
- product completion receipt rules for validation and browser proof,
- optional capability scope receipt duplication to reproduce the current issue.

Use a temp product root with a fake scaffold file containing `@page "/counter"`.

## Required test cases

1. `QualityAccepted_with_full_browser_receipts_and_scaffold_content_routes_repair_branch`
   - Input: output status `Completed`, branch `quality-accepted`, full receipts present, file content check fails.
   - Expected after fix: `StrategyOutcome.Succeeded`, branch signal for repair branch, runtime gate finding evidence.
   - Initially this may be skipped/failing until branch issue router is implemented.

2. `RepairRequired_with_deterministic_content_defect_does_not_require_acceptance_browser_receipts`
   - Input: output status `Completed`, branch repair, no browser proof receipts, deterministic content defect available.
   - Expected after fix: `StrategyOutcome.Succeeded`, branch signal repair.

3. `RepairRequired_without_defect_evidence_and_without_browser_proof_is_not_accepted_as_repair_branch`
   - Input: output status `Completed`, branch repair, missing browser proof, no failed validation/content/browser defect evidence.
   - Expected: current-step retry or blocked completion issue, not repair route.

4. `BranchRoutableContentFailure_does_not_increment_safe_retry_budget`
   - Use runtime/recovery classifier test around result lineage.

## Acceptance

The tests should document the intended behavior even before all fixes are implemented. Do not remove coverage for legacy behavior where no repair route metadata exists.
