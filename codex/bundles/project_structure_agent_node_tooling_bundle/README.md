# Project Structure Agent Node Tooling

This bundle is a coordination and execution package for `project_structure_agent_node_tooling_bundle`.

## Profile

- `initiative`

## Mission

- Project structure opens with a compact `PS - <project name>` browser title, and project-structure agents get enough typed-node catalog, selected-node context, and higher-level mutation tools to create correct work task nodes, reason about dependencies, and move selected node groups into a new subproject without leaving orphaned parentage.

## Outcome Contract

- Requested outcome: fix the page title, close the immediate work-task-node agent gap, add a higher-level selected-nodes-to-subproject operation, and produce an XLSX inventory of generic agent scenarios worth one-call tooling.
- Hard constraints: preserve project-structure leases and access checks, use `WorkItem` plus `objectSubtype = "task"` for work tasks, preserve dependency links when both endpoints move together, remove invalid cross-project links, and keep all moved nodes attached to the target project root or another moved parent.
- Evidence required before closure: prepared bundle validator, targeted .NET tests for service/API/tool exposure, component coverage for the page title/context prompt, and either a verified XLSX workbook or an explicit spreadsheet-runtime blocker.
- Known blockers or explicit scope exceptions: browser proof was skipped because test/build proof covers the title and contextual prompt; XLSX generation is blocked because the installed Spreadsheets skill cannot import `@oai/artifact-tool` in this session.

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

1. `subbundles/01-project-structure-page-title`
2. `subbundles/02-agent-node-catalog-and-context`
3. `subbundles/03-selected-node-subproject-tooling`
4. `subbundles/04-generic-agent-scenarios-workbook`
5. `subbundles/05-validation-and-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Prepared-stage validator passed`
- Execution status: `Implemented with XLSX artifact blocker`
- Subbundle gate review: `Code subbundles passed; workbook subbundle blocked by missing artifact runtime`
- Final closure gate: `Completed-stage validator passed with workbook subbundle blocked`
- Browser validation analytics: `Unit/component validation used instead of browser proof`
