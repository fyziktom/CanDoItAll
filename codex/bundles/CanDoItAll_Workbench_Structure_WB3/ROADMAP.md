# Current refactoring roadmap

| Family | State after accepted entry / this assignment |
|---|---|
| Workspace, Projects, Agents/Providers/Workflow | Preserve previously completed bounded UI families and native owners; no wholesale redo |
| Workbench Calendar/Gantt/task editors | WB1 complete |
| Workbench Summary/Activity/Selection/support | WB2 complete with qualified test evidence and native SDK CSS limitation |
| Main Workbench Structure authoring | WB3: actual canvas, toolbox, generic composer, graph/clipboard and structural dialogs |
| Specialized Workbench integrations | Next: party/assignment, secret/media/file and runtime/Workflow/Process integration renderers, grouped by actual owner |
| Processes product UI | Last major module; separate catalog/definition/launch/live/recovery cuts |
| Final cross-module closure | Current caller/asset graph, representative native journeys and release-specific tests |

WB3 is a substantial multi-stage cut, not an assertion that all Workbench is done.
At exit name actual remaining routes/renderers, their effect owners and dependency
risk. Do not infer a missing screen from the existence of a backend API, or count
legacy unused Razor files as active shipped product surfaces without a caller.
