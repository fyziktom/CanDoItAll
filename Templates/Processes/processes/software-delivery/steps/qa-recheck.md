# Re-run QA validation and browser proof after repair

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `qa-recheck`  
**Kind:** Review

## Purpose
Verify that the repair corrected the quality findings and select an explicit disposition.

## Inputs
- Original regression evidence and defect notes.
- Quality repair change set.
- Reviewed implementation package.

## Outputs
- Repaired regression evidence pack.
- Branch outcome: `quality-accepted` or `repair-escalation`.

## Dependencies
- `quality-repair`
- `qa-validation:repair-required`
- `implementation`

## Governance
Select `quality-accepted` only when the repaired deliverable has enough evidence for downstream security and release governance. Select `repair-escalation` when the repair still leaves release-blocking quality issues or proof gaps.
