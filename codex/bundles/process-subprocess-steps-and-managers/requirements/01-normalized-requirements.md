# Normalized Requirements

| Id | Requirement | Acceptance |
| --- | --- | --- |
| R1 | Add a strongly typed subprocess step kind. | `ProcessStepKind.Subprocess` exists and is supported in editor, persistence, templates, canvas, and runtime reads. |
| R2 | Persist subprocess definition references. | A subprocess step stores a referenced process definition id plus a display snapshot for historic readability. |
| R3 | Persist process run hierarchy. | Child runs store parent run id, parent step run id, root run id, and hierarchy depth. |
| R4 | Start child runs idempotently. | Dispatching a subprocess step starts exactly one child run for that parent step and safely reuses it after retries. |
| R5 | Observe subprocess state through queries, not threads. | Parent run views and manager reports query child runs by parent relation and do not allocate per-child observer loops. |
| R6 | Synchronize parent step status from child outcome. | Completed child runs complete the parent subprocess step; blocked/failed/cancelled child runs surface as blocked or failed parent state. |
| R7 | Add process manager override. | A process version can store a manager agent override id and name snapshot. |
| R8 | Honor manager override in HR matching. | HR matching uses the override for manager role selection when present and logs the reason. |
| R9 | Provide manager reports and instructions. | Users can request a concise run-tree report and add an instruction that is journaled and visible to the manager flow. |
| R10 | Support subprocess canvas editing. | Right-click/canvas actions can create a subprocess step and change its target process. |
| R11 | Open subprocess definitions from the canvas. | Double-clicking a subprocess node opens the referenced process definition in a new browser tab. |
| R12 | Style subprocess nodes distinctly. | Subprocess nodes have their own icon, accent, family, and runtime status chips. |
| R13 | Add default .NET subprocess templates. | The template pack includes at least one reusable .NET implementation subprocess and a parent template reference. |
| R14 | Validate with real scenarios. | Tests prove subprocess alone, parent-with-subprocess, UI editing, and template import; execution report captures commands and browser evidence. |
