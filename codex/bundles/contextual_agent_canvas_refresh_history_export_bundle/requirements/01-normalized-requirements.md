# Normalized Requirements

| ID | Requirement | Acceptance Signal |
| --- | --- | --- |
| R001 | The shared contextual agent window raises a refresh request after a successful contextual send or approval continuation in project-structure and process contexts. | Parent canvas hosts receive the event and reload their data without requiring a manual page refresh. |
| R002 | Project-structure canvas refresh captures current live canvas state before reloading data. | Pan, zoom, selection, and open CanvasLib floating windows remain in place after the reload. |
| R003 | Process definition canvas refresh captures current live canvas state before reloading process workspace data. | Definition canvas pan, zoom, selection, and open toolbox/selection/agent windows remain in place after the reload. |
| R004 | Each contextual agent row/card has a compact icon-style history action separate from the main row open/double-click action. | The row still supports select and double-click new-thread behavior, and the history button opens a dialog. |
| R005 | The history dialog shows the latest 25 threads for the selected agent, sorted by most recently updated first. | Dialog rows include title, date, message count, pending approval indicators, and preview text. |
| R006 | Double-clicking or keyboard-activating a history row opens the contextual chat floating window for that agent and thread. | The chat title/summary reflects the selected historical session and the user can continue composing in it. |
| R007 | The contextual chat floating window has a compact icon-style JSON export action. | The action is disabled until an agent/thread context exists and starts a JSON file download when available. |
| R008 | JSON export includes latest 25 thread sessions plus per-run detail with execution log, metrics, approvals, artifacts, checkpoints, and tool receipts. | Export payload can be inspected for all listed sections and enough metadata to debug tool calls. |
