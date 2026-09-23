# Read the selected project-structure node

Read the project structure of `ProjectId` with `project_structure_read` and locate the node identified by `ProjectNodeId` (the node the operator selected when the process was started). Treat `CurrentProcessRunId`, `CurrentProcessRunNodeId`, `ProcessRunNodeId`, `ParentProcessRunNodeId` and `TargetProcessRunNodeId` as typed launch context: `Current*` identifies this run, and `TargetProcessRunNodeId`, `ParentProcessRunNodeId` or `ProcessRunNodeId` identify the process-run node that will receive the durable note. Summarize the selected node (id, title, subtitle, status, object type and subtype, notes) and its direct children (id, title, object type) in the required brief artifact at `artifacts/process-runs/<current-process-run-id>/steps/read-selected-node.md`. Record the process-run node id you resolved from the launch variables, or state that none is present and that the writeback step must use `ProjectNodeId` instead. Do not call project-structure mutation tools in this step. Do not block only because the process-run node is projection-backed or is not returned by a direct persisted-node read; the writeback step verifies or creates durable receipts.

## Contract
- Inputs: Launch variables `ProjectId`, `ProjectNodeId`, `ProjectNodeTitle` and the process-run node context.
- Outputs: A brief naming the selected node, its direct children and the resolved writeback parent node id.
- Evidence: Node ids, titles, object types, child count and the `project_structure_read` receipt.
- Operation target scope: `ExternalProductTargetReadOnly`
