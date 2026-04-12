# Process workspace containment hardening

This bundle is the execution package for `cdi_process_workspace_containment_bundle`.

## Profile

- `feedback`

## Mission

- Repair the processes workspace and templates modal so the page fits the available window height, the process-definition list and tab panels scroll inside their own panes, the fullscreen templates dialog keeps both panes internally scrollable without nested body scrolling, and Mermaid preview zoom stays visually contained inside its preview surface.

## Bundle Layout

- `inputs/` raw request, screenshot note, and structured scope summary
- `analysis/` live repo state plus assumptions, risks, and reopen triggers
- `requirements/` normalized containment and proof requirements
- `architecture/` minimal layout strategy using existing BaseLib shells and tabs
- `plan/` dependency map, critical foundations, and phase gates
- `traceability/` raw-note coverage matrix
- `shared-prompts/` execution and QA prompts
- `subbundles/` numbered containment workstreams
- `reviews/` readiness review and execution report

## Recommended Execution Order

1. `subbundles/01-process-workspace-shell-and-tab-containment`
2. `subbundles/02-template-library-dialog-and-mermaid-viewport-containment`
3. `subbundles/03-browser-proof-and-bundle-closure`

## Dependency And Validation Map

- The authoritative dependency map, critical subbundle notes, and phase gates live in `plan/01-phase-plan.md`.
- This bundle uses the sandbox Chat page as the containment reference, but keeps implementation scoped to the existing processes module and BaseLib behavior already present in the repo.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Recorded`
