# Floating Agent Workspace Windows

This bundle coordinates contextual floating agent launchers and chat windows for project structure and process canvas work.

## Profile

- `initiative`

## Mission

- Add a reusable floating agent launcher that lists agents with explicit project-structure or process access, supports text and tag filtering, and opens a new persisted agent chat thread from the active project or process work surface.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input.
- `analysis/` current state, assumptions, and risks.
- `requirements/` normalized, testable requirements.
- `architecture/` target solution and important boundaries.
- `plan/` execution order and dependencies.
- `traceability/` requirement-to-bundle mapping.
- `shared-prompts/` reusable implementation and QA prompts.
- `subbundles/` numbered execution-ready workstreams.
- `reviews/` bundle self-review and execution report.
- `inventories/` affected source inventory.
- `templates/` reusable subbundle template.

## Recommended Execution Order

1. `subbundles/01-shared-contextual-agent-window-contract`
2. `subbundles/02-project-structure-integration`
3. `subbundles/03-process-workspace-integration`
4. `subbundles/04-validation-and-browser-proof`

## Dependency And Validation Map

- The shared AgentFramework component is the critical foundation because both host surfaces must use the same filtering, access display, and chat lifecycle.
- Project structure integration validates the project access path, ProjectStructure access metadata, and persisted chat creation.
- Process workspace integration validates process access metadata and the role/step canvas context.
- Browser proof must include open launcher windows, open chat windows, sent prompts, and a later Agents page thread check.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Prepared validator passed`
- Execution status: `Completed`
- Subbundle gate review: `All subbundle gates passed`
- Final closure gate: `Completed validator passed`
- Browser validation analytics: `Completed with Playwright MCP screenshots`
