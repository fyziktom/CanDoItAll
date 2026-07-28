# SB01 production source assertions

## Constructor and bypass assertions

The following read-only assertions ran from `C:\repositories\CanDoItAll` and passed:

```text
execution-service-constructions=1
workspace-service-constructions=3
orchestrator-constructions=3
preparation-constructions=8
context-registry-constructions=40
notification-hub-constructions=4
process-manager-orchestrator-hits=0
process-manager-direct-send-hits=1
terra-hits=0
```

The assertion script used `rg -n --glob '*.cs'` and failed the run unless counts matched. The Process Manager checks targeted `repo://src/Modules/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`. The Terra check targeted the SB01 integration test file.

## Persist/event/runtime assertion

Source inspection proves the current order:

- `SaveExecutionRunDetailAsync`
- `ExecutionUpdated?.Invoke`
- `executionEventSink.PublishAsync`

in `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs:55`.

The runtime call occurs later at `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:887`. The passing and failing-first integration transcripts exercise the same order against the real file store.

## Measurement graph boundary assertion

The baseline activates the direct DI `AgentFrameworkWorkspaceService` path and replaces its sink/runtime. It measures the shared Core send path, canonical persistence, provider-registry invocation, compatibility event, sink boundary, and runtime-entry boundary. It does not measure manual workspace-factory construction, MAF runtime/provider latency, or the current-profile relay.

`bundle://proof/SB01/transcripts/manual-factory-wiring-source-assertion.txt` proves the manually retained non-organization graph constructs the same `AgentFrameworkWorkspaceService`, which owns the sole `AgentFrameworkWorkspaceExecutionService` construction and event forwarder. That graph supplies a `NullAgentExecutionEventSink`; SB02 therefore must update both workspace-service construction paths and cannot use the legacy sink as its operational-feedback proof.

## Duplicate-read assertion

The first catalog/session reads occur in `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs:173`. The second catalog/session and blocking-summary reads occur in `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs:350`.

The three-iteration baseline independently observed:

- catalog loads `2` in every row;
- existing-session gets `2`;
- existing-session summary lists `1`;
- new-session gets and summary lists `0`;
- preparation warming changes none of those counts.

## Project Structure assertion

The page holds its current `ProjectStructureSurface`, but `ProjectStructureAgentRuntimeToolProvider.ProjectStructureReadAsync` calls `agentService.GetStructureAsync` for every tool invocation. The underlying project service calls the workbench structure query again. No invocation-snapshot read source exists in current production.

## Process Manager assertion

`ProcessWorkspaceShell.razor` has one direct manager-chat `workspaceService.SendMessageAsync` call and no `IAgentChatExecutionOrchestrator` reference. It also directly continues approvals. The shell projection and shallow manager prompt are already in memory, but the generic context registry path is bypassed.

## No-cost assertion

SB01 replaces `IAgentRuntime` with `StartupBarrierAgentRuntime`. Its provider-test, provider-chat, and model-maintenance methods throw `NotSupportedException`; the final five-case run passed, so none was invoked. The integration file contains no Terra reference. The unit preparation tests use deterministic providers. No paid/model call occurred.
