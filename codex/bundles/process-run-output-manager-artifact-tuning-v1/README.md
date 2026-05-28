# Process Run Output Manager Artifact Tuning v1

This bundle is a coordination and execution package for `process-run-output-manager-artifact-tuning-v1`.

## Profile

- `feedback`

## Mission

- Repair the generic process delivery path so a run launched from project structure respects external output folders defined anywhere in relevant project planning context, the Processes page manager tab can resolve and chat with the selected run manager, and project structure shows run-level workspace folders instead of one folder node per artifact subdirectory.

## Outcome Contract

- Requested outcome: new Blazor app delivery runs launched from the TetrisGame project structure ground `C:\programovani\dotnet-demo\output` as the requested product/output boundary, manager chat opens for a selected run, and process run projections expose only useful run folder nodes.
- Hard constraints: keep process and agent behavior generic; do not hard-code Tetris, Blazor-only paths, project ids, or run ids; preserve existing process templates, agents, and persisted project data.
- Evidence required before closure: targeted tests for grounding, manager resolution, and run-folder projection; bundle prepared/completed validator output; final build or focused test evidence; a live app restart on `http://localhost:5032` if implementation succeeds.
- Known blockers or explicit scope exceptions: this bundle fixes generic behavior and validates with representative fixtures; it does not rerun the full Blazor delivery process unless targeted proof indicates the fix is insufficient.

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

1. `subbundles/01-project-structure-output-grounding`
2. `subbundles/02-process-manager-chat-resolution`
3. `subbundles/03-run-folder-artifact-projection`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed for manager chat smoke`
