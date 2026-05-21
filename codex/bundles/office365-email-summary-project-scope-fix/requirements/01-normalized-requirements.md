# Normalized Requirements

| ID | Requirement | Observable Success Criteria |
| --- | --- | --- |
| R1 | Workflow LLM execution must pass project scope into MAF context contributors. | Captured `AgentRuntimeExecutionOptions.ContextWorkspaceScope` is `Project` with the workflow `projectId`. |
| R2 | Missing project scope must still fail governed Cognitive Memory context. | Existing missing-scope unit coverage remains passing. |
| R3 | Empty Cognitive Memory context for a valid project must not fail a payload-only workflow. | Contributor returns `Skipped` with `reason=empty-context-pack`; actual recall exceptions still fail. |
| R4 | Office365 workflow must preserve downstream project-structure context. | Integration and live run preserve `runContext`, create an asset, and pass lease validation. |
| R5 | Summary asset must contain the client request facts. | Live asset mentions Tetris, static hosting/no backend, keyboard controls, and the one-week delivery request. |
| R6 | Summary asset must be created under the workflow node that started the workflow. | Live proof shows `assetParentId == workflowNodeId` and a workflow-to-asset link exists. |
