# Re-run QA validation and runtime or browser proof after repair

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `qa-recheck`  
**Kind:** Review

## Purpose
Verify that the repair corrected the quality findings with runtime/API/browser validation as applicable and select an explicit disposition. Treat warnings, zero-test successful commands, entrypoint/runtime mismatches, and stale or unreferenced artifact evidence as unresolved quality defects unless explicitly accepted by the process.

## Inputs
- Original regression evidence and defect notes.
- Quality repair change set.
- Reviewed implementation package.

## Outputs
- Repaired warning-free validation, nonzero executed-test proof when tests are expected, shipped entrypoint/runtime consistency, stale or unreferenced artifact assessment, and runtime/API/browser regression evidence pack as applicable.
- Branch outcome: `quality-accepted` or `repair-escalation`.

## Dependencies
- `quality-repair`
- `qa-validation:repair-required`
- `implementation`

## Governance
Select `quality-accepted` only when the repaired deliverable has enough evidence for downstream security and release governance. Select `repair-escalation` when the repair still leaves release-blocking quality issues or proof gaps.
