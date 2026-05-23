# Current State

## Runtime Path

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs` creates an agent for each workflow LLM component and calls `agentRuntime.RunAsync`.
- The invoker trims `response.ResponseText` and only then calls `ValidateJsonPayload` when `component.ModelSettings.RequireJsonOutput` is true or the result shape kind is `Json`.
- `CreateExecutionOptions` currently sets `StructuredOutput: null`, `FinalizerMode: Disabled`, `RequireStructuredOutputValidation: true`, and `MaxStructuredOutputRepairAttempts: 0`. That means the runtime asks for validation but supplies no structured response format for workflow LLM nodes.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs` can apply `ChatResponseFormat.ForJsonSchema(...)`, but only through an `AgentStructuredOutputContract`.

## Workflow Template Path

- `repo://Templates/Workflows/manifest.yaml` sets `component.modelSettings.requireJsonOutput: true` and includes a broad JSON schema for workflow outputs.
- The same manifest instructions tell the model to return one JSON object only, but prompt-only control is the weak link shown by the reported `+` parse failure.
- `repo://Templates/Workflows/workflows/default-workflows.yaml` defines `office365-category-email-summary-to-project`; node `summarize-office365` is an `LlmCall` between `download-office365` and `store-office365-summary`.
- The workflow requires preserving `projectId`, `nodeId`, and `runContext.office365Processing` so the final `office365.mark-message-processed` executor can move the categorized message.

## Existing Related Proof

- `repo://codex/bundles/office365-email-summary-project-scope-fix` previously covered project-scope propagation and asset creation, but it did not cover model-side JSON response-format enforcement.
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs` already has `MafWorkflowLlmComponentInvokerPassesProjectScopeFromWorkflowPayload`, which is the right place to add focused regression coverage without broad test churn.
