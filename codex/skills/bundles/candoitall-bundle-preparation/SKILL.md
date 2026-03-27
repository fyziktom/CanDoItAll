---
name: candoitall-bundle-preparation
description: Prepare structured, implementation-ready CanDoItAll bundles from raw requests, testing feedback, docx notes, screenshots, or mixed artifacts. Use when work is large, multi-phase, UI-heavy, cross-project, risky, or ambiguous enough that Codex should create a bundle with normalized requirements, phased subbundles, prompts, traceability, proof rules, and self-review before implementation starts.
---

# CanDoItAll Bundle Preparation

Prepare the bundle first. Do not implement feature code while preparing the bundle.

Turn messy inputs into a bundle that an implementation agent can execute without rediscovering the problem. Save the raw inputs, structure them, split the work into explicit subbundles, and validate that the bundle is complete before handing it off.

## Choose The Bundle Profile

- Use the `feedback` profile for testing notes, QA findings, screenshots, docx review docs, or a short list of concrete issues.
- Use the `initiative` profile for migrations, refactors, new features, architecture work, cross-repo consolidation, or anything that needs inventories and templates in addition to the baseline bundle structure.
- Start from `feedback` unless the task clearly needs architectural decomposition across multiple workstreams.

## Required Flow

1. Save the raw request and every source artifact under `inputs`.
2. If the source includes `.docx`, use `scripts/extract_docx_feedback.py` to extract text before summarizing it.
3. Normalize the task into explicit objectives, hard constraints, assumptions, risks, and validation expectations.
4. Build a feedback-closure matrix that keeps every raw note visible through execution.
5. Build the current-state analysis from the real repo, not from the user’s memory of the repo.
6. Create the bundle structure with `scripts/scaffold_bundle.py` or mirror that structure manually if the bundle already exists.
7. Split execution into numbered subbundles. Every subbundle must be independently actionable and should own a coherent slice of work.
8. Write reusable implementation and QA prompts under `shared-prompts`.
9. Pre-create browser-validation logging instructions for each subbundle and seed the execution report with a browser-validation analytics section.
10. Complete traceability so every requirement points to at least one concrete bundle file.
11. Run `scripts/validate_bundle.py` before declaring the bundle ready.
12. Finish the self-review from QA, architect, and manager perspectives. Do not mark the bundle ready while any of those three reviews is incomplete or inconclusive.

## Bundle Contract

Always create these root sections:

- `README.md`
- `inputs/`
- `analysis/`
- `requirements/`
- `architecture/`
- `plan/`
- `traceability/`
- `shared-prompts/`
- `subbundles/`
- `reviews/`

Create these additional sections when the task needs them:

- `inventories/`
- `templates/`
- `evidence/`

The `initiative` profile usually needs `inventories` and `templates`. The `feedback` profile usually does not.

## Subbundle Contract

Every subbundle README must include:

- `## Status`
- `## Objective`
- `## Covered Inputs` or `## Covered Notes`
- `## Exact Source References`
- `## Deliverables` or `## Scope`
- `## Implementation Steps`
- `## Scope Exceptions` when any raw note cannot be fully closed in the current phase
- `## Do Not Do`
- `## Acceptance Checklist`
- `## Proof Required`
- `## Browser Validation Logging`
- `## Suggested Agent Prompt`

Do not create vague “misc cleanup” or “remaining fixes” buckets. If the work cannot be named precisely, the bundle is not ready yet.

## Feedback Closure Matrix

Create a note-by-note closure table under `traceability/` or `requirements/` for feedback-profile bundles.

Every row must include:

- raw note id and exact wording
- normalized requirement ids
- impacted UI/data surface
- planned proof method
- owning subbundle
- exception status when the literal request cannot be implemented exactly as written

If the user says `all`, `every`, `each type`, `same flow`, or equivalent absolute language, do not collapse that into `supported` or `eligible` without explicitly enumerating the unsupported cases and why.

## Literal Language Rule

When raw feedback uses absolute or high-risk wording such as `all`, `every`, `each`, `must`, `missing ability`, `same flow`, `exactly`, or `twice`:

