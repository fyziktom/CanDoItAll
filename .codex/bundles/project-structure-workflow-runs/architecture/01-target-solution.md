# Target Solution

## End State

- Project structure has a first-class workflow node concept that references a saved workflow definition/version and stores an input configuration.
- The add workflow dialog creates a workflow node beneath the selected parent and shows the effective input payload before creation.
- The start workflow dialog confirms execution from the workflow node and starts the workflow with a project-structure-specific start request.
- A backend coordinator composes input, starts the workflow run, links the run to the workflow node, updates progress/status/markers, and projects an execution summary/result children under that workflow node.
- The selection panel shows workflow run status details for selected workflow nodes without requiring navigation away from the canvas.

## Backend Boundaries

- `CanDoItAll.AgentFramework.Core` remains the workflow runtime contract layer.
- `CanDoItAll.Modules.AgentFramework` remains the workflow catalog/store/persistence implementation layer.
- `CanDoItAll.Modules.Workbench` owns project-structure workflow node contracts, node keys, input composition, status projection, and UI orchestration.
- `CanDoItAll.Web` exposes project-structure workflow endpoints and OpenAPI surfaces.
- Workflow executors may create project nodes/assets through existing project-structure services, but default parentage must resolve to the workflow node context.

## Proposed Contracts

- `ProjectStructureWorkflowNodeKeys`: strongly typed prefixes and parse/build helpers for workflow definition/run node keys.
- `ProjectStructureWorkflowInputSettings`: selected input modes such as include project, include parent, include subtree summary, include selected files, include folder path, and manual JSON.
- `ProjectStructureWorkflowInputPreview`: effective payload summary and raw JSON used by add/start dialogs.
- `ProjectStructureWorkflowNodeStartInput`: workflow definition/version override, requested backend, input settings, confirmation/requested-by fields, and lease token.
- `ProjectStructureWorkflowNodeStartResult`: workflow id/version, run id, state, route/detail link, warnings, summary node id when created.
- `ProjectStructureWorkflowExecutionSummary`: run status, step index/count, result summary, created node ids, created asset ids, created file paths, storage paths, and timestamps.

## UI Boundaries

- Follow the process add/start interaction model, but keep workflow start to confirmation only.
- Keep non-trivial logic in services/state models; Razor components should render and dispatch.
- Query the CanDoItAll components MCP before implementing new layout markup. Prefer `Stack`, `Grid`, `Row`, `Column`, `FormRow`, section/dialog primitives, and existing project-structure overlay components.
- Do not introduce Tailwind unless already used by the local component/page pattern being edited.

## Status Model

- `Pending/created`: workflow node exists but has no run.
- `Starting`: confirmation accepted and backend start is in progress.
- `Running`: workflow run state is running; node progress mode is `started` or `progress`.
- `WaitingForInput`: marker `pause`/warn and selection panel shows pending request count.
- `Completed`: node progress mode `complete`, progress percent `100`, marker cleared or success marker applied according to existing marker semantics.
- `Failed/Cancelled`: node status reflects state, progress remains last known percent, marker indicates failure/cancel/stop.

## Execution Summary Strategy

- Summary should be persisted in project structure as either typed metadata on the workflow node and/or a child `File`/`Note` summary node. The implementation must choose the smallest option that makes the summary visible in selection/details and queryable by API.
- File operations must surface paths even when no asset node was created.
- Result nodes created by workflow executors should use the workflow node as `parentNodeKey` unless the workflow explicitly supplies another parent and the summary records the exception.

## Scenario Strategy

- Extend workflow examples with purpose-built workflows for Mouser order reconciliation, Mouser order summary, SEAMARK folder summary, SEAMARK product comparison, SEAMARK price extraction, and financial plan review.
- Add synthetic cases for emails, business-plan notes, support tickets, meeting notes, vendor risk, release readiness, and file-save summaries until at least 20 distinct cases are covered.
- Persist scenario results under the bundle proof directory and store selected summaries in project structure during PostgreSQL validation.
