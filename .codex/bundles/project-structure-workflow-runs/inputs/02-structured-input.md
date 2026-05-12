# Structured Input

## Core Objective

- Users must be able to add, configure, start, monitor, and review workflow execution from the project-structure canvas.

## Success Criteria

- A user can add a workflow node under any eligible project-structure node.
- The add dialog lists workflows and previews the effective workflow input.
- The effective input always includes project details and parent-node details.
- A user can start the workflow from the workflow node context menu or inspector after explicit confirmation.
- Workflow start updates the workflow node to a started progress state.
- Workflow completion updates the workflow node to 100 percent progress.
- Workflow failure, cancellation, waiting, or pause-like states apply visible markers and status.
- Selecting a workflow node shows run state, current step, total steps, and summary details in the selection floating window.
- Workflow-created result nodes are parented under the workflow node.
- Each workflow run creates or updates a project-structure execution summary containing basic result text and created file paths.
- At least 20 real-world workflow cases run against PostgreSQL using realistic inputs, including required `gpt-5-mini` and local Ollama `gptoss20b64k` coverage.

## Hard Constraints

- Preserve every raw note in traceability.
- Backend foundations must be implemented and tested before UI layers.
- Do not use process-style matching resources dialogs for workflow starts.
- Do not silently fall back to a different model, provider, runtime backend, or parent node.
- Use typed ids/settings/status models rather than ad hoc string metadata.
- Use existing workflow runtime and project-structure services where possible.
- Use existing CanDoItAll component-library patterns for Blazor UI and query component MCP before adding structural UI markup.
- Result nodes created by a workflow default under the workflow node itself.

## Allowed Side Effects

- Add workflow-node metadata/contracts and migrations when needed.
- Add project-structure backend services/API endpoints for workflow nodes.
- Add workflow projection services for status, markers, summaries, and result node placement.
- Add Blazor dialog/state components and context-menu actions for project-structure workflows.
- Add workflow example definitions and test/scenario harnesses.
- Add tests and bundle proof artifacts.

## Source Artifacts

- `C:\repositories\CanDoItAll\.codex\bundles\project-structure-workflow-runs\inputs\00-original-request.md`
- `C:\programovani\testdata\testworkflows`
- `C:\repositories\CanDoItAll\.codex\bundles\workflow-executors-maf-tools`
- `C:\repositories\CanDoItAll\.codex\bundles\ai-workflows-maf-integration`

## Input Coverage Signals

- The "always provide project info" and "parent node with all details" language is absolute and must be tested.
- The "no matching resources dialog" instruction is explicit and must be tested against UI regression.
- The "workflow result nodes under yourself node" rule is a hard parentage invariant.
- The summary must include file paths even when no project-structure asset node was created.
- The 20-scenario provider/database validation is part of the feature, not optional QA polish.

## Dependency And Sequencing Signals

- Backend workflow-node contracts and input composition unlock every UI phase.
- Start coordinator/status projection unlocks selection-panel status and browser proof.
- Result-node parentage and execution summary unlock meaningful real-world scenario validation.
- Real-world scenario validation may reopen earlier subbundles if workflow outputs are shallow, incorrectly parented, missing paths, or not true work.

## Validation Expectations

- Prepared-stage bundle validation passes before implementation.
- Backend unit/integration tests prove create/start/input/status/summary/projection behavior.
- Component tests prove add and start dialogs.
- API tests prove workflow project-structure endpoints exist and mutate state predictably.
- Playwright proof covers add workflow, input preview, start confirmation, running status, completion/failure status, selection-panel details, and result summary display.
- PostgreSQL scenario proof covers 20 real cases, `gpt-5-mini`, local Ollama `gptoss20b64k`, supplied files, and synthetic realistic inputs.

## Evidence Contract

- Command results must be recorded in `reviews/01-execution-report.md`.
- Browser screenshots must be captured under `.codex/bundles/project-structure-workflow-runs/proof/browser/`.
- Scenario result JSON/Markdown must be captured under `.codex/bundles/project-structure-workflow-runs/proof/scenarios/`.
- Provider-specific raw validation notes must state expected behavior, actual output, and pass/fail decision.
- Any discovered product defect must become a repair subbundle and rerun proof.

## UI Validation Strategy

- Use Playwright with a large desktop viewport first, then at least one narrower viewport for dialogs/selection panel.
- Open-state proof is required for add workflow dialog, start confirmation dialog, right-click menu, and selection floating window.
- Screenshot review must answer: readable content, no clipping, no lateral overflow, correct overlay layering, clear selected workflow/input preview, and no process resource-matching stage.

## Browser Validation Analytics

- Log route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.
- Minimum route target: project-structure canvas for a seeded PostgreSQL project.
- Minimum actions: create/select parent node, add workflow node, inspect input preview, start workflow, select workflow node, inspect status, inspect execution summary/result child nodes.

## Working Assumptions

- The existing workflow runtime can run short in-app workflows, but project-structure node ownership/projection is missing or incomplete.
- Workflow step count can be derived from workflow graph nodes and persisted workflow events if no explicit runtime step-progress model exists.
- The Visual Studio PostgreSQL configuration is the intended shared validation database.

## Primary Risks

- Workflow run persistence has no project-structure linkage today, so summary/status projection may require a new bridge instead of simple UI work.
- Workflow examples may be too generic for Mouser/SEAMARK; scenario validation must verify real work and repair shallow workflows.
- Provider configuration may be environment-dependent; failures must be differentiated between environment setup and product behavior.
