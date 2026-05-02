# Contextual Agent Canvas Refresh History Export

This bundle coordinates implementation for `contextual_agent_canvas_refresh_history_export_bundle`.

## Profile

- `feedback`

## Mission

- Contextual agent windows on project-structure and process canvases refresh the underlying canvas after a contextual agent run, while preserving pan, zoom, selection, and open floating windows. Agent rows expose a compact thread-history action for reopening one of the latest 25 threads, and the agent chat window exposes a compact JSON export for recent thread history with runtime/tool evidence.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-canvas-refresh-callback`
2. `subbundles/02-thread-history-dialog`
3. `subbundles/03-thread-history-json-export`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared and validated`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Ready for completed validator`
- Browser validation analytics: `Captured on project-structure canvas route`

## Closure Notes

- Automatic contextual-agent refresh is wired through the shared floating-window component and both project-structure/process canvas hosts. Refresh captures current workbench UI state before existing reload paths run.
- Agent rows now expose a separate compact history icon. The dialog caps display at 25 newest threads and double-clicking a row opens the contextual chat floating window on that thread.
- Contextual chat now exposes a compact JSON export button. The export includes all saved threads for the selected agent, session messages, run records, run details, execution logs, metrics, approvals, artifacts, checkpoints, and tool receipts.
- Final validation commands and browser proof are recorded in `reviews/01-execution-report.md`.
