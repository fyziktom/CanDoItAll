# Current State

- `MafWorkflowLlmComponentInvoker` used `IAgentRuntime.RunAsync` with suppressed approvals but no runtime execution options carrying workflow scope.
- `MafAgentRuntime` built context contribution policy from its runtime-wide `workspaceScope`, which is not the project-structure workflow node's project.
- `CognitiveMemoryAgentContextContributor` failed governed automation when no project scope was available. This behavior is correct and must remain.
- Live verification confirmed Office365 Graph could fetch the categorized message from the development database OAuth connection.
- Live verification also exposed the second valid-new-project condition: Cognitive Memory recall can return an empty context pack. That is not an outage and should not block a payload-only summarization workflow.

## Relevant Runtime Path

- Project-structure workflow start builds payload with `projectId`, `nodeId`, `project`, and `runContext`.
- Office365 download executor preserves `projectId`, `nodeId`, `project`, and `runContext.office365Processing`.
- LLM node invokes MAF.
- Project-structure executor creates the markdown asset and validates the original project-structure lease through preserved `runContext`.
