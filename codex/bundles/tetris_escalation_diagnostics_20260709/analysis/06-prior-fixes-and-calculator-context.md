# Prior Fixes And Calculator Context

## Earlier Pro Root Cause

The previous ChatGPT Pro analysis identified the general class of escalation root causes:
- diagnostic-specific recovery packets were too generic;
- completion gates and artifact truth needed to be ledger-grounded;
- semantic gates needed safe/idempotent retry behavior;
- branch-specific process defects needed explicit rework packets rather than generic manager escalation.

Relevant copied files:
- `source-context/prior-pro-root-cause/analysis/02-root-causes.md`
- `source-context/prior-pro-root-cause/plan/01-phase-plan.md`
- `source-context/prior-pro-root-cause/codex/02-completion-gate-aggregator.md`
- `source-context/prior-pro-root-cause/codex/03-safe-auto-rework-recovery.md`
- `source-context/prior-pro-root-cause/codex/04-diagnostic-specific-rework-packets.md`
- `source-context/prior-pro-root-cause/subbundles/04-sb04-diagnostic-rework-packets/README.md`

## Updates Already Present In Current Source

The current source snapshots include the earlier repairs:
- ungrounded reference recovery guidance for peer-review path hygiene;
- QA product-content/readback guidance that chooses `repair-required` for `qa-validation` and `repair-escalation` for `qa-recheck`;
- missing receipt recovery guidance for QA/recheck;
- scaffold-content gates for visible UI apps;
- `quality-repair` scaffold-removal checks and prompt hardening.

## Calculator Contrast

The user reported that the calculator process run completed without trouble after the earlier repair. That is a useful contrast, but this diagnostic folder does not re-run or independently verify the calculator flow. It records the user-observed fact so Pro can compare why a simple calculator passed while Tetris still exposes QA/repair branch friction.

## Current Tetris Delta

Tetris is not failing because the app cannot build. It is failing because process governance is stuck between:
- product defect detection: scaffold content remains, so QA should select `repair-required`;
- runtime/browser proof contract: QA is still required to produce acceptance-style browser receipts even when not accepting the product.

This suggests the earlier fix helped, but it may not have completed the branch-aware distinction between acceptance gates and defect-routing gates.