- preserve that wording in the normalized requirements or state the exact justified narrowing
- enumerate the affected inventory instead of hand-waving with `supported`
- call out system-managed, synced, relation-backed, upload-backed, or host-only cases explicitly if they may be exceptions
- do not leave exception discovery to the implementation phase when the current repo already exposes the gap

## UI And Host Proof Planning

When the feedback source includes screenshots, layout complaints, or desktop actions:

- plan Playwright MCP and the `playwright` skill as the default browser-proof path for UI work
- plan the first browser validation pass in a maximized headed browser window or an equivalent large-screen desktop viewport that fills the available work area
- plan a large-screen screenshot capture for real visual review, not just as an artifact to attach
- add the visual validation question set to the QA prompt and subbundle proof requirements
- plan how each UI subbundle will log browser-validation analytics: route, viewport, Playwright MCP actions, assertions, screenshot paths, and result
- if the change touches overlays such as tooltips, help affordances, dropdowns, menus, dialogs, or floating-window popovers, explicitly plan open-state proof for:
  - full readable content
  - no clipping by the viewport or parent container
  - no harmful lateral overflow
  - correct layering above neighboring chrome or floating windows
- after desktop validation is planned, add a narrower-width pass when the change affects layout or responsive behavior
- plan host-level proof for shell launch, file open, admin elevation, or other desktop integrations
- use the `screenshot` skill when fullscreen, active-window, or OS-level capture is needed beyond what Playwright can see
- if the visual target is unclear and a quick mock or alternative composition would reduce implementation guesswork, mention `imagegen` only as a planning aid and never as acceptance proof
- if the proof cannot reasonably be captured, mark that as an explicit open validation gap before implementation starts

When the likely implementation loop includes repeated test churn:

- detect whether the relevant test projects use Microsoft Testing Platform
- if they do, mention `mtp-hot-reload` in the implementation or QA prompts as an optional acceleration path
- still require a clean non-hot-reload confirmation run in the planned proof

## Quality Bar

- Use the smallest complete bundle that still removes ambiguity for the implementation agent.
- Keep source references exact. A future agent should be able to open the right files immediately.
- Make acceptance criteria observable. Prefer concrete browser, test, build, and artifact proof over subjective claims.
- Expand UI validation beyond “does it work.” Require readability, spacing, hierarchy, alignment, affordance, shared-component usage, and space-use checks.
- For UI work, require that screenshots are actually reviewed against explicit questions, not merely attached.
- For UI work, require that the bundle already says where the browser-validation analytics will be recorded and what counts as sufficient Playwright proof.
- Preserve the rule from the successful packs: the bundle is a coordination artifact first, not a place to sneak in implementation work.
- Do not silently weaken raw feedback scope during normalization. If you narrow scope, show the exception list and make the follow-up path explicit inside the bundle.

## References

- Read [references/bundle-profiles.md](references/bundle-profiles.md) before choosing the structure.
- Read [references/subbundle-contract.md](references/subbundle-contract.md) while splitting work.
- Read [references/bundle-validation-rubric.md](references/bundle-validation-rubric.md) before the final self-review.
- Use `scripts/scaffold_bundle.py` to create the initial folder skeleton.
- Use `scripts/extract_docx_feedback.py` when the source artifact is a `.docx`.
- Use `scripts/validate_bundle.py` before declaring the bundle implementation-ready.
- Use `playwright`, `screenshot`, `imagegen`, and `frontend-skill` when the planned proof needs them.

## Output Rule

The bundle is only ready when a different implementation agent could execute it phase by phase without guessing what to change, how to prove it, which raw notes are still open, or how to record completion.

## Validator Expectations

The validator is not just a folder-shape check.

- subbundle READMEs must include `## Status`
- subbundle `## Exact Source References` must contain absolute paths that already exist
- subbundle READMEs must include `## Browser Validation Logging`, using `N/A` only when the subbundle does not affect browser-visible or host-visible proof
- feedback-profile execution reports must already include `## Status`, `## Browser Validation Analytics`, `## Analytics Review`, and `## Raw Note Closure`
- if validation fails, repair the bundle before calling it ready

Keep these checks in mind while preparing the bundle so the first validation pass is not a surprise.
