# SB06 Dispatcher Execution Client Migration Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs` now injects `IProcessAutomationExecutionClient executionClient` instead of `IAgentFrameworkWorkspaceService workspaceService`.
- Direct dispatcher calls to execution start, execution detail, execution-run listing, agent listing, provider listing, provider testing, agent editor load, and agent editor save now route through `executionClient`.
- The migrated call sites are in `ProcessRunAutomationDispatchService.Execution.cs`, `Concurrency.cs`, `Costing.cs`, `Dispatch.cs`, `Grounding.cs`, and `CompletionArtifactRecovery.cs`.
- `AgentChatRunFailedException` and `AgentRunFailedException` catch blocks are unchanged except for using `executionClient.GetExecutionRunDetailAsync` for failed-run inspection.
- Structured output, finalizer policy, retry, provider recovery, concurrency adoption, and artifact recovery control flow are unchanged by SB06.
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` now rejects `workspaceService.` and `IAgentFrameworkWorkspaceService workspaceService` in dispatcher partials and requires execution-client call sites.
- `bundle://proof/SB06/transcripts/dispatcher-direct-workspace-call-scan.txt` shows no remaining direct workspace-service calls in dispatcher partials.
- Browser validation is N/A because SB06 changed no rendered UI route and produced no screenshots.
