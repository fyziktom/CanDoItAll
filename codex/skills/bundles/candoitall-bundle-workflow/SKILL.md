---
name: candoitall-bundle-workflow
description: "Run the end-to-end CanDoItAll bundle workflow: decide whether a bundle must be prepared, prepare it when needed, validate it, then execute it phase by phase until completion. Use when the user wants one commandable workflow instead of manually switching between bundle preparation, validation, and execution."
---

# CanDoItAll Bundle Workflow

Use this as the coordinator skill. It keeps the preparation, validation, and execution halves aligned and prevents jumping into code before the bundle is ready.

This skill is the right entry point when the user says `prepare a bundle and execute it`, or when the task already smells too broad for direct implementation.

## Workflow

1. Decide whether a usable bundle already exists.
2. If not, switch into bundle preparation mode and create one.
3. Run the bundle readiness gate with `candoitall-bundle-validator` and `scripts/validate_bundle.py --stage prepared`.
4. Repair the bundle until the readiness gate passes.
5. Review the subbundle dependency map, critical foundations, and phase gates before touching implementation code.
6. Execute the bundle one subbundle at a time.
7. Before starting each subbundle, run the entry gate with `candoitall-subbundle-validator`.
8. Capture browser-validation analytics for every UI-relevant subbundle while execution is happening.
9. After each subbundle, run the closure gate with `candoitall-subbundle-validator` and decide whether downstream work may continue.
10. If later work exposes weak earlier proof, reopen the earlier subbundle instead of pretending the final audit is enough.
11. Run a raw-feedback closure audit against the original artifacts after execution.
12. Run the final closure gate with `candoitall-bundle-validator` and `scripts/validate_bundle.py --stage completed`.
13. Keep the bundle updated with proof, exceptions, reopened phases, residual risks, and final status synchronization.
14. Stop only when the requested scope is implemented and validated, or when a real blocker is documented.

## Decision Rule

- If the user provides raw notes, docx feedback, screenshots, or a broad initiative prompt, start with `candoitall-bundle-preparation`.
- If the user points at an existing bundle and asks to implement it, start with `candoitall-bundle-execution`.
- If the bundle exists but is stale, incomplete, missing dependency gates, or inconsistent with the repo, repair the bundle first, then execute it.

## Coordination Rules

- Do not start implementing from raw user notes when the work clearly needs decomposition.
- Do not keep a bundle frozen when execution reveals missing proof or incorrect assumptions.
- Do not let execution drift away from the documented bundle.
- Prefer one good bundle that is kept current over many partial bundles.
- Do not weaken raw feedback language such as `all`, `every`, `each`, `same flow`, `must`, or `missing ability` into a smaller supported subset unless the bundle explicitly lists the exception and the follow-up path.
- If implementation reality forces a scope reduction, route back through bundle repair before calling the work complete.
- If you materially repair the bundle during execution, rerun `scripts/validate_bundle.py --stage prepared` before continuing.
- If the execution loop becomes test-iteration-heavy and the repo uses Microsoft Testing Platform, allow `mtp-hot-reload` for faster local iteration, but never treat hot reload alone as final proof.
- If a UI subbundle closes without a real Playwright MCP session, browser assertions, screenshot review, and recorded analytics, treat that as a process defect and reopen the subbundle unless an explicit blocker was documented.
- If a dependent subbundle starts before the prerequisite progression gate has passed, treat that as a workflow defect and go back.

## Dependency And Gate Rule

Before executing any subbundle:

- read `plan/01-phase-plan.md`
- review the mermaid dependency map
- identify whether the current subbundle is a critical foundation
- confirm that every prerequisite listed in the subbundle README is actually complete and still trusted
- if a prior critical foundation has weak proof, reopen it before moving on

Critical foundations need deeper validation before the workflow can advance. That usually means tests plus real UI proof, or behavior proof plus one dependent-flow smoke, not only a local happy-path assertion.

## UI Rule

When the bundle or subbundle is UI-heavy:

