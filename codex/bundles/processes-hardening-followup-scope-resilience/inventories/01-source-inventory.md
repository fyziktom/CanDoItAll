# Source Inventory

| Area | Current source | Notes |
| --- | --- | --- |
| Dispatch loop | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | Claims steps, detects subprocess/workflow/direct-agent path, applies transitions. |
| Agent execution | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs` | Runs AgentFramework, manages attempts/recovery, builds invocation metadata and prompt. |
| Prompt | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs` | Contains extensive scope, artifact, project-structure, browser, and implementation instructions. |
| Artifact projection | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` | Projects execution artifacts, workspace writes, response text, browser outputs, and decision artifacts. |
| Completion finalizer | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | New finalizer and artifact validation surface. |
| Artifact recovery | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs` | Manager recovery resolution and directive generation. |
| Runtime events | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs` | Adds `artifact-validation-diagnostic`. |
| Tests | `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | Contains many reflection-based tests, mostly source/prompt/runtime heuristics. |
