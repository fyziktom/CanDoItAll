# Target Solution

## Smallest Correct Change

- Extend `AgentRuntimeExecutionOptions` with explicit JSON response-format settings that can carry a raw JSON schema from workflow component model settings.
- Update `MafAgentRuntime` run-option creation so it applies `ChatResponseFormat.Json` or `ChatResponseFormat.ForJsonSchema(...)` when those options are present.
- Update `MafWorkflowLlmComponentInvoker` so JSON-required workflow components populate those execution options before calling `agentRuntime.RunAsync`.
- Keep `ValidateJsonPayload` unchanged in spirit: it remains the final hard failure if the provider returns malformed JSON.

## Boundaries

- Runtime layer: `repo://src/CanDoItAll.AgentFramework.Maf/Runtime`.
- Shared execution options model: `repo://src/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs`.
- Tests: `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`.

## Non-Goals

- Do not implement JSON repair loops for workflow components.
- Do not change Office365 Graph category behavior.
- Do not change project-structure lease or storage semantics.
- Do not require UI changes.
