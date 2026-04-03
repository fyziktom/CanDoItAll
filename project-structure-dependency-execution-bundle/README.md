# Project Structure Dependency Execution Bundle

This bundle is a coordination and execution package for `project-structure-dependency-execution-bundle`.

## Profile

- `initiative`

## Mission

- Add first-class project-structure dependencies across all node types, make them authorable and deletable from the canvas toolbar, expose dependency intelligence for downstream agent and Gantt consumers, add explicit duration-seconds support with sensible defaults, and prove the feature end-to-end with fresh-SQLite Playwright validation and screenshots.

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

1. `subbundles/01-phase-01-models-persistence-and-mcp-dependency-surfaces`
2. `subbundles/02-phase-02-canvas-toolbar-modes-and-dependency-authoring-ux`
3. `subbundles/03-phase-03-dependency-intelligence-and-mermaid-gantt-export`
4. `subbundles/04-phase-04-fresh-db-seeding-tests-and-browser-proof`

## Dependency And Validation Map

- The operational dependency map, critical-subbundle notes, and phase gates live in `plan/01-phase-plan.md`.
- Execution must update `reviews/01-execution-report.md` after each phase, including browser analytics rows and screenshot findings.
- Browser-visible work cannot close without Playwright MCP interaction on a fresh SQLite profile plus screenshot review notes.

## Validation Summary

- Bundle preparation status: `Passed after execution-sync rerun`
- Bundle readiness gate: `Passed with validate_bundle.py --stage prepared`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed with validate_bundle.py --stage completed`
- Browser validation analytics: `Passed on fresh SQLite proof profile`
