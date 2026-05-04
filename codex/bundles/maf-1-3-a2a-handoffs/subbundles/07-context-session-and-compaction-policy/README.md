# Context Session And Compaction Policy

## Status

- `Completed`

## Objective

Audit and repair context, session, transcript, and compaction policies so governed process agents do not lose required upstream artifacts, handoff state, or tool evidence.

## Covered Inputs

- `NOTE-07`
- `REQ-10`

## Prerequisites

- Subbundle 01 package/API state is known.
- Current process artifact handoff rules from subbundle 05 are understood.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.Session.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\Chat\ChatSessionRuntimeCompatibilityAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Audit\WorkspaceExecutionAuditContext.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Grounding.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\MafAgentRuntimeTests.cs`

## Deliverables

- Explicit context policy for governed process automation, interactive chat, handoff workflows, and A2A long-running tasks.
- Safer compaction defaults or opt-out rules where current `8` turns / `12000` tokens can truncate important evidence.
- Tests or trace proof that upstream artifact summaries and required file paths survive runtime execution.
- Logging when compaction/session restore is skipped or applied.

## Dependency Impact

- Process-flow integration is not reliable if required artifacts or handoff context are truncated before QA/review.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Review `RestoreOrCreateSessionAsync`, transcript replay, and compaction attachment rules.
2. Check whether handoff/A2A sessions require special serialization compatibility keys.
3. Raise or make configurable compaction thresholds for roles/processes where needed.
4. Ensure governed process automation keeps required artifact and prompt contract context.
5. Add tests around process-safe compaction skip/apply behavior.

## Scope Exceptions

- Do not disable compaction globally for all interactive chat without evidence.
- Do not store unbounded transcripts if the provider/session model can handle state safely.

## Do Not Do

- Do not silently drop serialized session state on incompatibility.
- Do not rely on prompt memory when concrete artifact references are required.
- Do not hide context truncation from logs.

## Acceptance Checklist

- Governed process runs preserve required artifact context.
- Interactive compaction behavior is explicit and configurable.
- Handoff/A2A session compatibility is handled or rejected predictably.
- Logs explain compaction/session decisions.

## Proof Required

- Targeted Maf runtime tests for compaction/session policy.
- Process prompt or runtime test proving required artifact references are present after context handling.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter MafAgentRuntime --no-restore -m:1`

## Browser Validation Logging

- N/A.

## Progression Gate

- Architecture review gate 1 may approve process integration only after context/session policy is explicit and tested.

## Suggested Agent Prompt

```text
Implement subbundle 07 only: audit and repair context/session/compaction policy for governed process, A2A, and handoff runs. Prove required artifact context is not lost.
```
