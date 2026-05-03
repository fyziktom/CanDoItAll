# Process Flow Integration

## Status

- `Completed`

## Completion Notes

- Added typed process cooperation metadata for cooperation mode, workspace tool profile, and cooperation summary.
- Process dispatch now resolves step role intent, selected agent A2A/handoff configuration, upstream artifact handoff state, and branch options before invoking AgentFramework.
- Core execution logs the process cooperation decision and exposes trusted process metadata through `WorkspaceExecutionAuditContext`.
- Maf runtime honors trusted process workspace-tool profile overrides for the current execution only, preserving configured external target and storage bounds.
- Live regression repair on `2026-05-03` tightened that contract so the same effective process profile now drives MAF tool attachment, not only the runtime plugin permission checks.
- The deterministic three-agent artifact handoff test now proves implementation and QA execution runs carry process cooperation metadata, runtime audit state, execution log evidence, and QA stat/read inspection of implementation artifacts.

## Objective

Wire A2A/handoff cooperation, tool profiles, context policy, and artifact gates into process launch, dispatch, and runtime progression.

## Covered Inputs

- `NOTE-03`
- `NOTE-04`
- `NOTE-05`
- `NOTE-06`
- `NOTE-10`
- `REQ-07`
- `REQ-08`
- `REQ-09`
- `REQ-13`

## Prerequisites

- Architecture review gate 1 returned `Proceed`.
- Subbundles 03, 04, 05, 06, and 07 are complete or have accepted limited scope.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Launch\ProcessesService.Launch.Planning.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Launch\ProcessesService.Launch.Staffing.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.RunStart.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeProgressionPlanner.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Execution.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ExecutionMetadata.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Tools.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessMockAgentRuntimeIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\MafAgentRuntimeTests.cs`

## Deliverables

- Process launch/staffing metadata that selects cooperation mode and tool profiles for step roles.
- Dispatch integration that passes handoff/A2A execution options to the agent runtime.
- Dispatch/runtime integration proof that trusted process workspace-tool profile metadata affects the actual tool surface presented to MAF agents.
- Progression logic that respects artifact gates and branch outcomes after handoff-enabled steps.
- Tests showing a software-delivery-like process produces implementation artifacts and QA consumes them.

## Dependency Impact

- Validation and final architecture review depend on this integration proving the user-visible process problem is addressed.

## Validation Depth

- Process-critical integration.

## Implementation Steps

1. Add process metadata for cooperation mode and tool profile selection.
2. Wire launch/staffing so developer, QA, architecture, business, and release roles select appropriate runtime options.
3. Pass cooperation options through dispatch into Core/Maf runtime.
4. Preserve existing branch/outcome/finalizer handling.
5. Extend deterministic process tests to exercise the integrated path.

## Scope Exceptions

- Do not require all process templates to use A2A/handoff immediately.
- Do not implement public remote agent marketplace discovery.

## Do Not Do

- Do not bypass existing process run status and branch outcome rules.
- Do not create hidden background agent calls that are not visible in execution logs.
- Do not allow process integration to skip required artifacts.

## Acceptance Checklist

- Process roles can select cooperation mode/tool profile. `Done`
- Handoff-enabled process execution logs identify cooperation decisions. `Done`
- Implementation artifacts are available to QA/review. `Done`
- Existing process mock flows still pass. `Done`
- Runtime capability composition applies process-selected software-development tools even when persisted agent settings are read-only. `Done`

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter ProcessMockAgentRuntimeIntegrationTests --no-restore -m:1`
- Integration proof that process metadata reaches runtime options.
- Runtime proof that process metadata reaches tool attachment for configured workspace tools and `workspace-plugin`.
- Execution log assertion for cooperation mode where applicable.

## Proof Captured

- `dotnet build src\CanDoItAll.Modules.Processes\CanDoItAll.Modules.Processes.csproj --no-restore -m:1`: passed with existing NU1902, NU1904, and nullable warnings.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests" --no-restore -m:1`: passed; 7 tests.
- `git diff --check`: passed with existing LF-to-CRLF warnings only.
- Live regression repair proof: `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~MafAgentRuntimeTests" --no-restore -m:1`: passed; 40 tests.
- Broader process-mock rerun after the repair did not pass because existing launch-plan fixture expectations selected seeded .NET agents instead of process-mock agents, and one failed test left a temp workspace locked. Treat this as a separate fixture/proof reliability issue, not proof against the MAF runtime repair.

## Browser Validation Logging

- N/A unless process launch/editor UI changes.

## Progression Gate

- Architecture review gate 2 may start only after process integration tests pass or a concrete blocker is recorded.

## Suggested Agent Prompt

```text
Implement subbundle 09 only: wire cooperation mode, handoff/A2A runtime options, tool profiles, and artifact gates into process launch/dispatch/runtime. Preserve process status and branch semantics.
```
