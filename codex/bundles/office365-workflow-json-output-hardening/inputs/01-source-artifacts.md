# Source Artifacts

| Artifact | Kind | Notes |
| --- | --- | --- |
| `inputs/00-original-request.md` | Raw user report | Contains the invalid JSON failure and stack trace. |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs` | Runtime source | Calls `agentRuntime.RunAsync`, trims `response.ResponseText`, and validates JSON after the model response is complete. |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | Runtime source | Creates MAF run options from `AgentRuntimeExecutionOptions`. |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs` | Runtime source | Applies `ChatResponseFormat` for typed structured output contracts only. |
| `repo://src/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs` | Shared model source | Defines `AgentRuntimeExecutionOptions`. |
| `repo://Templates/Workflows/manifest.yaml` | Workflow template source | Defines `requireJsonOutput: true` and `responseFormatJsonSchema`, but the workflow LLM runtime currently does not apply the schema as a provider response format. |
| `repo://Templates/Workflows/workflows/default-workflows.yaml` | Workflow template source | Defines `office365-category-email-summary-to-project` and node `summarize-office365`. |
| `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs` | Test source | Contains focused MAF workflow LLM component tests and test doubles. |
