# Task 04: Apply branch-aware gates and deduplicate receipt diagnostics

## Goal

Receipt gates must evaluate only rules applicable to the selected branch outcome.

## Behavior

For visible UI software-delivery QA:

- `quality-accepted` requires validation receipts and browser/runtime acceptance proof receipts.
- `repair-required` requires concrete defect evidence. It does not require acceptance-only browser proof when the defect is already proven by validation/content/browser evidence.
- Missing proof because QA skipped its own required tools is not a product repair defect.

For `qa-recheck`:

- `quality-accepted` requires validation and browser proof.
- `repair-escalation` requires concrete unresolved defect evidence, not acceptance proof.

## Deduplication

If the same tool is required by product completion rules and capability scope, do not produce two separate missing receipt diagnostics for the same semantic requirement. Options:

1. Remove QA browser proof receipts from `CapabilityScope.RequiredReceipts` and keep them only in product completion rules.
2. Or deduplicate after both gates evaluate by normalized selector and purpose.

Prefer option 1 unless capability scope is needed to expose tools in prompt/tool access. Tool exposure should be separated from completion evidence requirements.

## Acceptance

- Final Tetris attempt equivalent (`repair-required` + deterministic scaffold defect + no browser receipts) returns `Succeeded` with repair branch signal.
- `quality-accepted` still fails or routes repair when acceptance proof is missing or product content fails.
- Diagnostics list skipped receipt rules with branch mismatch reason.
