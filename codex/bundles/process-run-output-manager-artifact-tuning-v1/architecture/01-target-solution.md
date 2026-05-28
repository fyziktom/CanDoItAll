# Target Solution

## Ownership Boundaries

- Dispatcher grounding stays in `CanDoItAll.Modules.Processes.Automation.Dispatch`. It owns translating project structure into execution prompt constraints and external-target aliases.
- Manager chat resolution stays in `CanDoItAll.Modules.Processes.Runtime.Observation` and the Processes UI. Shared resolver logic should avoid diverging heuristics between the live dashboard and the process workspace tab.
- Project structure projection stays in `CanDoItAll.Modules.Workbench.ProjectStructure`. It owns graph nodes for process definitions, runs, and run output folders.

## Design

- Expand project-structure focus selection to include planning branches related to ancestors of the target branch. This lets an architecture/output branch inform a nested delivery task without scanning unrelated work items indiscriminately.
- Add explicit final-delivery prompt language for grounded external output roots so a temporary workspace build cannot be treated as the finished deliverable.
- Extract manager-agent resolution into a small internal helper used by `ProcessManagerChatService` and `ProcessWorkspace.ManagerChat`.
- Collapse process artifact paths to run roots with path-segment logic:
  - run artifact root: `.../process-runs/{runId}`
  - generated product root: `output/.../process-runs/{runId}/{firstChild}`
  - generic run-id folder: path through the `{runId}` segment
  - no run-id match under process-runs: ignored for run folder projection

## Tradeoffs

- The folder projection intentionally hides date-based tool receipt folders from the structure graph. Those files remain in process artifacts and runtime details; the graph should favor navigable run-level folders.
- The grounding expansion may include more planning context text, but only external path hints and concise node summaries are emitted, keeping prompts bounded.
