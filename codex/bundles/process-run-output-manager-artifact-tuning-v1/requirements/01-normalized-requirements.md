# Normalized Requirements

## R1 External Output Grounding

- When a process run is launched from a project-structure node, dispatcher grounding must include relevant external output/product paths from project-level planning branches even when those branches are not descendants of the selected delivery node.
- If a grounded external output path exists, execution prompts must tell agents to deliver or finalize the product in that external target before claiming completion.
- The implementation must remain generic and must not depend on the Tetris project id, run id, app name, or exact path.

## R2 Manager Chat Resolution

- The Processes page Manager tab must resolve a technical manager agent for a selected run using the same precedence as live process manager chat: configured manager id/name, selected run assignments, then a unique scored fallback.
- Selecting a run with a generic `Default process manager` label must not block chat when the run has a unique assigned delivery/process manager.
- Ambiguous fallbacks must remain unresolved instead of silently choosing an arbitrary manager.

## R3 Run Folder Projection

- Project structure process run projection must create folder nodes for run-level workspace folders, not one node per artifact file directory.
- Paths under `output/.../process-runs/{runId}/{productFolder}/...` must collapse to the top-level product folder for that run.
- Paths under `artifacts/.../process-runs/{runId}/...` must collapse to the run artifact folder.
- Paths under date-based process-runs receipt folders that do not include the current run id must not create a specific run folder node.

## R4 Validation

- Add or update automated tests for each requirement.
- Run the prepared-stage bundle validator before implementation and the completed-stage validator before closure.
- Restart the web app on `http://localhost:5032` after successful build/test validation so the user can test.
