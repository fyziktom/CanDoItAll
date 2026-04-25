# Implementation Prompt

Implement the selected subbundle only.

Before editing:

- Read `README.md`, `plan/01-phase-plan.md`, `traceability/01-requirement-traceability.md`, and the selected subbundle README.
- Reopen `inputs/00-original-request.md` and preserve its literal scope.
- Treat `01-runtime-foundation-refactor-and-api-shaping` and `02-in-scene-toolbar-and-settings-chrome` as critical foundations.

While implementing:

- Keep the WebGlLib public runtime bridge stable or update all consumers and proof surfaces together.
- Do not leave the requested toolbar or context menu as ordinary host HTML controls.
- Do not widen scope into the production `ProcessWorkspace`.
- If reality forces a scope reduction, repair the bundle before proceeding.

Proof expectations:

- Use targeted .NET tests for the affected interop/session/state areas.
- Use Playwright MCP on `/webgl/process-workbench` with desktop screenshots first.
- Record browser analytics and gate results in `reviews/01-execution-report.md` while the proof is fresh.
