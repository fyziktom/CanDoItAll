---
name: candoitall-bundle-workflow
description: "Run the end-to-end CanDoItAll bundle workflow: decide whether a bundle must be prepared or repaired, validate it, execute it phase by phase, and close it with evidence. Use when the user wants one commandable workflow instead of manually switching between bundle preparation, validation, and execution."
---

# CanDoItAll Bundle Workflow

Use this as the coordinator skill. It keeps preparation, validation, execution, and closure aligned without duplicating every checklist from the underlying bundle skills.

This skill is the right entry point when the user says `prepare a bundle and execute it`, when a bundle already exists but may be stale, or when the task is too broad, risky, or UI-heavy for direct implementation.

## GPT-5.5 Operating Model

- Treat the skill as an outcome contract, not ceremonial process. The outcome is a bundle-backed implementation whose source inputs, code changes, proof, and status all agree.
- Prefer concise phase decisions and explicit success criteria over long process narration. Use detailed checklists only when they materially protect coverage, dependencies, UI truth, or final proof.
- Assume ChatGPT-5.5 starts at balanced `medium` reasoning. Do not add more process or request higher effort just because the work is large; deepen validation only when risk, ambiguity, or failed gates justify it.
- Let the model choose efficient tool order when the dependencies are independent, but never parallelize a step whose result determines the next action.
- After resume, compaction, or a long interruption, re-anchor on the bundle root, current subbundle, raw inputs owned, gate state, proof already captured, and open blockers before continuing.

## Outcome Contract

The workflow is complete only when all of these are true:

- raw inputs are preserved and mapped to requirements, owning subbundles, proof, and closure status
- the bundle has a usable dependency map, critical-foundation labels, and progression gates
- every executed subbundle has passed its entry and closure gates or is honestly blocked
- code changes, tests, browser or host proof, screenshots, and execution report rows support the same conclusion
- raw feedback has a note-by-note closure result of `Solved`, `Partially solved`, or `Not solved`
- unresolved gaps are represented as blockers, reopened work, or explicit follow-up subbundles, not hidden as residual-risk prose

## Workflow

1. Decide whether a usable bundle already exists.
2. If no bundle exists, use `candoitall-bundle-preparation`.
3. If the bundle exists but is stale, incomplete, or inconsistent with the repo, repair it before implementation.
4. Run the readiness gate with `candoitall-bundle-validator` and `scripts/validate_bundle.py --stage prepared`.
5. Review the subbundle dependency map, critical foundations, and phase gates before touching implementation code.
6. Execute one subbundle at a time with `candoitall-bundle-execution`.
7. Before each subbundle, run the entry gate with `candoitall-subbundle-validator`.
8. After each subbundle, record proof and run the closure gate with `candoitall-subbundle-validator`.
9. Reopen earlier work when later observations weaken a prerequisite or critical foundation.
10. After implementation, audit the original raw notes and source artifacts one by one.
11. Run the final closure gate with `candoitall-bundle-validator` and `scripts/validate_bundle.py --stage completed`.
12. Synchronize root status, subbundle status, execution report, analytics rows, proof paths, residual risks, and follow-up items.

## Decision Rule

- Raw notes, docx feedback, screenshots, mixed artifacts, broad initiatives, and architecture-heavy requests start in preparation.
- Existing validated bundles start in execution.
- Existing weak bundles start in repair, then readiness validation, then execution.
- If implementation reality forces a scope reduction, repair the bundle and rerun the prepared-stage validator before continuing.

## Gate Discipline

- Do not implement from raw notes when the work clearly needs decomposition.
- Do not let execution drift away from the documented bundle.
- Do not weaken words such as `all`, `every`, `each`, `same flow`, `must`, or `missing ability` unless the bundle lists the exception and follow-up path.
- Do not let a dependent subbundle start before prerequisite gates pass.
- Do not treat missing proof as a harmless residual risk when that proof is necessary to know whether the request works.
- If targeted tests become the bottleneck and the repo uses Microsoft Testing Platform, `mtp-hot-reload` may speed iteration, but final proof still needs a clean standard confirmation run.

## Proof Rules

- UI-heavy work requires real browser proof through Playwright MCP and the `playwright` skill unless an explicit blocker is documented.
- The first UI validation pass should use a maximized headed browser window or equivalent large-screen viewport, followed by narrower widths when layout is affected.
- Browser proof must include route or window, viewport, actions, assertions, screenshots, and pass or fail result in `reviews/01-execution-report.md`.
- Overlays, contextual help, dropdowns, menus, dialogs, and floating windows require open-state proof for readability, clipping, lateral overflow, and layering.
- Host-visible behavior such as PowerShell launch, UAC, file opening, or desktop integration requires host-level proof or an explicit validation gap.
- Use `screenshot` when browser capture cannot prove the desktop or window context.
- Use `imagegen` only as a planning aid when visual direction is unclear. Generated images never count as shipped proof.

## Resume And Compaction Rule

When the conversation resumes after compaction or a long-running interruption:

- reopen the current bundle files before continuing if the active state is uncertain
- restate the current subbundle, gate state, owned raw inputs, and next proof step in one concise working note
- do not trust memory over bundle files, validator output, or fresh repo observations
- continue from the latest proven gate instead of restarting or skipping ahead

## Feedback Closure Audit

After implementation, reopen the original raw notes, screenshots, and extracted docx text.

- produce note-by-note closure results: `Solved`, `Partially solved`, or `Not solved`
- map each result to code changes and proof
- cite browser analytics and subbundle gate rows when UI, host behavior, or prerequisite proof matters
- if any note is partial or unsolved, repair the bundle or create a concrete follow-up subbundle before exit

## Final Bundle Sync

Before the workflow exits:

- root `README.md` validation summary reflects readiness, execution, subbundle gates, final closure, and browser validation state
- completed subbundles no longer remain `Ready` or `In progress`
- `reviews/01-execution-report.md` contains shipped proof, final raw-note closure, browser-validation analytics, and subbundle gate results
- material bundle edits made during execution have passed the prepared-stage validator again

## References

- Read [references/workflow-decision-tree.md](references/workflow-decision-tree.md) when choosing between preparation, repair, and execution.
- Read [references/handoff-rules.md](references/handoff-rules.md) to keep the bundle structure and execution flow compatible.
- Use `candoitall-bundle-preparation` for raw inputs and bundle repair.
- Use `candoitall-bundle-execution` for implementation and proof updates.
- Use `candoitall-bundle-validator` for readiness and final closure gates.
- Use `candoitall-subbundle-validator` for per-phase entry and closure gates.
- Use `mtp-hot-reload` only as an iteration accelerator when the targeted test project already uses Microsoft Testing Platform.
- Use `playwright`, `screenshot`, `imagegen`, and `frontend-skill` as part of the UI validation loop when the bundle scope justifies them.

## Exit Condition

The workflow ends only when the bundle is ready, the implementation is complete, every executed subbundle has passed its gate or is honestly blocked, proof is recorded, analytics have been reviewed, raw feedback is closed note by note, final validators pass, and remaining risk is honestly documented.
