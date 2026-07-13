# Workflow Template Dialog Catalog

This bundle coordinates the Workflows page change requested on 2026-06-30: remove the template catalogue from the primary tab flow, expose it from the Workflows tab as a lazy-loaded dialog, add a read-only canvas preview dialog with an "Add to my drafts" action, and debrand SEAMARK-specific workflow examples into generic offer-analysis examples.

## Profile

- `initiative`

## Mission

- Make workflow templates discoverable without making the main Workflows page pay the template-loading or layout cost until the user asks for templates.
- Keep the UX large-screen-first: dialogue layouts should be dense, scannable, and aligned with existing CanDoItAll/BaseLib components.
- Preserve templates as generic examples for new users and remove company-specific SEAMARK wording from workflow templates and UI-facing tests.

## Outcome Contract

- Requested outcome: a Workflows-tab button opens a template catalogue dialog; the template pack loads only when that dialog opens; each catalogue item shows basic description and Preview; Preview opens a canvas-oriented dialog; "Add to my drafts" creates a user-owned draft copy with deterministic `01`, `02`, etc. prefixes when names collide.
- Hard constraints: use existing shared components where practical; do not keep the template catalogue as a primary tab; do not load the workflow template pack during page initialization, refresh, or unrelated tab changes; avoid exact company names and sensitive source details in generic example templates; UI proof is large-screen only.
- Evidence required before closure: component/unit tests for lazy loading, preview, draft naming, and debranding; a web build; large-screen Playwright screenshots for catalogue and preview dialogs; screenshot comparison notes against the generated design proposals; final bundle validation.
- Known blockers or explicit scope exceptions: small and medium viewport validation is intentionally out of scope because the app is large-screen-only for this request; Gmail plugin-specific examples may remain unavailable when the plugin is not installed.

## Bundle Layout

- `inputs/` raw request, generated design artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `inventories/` source and validation surfaces
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-sb01-design-proposals-and-current-state-grounding`
2. `subbundles/02-sb02-lazy-template-catalogue-dialog`
3. `subbundles/03-sb03-template-preview-canvas-and-draft-adoption`
4. `subbundles/04-sb04-generic-template-debranding-and-large-screen-proof`

## Dependency And Validation Map

- The operational dependency map, critical subbundle list, and phase gates live in `plan/01-phase-plan.md`.
- If resumed after compaction, use this README, the active subbundle README, `traceability/01-requirement-traceability.md`, and `reviews/01-execution-report.md` as durable state.

## Validation Summary

- Bundle preparation status: `Prepared validator passed`
- Execution status: `Completed`
- Subbundle gate review: `SB01 passed; SB02/SB03 component proof passed and final large-screen browser proof passed in SB04`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed at 1680x1000; small and medium viewport checks intentionally skipped by user request`
- Design proposal artifacts:
  - `bundle://evidence/design/template-catalogue-dialog-proposal.png`
  - `bundle://evidence/design/template-preview-dialog-proposal.png`