- use Playwright MCP and the `playwright` skill for real browser proof, not just static reasoning
- treat real browser proof as a headed Playwright session that navigates the target route and performs route-specific checks such as click, evaluate, snapshot, or screenshot
- use `frontend-skill` when available for layout critique and stronger UI validation questions
- use `candoitall-watch-playwright-loop` for nearby-edit browser validation
- run the first browser validation pass in a maximized headed browser window, or resize the browser to fill the available large-screen desktop work area before judging layout
- capture large-screen browser screenshots on that first pass and actually inspect them
- record browser-validation analytics per subbundle in `reviews/01-execution-report.md`, including route, viewport, Playwright actions, assertions, screenshots, and pass or fail result
- record the subbundle gate decision in `## Subbundle Gate Results`, including whether the proof is strong enough for downstream work
- when overlays or contextual help are part of the change, require proof of the open overlay state rather than only the trigger state, and explicitly check clipping, lateral overflow, and z-order against neighboring chrome
- if Playwright capture is insufficient or desktop or window context matters, use `screenshot` for fullscreen, active-window, or desktop-level proof
- if the visual direction is unclear and a mock or composition variant would reduce guesswork, use `imagegen` only as a planning aid; generated images never replace browser proof
- keep screenshots and browser proof tied to the subbundle that changed the UI
- after the large-screen pass is stable, continue to narrower widths on the same page context
- if the source feedback includes screenshots, layout complaints, or `looks wrong` language, browser proof is required unless a blocker is documented
- if the source feedback involves desktop or host actions such as PowerShell, UAC, shell launch, or file opening, require host-level proof or document the exact missing validation as an open gap
- if no real Playwright MCP actions were performed for a UI subbundle, do not hide that behind `tested manually`; record the blocker and keep the subbundle open

## Required Visual Questions

For UI work, do not stop at `the test passed`. Review the resulting screenshots and answer at least these questions:

- Can I read all texts properly without zooming?
- Is anything overlapping, clipped, or visually colliding?
- Is anything too large, too small, or leaving awkward gaps?
- Are components aligned and justified consistently?
- Are we using the available space intentionally on the page?
- Do overlays, floating windows, menus, tooltips, and dialogs layer correctly?
- When an overlay is open, is all of its content still visible instead of clipped, shifted out of frame, or hidden behind neighboring surfaces?
- Does the screen still feel coherent with the app’s existing visual system?
- For visually led work, does the surface have a clear visual anchor and hierarchy?

If any answer is not acceptable, keep tuning the layout or interaction before closing the subbundle.

## Feedback Closure Audit

After the implementation pass, reopen the original raw notes, screenshots, and extracted docx text and verify them one by one.

- produce a note-by-note closure result: `Solved`, `Partially solved`, or `Not solved`
- map each closure result to code changes and proof, not just bundle prose
- if a note lands in `Partially solved` or `Not solved`, the workflow is not done yet; repair the bundle or create a concrete follow-up subbundle before exit
- do not hide missing proof inside `residual risk` if the missing proof is necessary to know whether the request really works
- if `mtp-hot-reload` was used during iteration, record it only as an acceleration aid and cite the clean confirmation run separately

## Browser Analytics Audit

Before the workflow exits:

- review the `## Browser Validation Analytics` rows in `reviews/01-execution-report.md`
- review the `## Subbundle Gate Results` rows in `reviews/01-execution-report.md`
- confirm each UI subbundle captured a real route, viewport, Playwright MCP actions, screenshot paths, and an explicit result
- confirm each executed subbundle records whether downstream dependencies were checked and whether the progression gate passed
- if the analytics show missing screenshots, no browser assertions, or no actual Playwright interaction, reopen the affected subbundle or repair the skill pack before calling the workflow complete
- if the analytics show overlays were only validated in the closed state, or the open-state proof missed clipping, lateral overflow, or layering checks, treat that as weak proof and repair the subbundle or skill pack before exit
- summarize the browser-validation quality and gate quality in `## Analytics Review` so the next workflow run inherits the lesson instead of repeating the gap

## Final Bundle Sync

Before the workflow exits, the bundle documentation must match reality:

- root `README.md` validation summary reflects the readiness gate, execution state, subbundle gate review, final closure gate, and browser validation state
- completed subbundles no longer remain `Ready` or `In progress`
- `reviews/01-execution-report.md` contains the shipped proof and final raw-note closure table
- `reviews/01-execution-report.md` contains the final browser-validation analytics table and subbundle gate results
- any material bundle edits made during execution have passed the validator again

## References

- Read [references/workflow-decision-tree.md](references/workflow-decision-tree.md) when choosing between preparation and execution.
- Read [references/handoff-rules.md](references/handoff-rules.md) to keep the bundle structure and execution flow compatible.
- Use `candoitall-bundle-validator` for readiness and final closure gates.
- Use `candoitall-subbundle-validator` for per-phase entry and closure gates.
- Use `mtp-hot-reload` when repeated failing-test iteration becomes the bottleneck and the targeted test project already uses Microsoft Testing Platform.
- Use the `playwright`, `screenshot`, `imagegen`, and `frontend-skill` skills as part of the UI validation loop when the bundle scope justifies them.

## Exit Condition

The workflow ends only when the bundle is ready, the implementation is complete, every executed subbundle has passed its gate or is honestly blocked, the proof is recorded, the browser-validation analytics have been reviewed, the root README and execution report are synchronized, the original feedback artifacts have been closed note by note, the final closure validator passes, and the remaining risk is honestly documented.
