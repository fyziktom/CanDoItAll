# Handoff Workflow Runtime

## Status

- `Completed`

## Objective

Add a MAF handoff workflow runtime path so configured local and remote agents can transfer work explicitly and preserve durable response/session state.

## Covered Inputs

- `NOTE-04`
- `REQ-06`

## Prerequisites

- Subbundle 01 build state is known.
- Subbundle 03 has defined A2A adapter boundaries if remote agents are included.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\AgentFrameworkWorkspaceExecutionService.Chat.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.AgentFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.Session.cs`
- `C:\repositories\agent-framework\dotnet\samples\03-workflows\Orchestration\Handoff\Program.cs`
- `C:\repositories\agent-framework\dotnet\samples\03-workflows\Orchestration\Handoff\AgentRegistry.cs`
- `C:\repositories\agent-framework\dotnet\tests\Microsoft.Agents.AI.Workflows.UnitTests\HandoffAgentExecutorTests.cs`
- `C:\repositories\agent-framework\dotnet\tests\Microsoft.Agents.AI.Workflows.UnitTests\Sample\12_HandOff_HostAsAgent.cs`

## Deliverables

- Typed handoff graph/settings model.
- Maf workflow builder adapter using `AgentWorkflowBuilder.CreateHandoffBuilderWith(...)`.
- Runtime option to run a handoff workflow as an `AIAgent`.
- Max-depth/correlation guards for handoff execution.
- Tests proving handoff transfer, return-to-previous setting, and no same-agent loop.

## Dependency Impact

- Process-flow integration depends on this being real workflow orchestration, not merely prompt guidance.

## Validation Depth

- Critical foundation.
- Unit/integration runtime proof with deterministic agents.

## Implementation Steps

1. Add typed handoff configuration in Models.
2. Add Core execution options/contracts needed to select handoff mode.
3. Build Maf adapter that creates a handoff workflow from configured agents.
4. Preserve runtime session serialization and continuation behavior.
5. Add loop/depth/cancellation guards and logging.
6. Add deterministic tests.

## Scope Exceptions

- Do not replace normal single-agent chat execution.
- Do not require every process to use handoff mode.

## Do Not Do

- Do not create handoff by appending "ask QA" instructions only.
- Do not allow recursive unbounded same-agent handoff.
- Do not bypass tool approval or finalizer policy.

## Acceptance Checklist

- Handoff configuration is typed.
- Workflow can transfer from intake/implementer to QA/reviewer.
- Return-to-previous can be enabled explicitly.
- Loop/depth limits are enforced.
- Runtime state is persisted or rejected explicitly when unsupported.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter AgentHandoffMetadataTests --no-restore -m:1`: passed; 3 tests.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter MafAgentRuntimeHandoffTests --no-restore -m:1`: passed; 3 tests.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter MafAgentRuntime --no-restore -m:1`: passed; 33 tests.
- `dotnet build src/CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj --no-restore -m:1`: passed.
- `dotnet build src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj -m:1`: passed with existing NU1902/NU1904 warnings.

## Browser Validation Logging

- N/A unless configuration UI is added.

## Progression Gate

- Artifact/process subbundles may depend on typed handoff options and deterministic local-agent transfer/return-to-previous behavior.

## Suggested Agent Prompt

```text
Implement subbundle 04 only: create typed handoff settings and MAF HandoffWorkflowBuilder runtime support with deterministic tests. Keep single-agent execution intact.
```
