# 03-large-screen-browser-proof

## Status

- `Completed`

## Objective

- Prove the compact header/stat migration with build output, large-screen screenshots, delayed tooltip checks, and raw-note closure.

## Covered Inputs

- N001-N008

## Prerequisites

- `01-shared-compact-header-primitives` completed.
- `02-page-and-tab-stat-migration` completed.
- App can run locally for browser validation.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\page-header-compact-stats\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\codex\bundles\page-header-compact-stats\inputs\00-original-request.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- `C:\repositories\CanDoItAll\CanDoItAll.slnx`

## Deliverables

- Passing build or clearly documented blocker.
- Browser screenshots for representative changed routes.
- Tooltip timing/open-state evidence.
- Updated execution report with commands, screenshots, browser analytics, subbundle gates, and raw-note closure.
- Completed-stage bundle validator pass or documented blocker.

## Dependency Impact

- This phase is final closure. Weak proof reopens earlier subbundles.

## Validation Depth

- End-to-end UI closure with browser proof and final bundle audit.

## Implementation Steps

1. Run build/test proof.
2. Start the app and capture large-screen screenshots for `/processes`, CRM-HR routes, and representative non-CRM pages.
3. Hover compact stats/actions and verify the 2-second tooltip delay/open state.
4. Inspect screenshots for height savings, no overflow, and no clipping.
5. Update execution report, raw-note closure, root status, and subbundle statuses.
6. Run `validate_bundle.py --stage completed`.

## Scope Exceptions

- Medium/mobile screenshots are not required for this request.
- Browser validation may use representative routes rather than every migrated route if build and inventory proof cover the rest.

## Do Not Do

- Do not declare closure if screenshots are missing for the visual request.
- Do not hide unresolved route failures as residual risk.
- Do not mark raw notes solved without mapping to code and proof.

## Acceptance Checklist

- Build proof recorded.
- Screenshot evidence recorded.
- Tooltip delay/open-state proof recorded.
- Raw notes N001-N008 are `Solved`, `Partially solved`, or `Not solved` with proof.
- Completed-stage validator passes or blocker is explicit.

## Proof Required

- Build command output.
- Browser screenshots under `codex\bundles\page-header-compact-stats\evidence\`.
- Tooltip screenshots or DOM visibility checks after 2-second wait.
- `validate_bundle.py --stage completed` output.

## Browser Validation Logging

- Routes: `/processes`, `/crm-hr`, `/crm-hr/directory`, `/crm-hr/crm`, `/automation`, `/validation`, and any visually risky migrated route.
- Viewport: 1600x900 or larger.
- Actions: navigate, wait for data, screenshot, hover compact stat/action, wait 2 seconds, screenshot tooltip.
- Screenshot review: header height, stat badge row height, no overlay/overflow, tooltip readable and unclipped.

## Progression Gate

- The bundle can close only when browser evidence, build proof, inventory proof, and raw-note closure all agree.

## Suggested Agent Prompt

```text
Implement subbundle 03 only after prior subbundles pass. Run build proof, start the app, capture large-screen screenshots and delayed tooltip evidence on representative routes, update execution report and raw-note closure, run completed-stage bundle validation, and reopen earlier phases if proof is weak.
```
