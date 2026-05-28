# Structured Input

## Core Objective

- Fix generic process delivery plumbing for output grounding, selected-run manager chat, and artifact folder projection.

## Success Criteria

- Dispatcher grounding includes the architecture branch output folder for a nested delivery process launch.
- A selected run can resolve a technical manager from run assignments when `ManagerAgentName` is only `Default process manager`.
- Process run projection creates run-level folder nodes instead of one node per artifact subdirectory.

## Hard Constraints

- No hard-coded Tetris project id, path, run id, or app name in production code.
- Do not hide manager ambiguity by choosing an arbitrary agent.
- Keep process and agent templates generic.

## Allowed Side Effects

- Minimal C# changes in process dispatch, manager chat resolution, and workbench projection.
- Focused test updates for the repaired behaviors.

## Source Artifacts

- See `inputs/01-source-artifacts.md`.

## Input Coverage Signals

- Raw note 1 owns output destination and workspace-first finalization behavior.
- Raw note 2 owns Processes page manager chat.
- Raw note 3 owns project-structure run folder projection.

## Dependency And Sequencing Signals

- Output grounding should run first because future process executions depend on prompt context.
- Manager chat and projection are independent but both affect the user's post-run inspection workflow.

## Validation Expectations

- Targeted integration/unit tests for all three behaviors.
- Bundle validators at prepared and completed stages.
- Final app build and restart on port `5032` after code validation.

## Evidence Contract

- Test command transcript.
- Bundle validator transcript.
- Final process/browser smoke notes when the updated app is running.

## UI Validation Strategy

- Manager chat is UI-visible. Prefer a large-screen browser pass on `/projects/{projectId}/processes?processId={definitionId}&runId={runId}` if the app and test data are available after restart.

## Browser Validation Analytics

- Route: `/projects/7330105d-8450-4c80-923b-5c27d8e63d6c/processes?processId=672935c3-f687-4255-b8bf-90528248c642&runId=801f259d-8a52-41b8-a99f-cc96a2fc1947`
- Viewport: large desktop first; no mobile layout change is expected.
- Assertion: Manager tab no longer shows the "No bound technical manager agent" error for the selected run.

## Working Assumptions

- Date-based tool receipt folders are not the run workspace folder the user wants in project structure.
- Top-level generated product folders under a run output root are valid separate folders.

## Primary Risks

- Over-broad output grounding could include unrelated paths.
- Under-broad folder collapsing could hide a useful product folder.
