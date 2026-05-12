# Project Structure Workflow Runs

## Profile

- `initiative`

## Mission

- Make workflows first-class executable nodes on the project-structure canvas: users can add a workflow node under an existing project node, configure the workflow input contract from the parent/project context, start the workflow from the node context menu with confirmation, observe status in the canvas and selection window, and receive workflow-created result nodes and an execution summary beneath the workflow node.

## Outcome Contract

- Requested outcome: project-structure workflow nodes can be created, started, monitored, and used as the parent for workflow result nodes without leaving the project-structure canvas.
- Hard constraints: use the existing workflow runtime and project-structure mutation APIs; keep workflow selection/input setup strongly typed; always include project and parent-node details in workflow input; do not reuse process resource-matching dialogs; do not hide workflow start or projection errors behind silent fallback behavior; use existing CanDoItAll component patterns and Radzen only where the project already uses it.
- Evidence required before closure: backend tests for workflow-node create/start/status/summary behavior, UI/component tests for add/start dialogs and selection status, Playwright proof of add workflow/start/status/result projection flows, PostgreSQL-backed real workflow scenario results for at least 20 distinct realistic cases, one `gpt-5-mini` provider run, one local Ollama `gptoss20b64k` run, and manual validation notes that workflow outputs performed true work on the supplied test data.
- Known blockers or explicit scope exceptions: production DurableTask/Azure hosting remains outside this bundle unless already available in the running app; this bundle may use the existing in-process workflow backend for short canvas-started runs when the persisted workflow settings allow it, but it must not pretend that in-process execution is the durable production host.

## Bundle Layout

- `inputs/` raw request, source artifacts, and structured input.
- `analysis/` current-state evidence, assumptions, risks, and reopen triggers.
- `requirements/` normalized requirements and input coverage matrix.
- `architecture/` target contracts, UI flow, status model, and projection strategy.
- `plan/` dependency-aware subbundle order and gates.
- `traceability/` raw-note and requirement-to-subbundle mapping.
- `shared-prompts/` reusable implementation and QA prompts.
- `subbundles/` numbered execution-ready workstreams.
- `reviews/` bundle self-review and live execution report.
- `inventories/` affected code/test/data inventory.
- `templates/` scenario matrix and subbundle template material.

## Recommended Execution Order

1. `subbundles/01-backend-project-structure-workflow-node-foundation`
2. `subbundles/02-workflow-add-dialog-and-input-contract`
3. `subbundles/03-workflow-start-coordinator-status-and-summaries`
4. `subbundles/04-project-structure-ui-actions-dialogs-and-selection-status`
5. `subbundles/05-workflow-result-node-projection-and-summary-artifacts`
6. `subbundles/06-real-world-workflow-catalog-and-scenario-harness`
7. `subbundles/07-postgresql-provider-browser-validation-and-closure`

## Dependency And Validation Map

- Keep `plan/01-phase-plan.md` current and rerun prepared-stage validation whenever implementation reality changes the scope.
- Treat subbundles 01, 03, and 05 as critical foundations. Weak proof there invalidates downstream UI and 20-scenario validation.
- If this bundle is resumed after compaction, use this README, the current subbundle README, and `reviews/01-execution-report.md` as durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed by script and manual readiness review`
- Execution status: `Completed`
- Subbundle gate review: `Subbundles 01-07 passed`
- Final closure gate: `Passed with explicit global-test residual`
- Browser validation analytics: `PostgreSQL add/start/status/result screenshots captured; result projection API/component proof passed; SQLite/PostgreSQL 20-scenario harness artifacts captured; gpt-5-mini and local Ollama gptoss20b64k provider workflow proof captured`
- Residual validation state: `Full-solution test timed out after 20 minutes, and the workflow-filtered solution run still fails in the unrelated Playwright process audit waiting for processes-launch-name-input. Targeted project-structure workflow proof passed.`
