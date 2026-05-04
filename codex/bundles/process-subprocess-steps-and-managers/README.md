# Process Subprocess Steps And Managers

This bundle is the execution package for adding subprocess-as-step support, manager reporting, manager override selection, canvas editing, and default software-development subprocess templates.

## Profile

- `initiative`

## Mission

Allow a process definition to use another process definition as a subprocess step while keeping process runtime state observable through one canonical parent-child run relation, avoiding per-step observer threads, and exposing manager-level status, blockers, and instructions for large process trees.

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

1. `subbundles/01-architecture-source-of-truth-and-schema`
2. `subbundles/02-runtime-subprocess-orchestration`
3. `subbundles/03-manager-control-plane-and-hr-override`
4. `subbundles/04-canvas-and-editor-ui`
5. `subbundles/05-default-software-development-subprocess-templates-and-agents`
6. `subbundles/06-validation-real-world-scenarios`
7. `subbundles/07-architecture-revalidation-and-closure`

## Dependency And Validation Map

- `plan/01-phase-plan.md` contains the dependency graph, critical subbundle list, and revalidation checkpoints.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed after targeted build, integration, browser, and bundle validation`
- Browser validation analytics: `Completed on http://localhost:5272/processes`
