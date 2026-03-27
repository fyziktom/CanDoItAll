---
name: candoitall-bundle-execution
description: Execute an existing CanDoItAll bundle or subbundle phase by phase with code changes, proof, and bundle status updates. Use when a bundle already exists and Codex needs to implement it safely without skipping scope, validation, screenshots, tests, or follow-up documentation.
---

# CanDoItAll Bundle Execution

Treat the bundle as the contract. Implement one subbundle at a time and update the bundle with proof as the code changes land.

This skill is for delivery, not planning. If the bundle is missing, unclear, or unvalidated, switch back to bundle preparation before changing feature code.

## Required Flow

1. Read the root `README.md`, `plan`, `traceability`, and the selected subbundle README before editing code.
2. Reopen the raw feedback notes and verify that the subbundle scope still matches their literal meaning.
3. Confirm which requirements or notes the subbundle owns. Do not bleed scope from later phases into the current pass.
4. Audit the exact source references named by the bundle and the nearby tests they imply.
5. If implementation reality reveals that the bundle weakened or missed a raw note, repair the bundle before proceeding.
6. Implement the smallest correct change set for the current subbundle only.
7. Validate with the proof defined in the bundle:
   - component or unit tests
   - builds
   - Playwright or browser checks
   - screenshots when UI is involved
   - host-level validation when the feature launches local processes, opens files, or depends on elevation or OS behavior
8. Record browser-validation analytics for the subbundle while validation is happening.
9. Update the bundle after each completed subbundle:
   - root `README.md` validation summary when bundle-level state changes
   - subbundle status
   - execution report
   - proof artifacts
   - follow-up items when something cannot be closed in the current phase
   - note-by-note closure status for the raw feedback the subbundle owns
10. Move to the next subbundle only after the current one is proven.

## Execution Rules

- Do not silently widen scope because two subbundles touch the same file.
- Do not declare a subbundle done without the proof listed in its README.
- Do not rewrite the bundle casually while implementing. Only change the bundle when reality requires a better statement of scope, proof, or follow-up work.
- If a subbundle is blocked, create a concrete follow-up subbundle or item instead of hiding the gap in prose.
- Keep the bundle and the code in sync. A stale bundle is a process defect.
- Do not silently narrow scope when the raw note said `all`, `every`, `each type`, `same flow`, or equivalent language.
- If editing excludes synced nodes, relation-backed fields, upload-backed assets, or other discovered categories, record that as an explicit exception and create the follow-up path before calling the subbundle complete.
- Missing proof that is necessary to know whether the user request really works is an open gap, not a harmless residual risk.
- If targeted tests keep failing during active development and the project uses Microsoft Testing Platform, use `mtp-hot-reload` to shorten the edit-run loop, record it as iteration-only evidence, then finish with a clean standard test run.
- If a UI subbundle does not produce real Playwright MCP interaction plus screenshots or an explicit blocker, it is not done.

## UI Work Rule

For Blazor or other UI-heavy subbundles:

- use Playwright MCP and the `playwright` skill for browser truth
- open a real headed browser session on the target route and keep the proof tied to that route
- use `candoitall-watch-playwright-loop` when hot-reload and browser proof matter
- use `frontend-skill` for visual hierarchy, composition, and spacing critique
- start with a maximized headed browser window or a large-screen desktop viewport that fills the available work area
- capture a first-pass large-screen screenshot and actually inspect it before moving on
- use browser truth, not assumption, for layout and visibility fixes
- use route-specific Playwright actions such as click, evaluate, snapshot, or screenshot so the execution report shows what was actually validated
- capture screenshots for meaningful UI changes
- ask the validation questions from `references/ui-validation-questions.md`
- when the change adds or affects help affordances, tooltips, menus, dropdowns, dialogs, or other overlays, open them in the real browser and prove the open state itself:
  - the full content is readable
  - the overlay is not clipped by its own container or the viewport
  - the overlay does not overflow so far laterally that critical content disappears
  - the overlay is not hidden behind adjacent floating windows, inspectors, or page chrome
- if any answer from that visual review is not acceptable, keep tuning the layout before closing the subbundle
- after the large-screen pass is stable, continue to narrower widths on the same page context
- use `screenshot` when browser capture is insufficient or desktop/window context matters
- use `imagegen` only to explore visual alternatives when the direction is unclear; generated images never count as shipped proof
- if the feedback originated from screenshots or visual complaints, component tests do not replace browser proof
- if the feature triggers host behavior outside the browser, browser proof does not replace host-level validation
- append a `## Browser Validation Analytics` row for the subbundle with route, viewport, Playwright MCP evidence, screenshots, and result before marking the subbundle complete

## Bundle Update Rule

After finishing a subbundle, update at least:

- the root `README.md` validation summary when the bundle moves from prepared to implemented or from partial to complete
- the subbundle README status section if it has one
- `reviews/01-execution-report.md`
- `reviews/01-execution-report.md` browser-validation analytics row for the subbundle
- any proof artifact paths
- any remaining risks or follow-up subbundles
- the raw-note closure table for the notes owned by the subbundle

If you materially edit the bundle contract while executing, rerun `scripts/validate_bundle.py` before moving on.

## Raw Feedback Closure Rule

Before declaring the bundle complete:

- compare shipped behavior against the original raw notes, not only the normalized requirements
- mark each raw note as `Solved`, `Partially solved`, or `Not solved`
- attach the proof that justifies each status
- cite the browser-validation analytics row when the note depends on UI or host proof
- if a note is only partial, create the follow-up subbundle or blocker record immediately instead of burying it in a summary paragraph

## References

- Read [references/execution-loop.md](references/execution-loop.md) for the subbundle-by-subbundle delivery sequence.
- Read [references/proof-and-status-updates.md](references/proof-and-status-updates.md) before updating the bundle after execution.
- Read [references/ui-validation-questions.md](references/ui-validation-questions.md) for the UI inspection checklist.
- Use `mtp-hot-reload` for repeated local test iteration when the relevant test project already runs on Microsoft Testing Platform.
- Use `playwright`, `screenshot`, `imagegen`, and `frontend-skill` when the subbundle’s UI proof needs them.

## Exit Condition

The execution is only complete when the code, tests, browser evidence, browser-validation analytics, host evidence when applicable, raw-feedback closure status, and bundle status all agree.
