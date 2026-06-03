# SB06 - Process tool parity and policy regression suite

## Status

Not started.

## Objective

Repair and expand tests so the decoupling cannot silently drop process tools, weaken access checks, or change approval behavior.

## Covered Inputs

- User request to decouple MAF from Processes in small safe steps.
- `inputs/01-source-artifacts.md`
- `analysis/01-current-state.md`
- `inventories/01-process-tool-parity-inventory.md`
- `evidence/checklists/MAF_Processes_Decoupling_Checklists.xlsx`

## Prerequisites

- SB05 closure gate passed.

## Exact Source References

- `repo://tests/CanDoItAll.Tests.Unit/AgentRuntimeHardeningStaticRegressionTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs`
- `bundle://inventories/01-process-tool-parity-inventory.md`

## Deliverables

- Exact process tool parity tests.
- Read-vs-mutation approval tests.
- MAF-without-Processes test.
- MAF-with-Processes registration test.
- Static guard tests for hidden dependencies.
- Updated existing tests without weakening assertions.

## Dependency Impact

- This subbundle validates SB02-SB05. If it finds parity or policy drift, reopen SB04 or SB05.


## Validation Depth

- Critical foundation. Requires semantic adequacy proof, artifact-backed manifest, source assertions, anti-stub audit, and downstream smoke where named in the progression gate.


## Implementation Steps

1. Update static regression tests to assert new architecture.
2. Add explicit expected process tool name list in tests.
3. Assert read process tools are not `ApprovalRequiredAIFunction` by default.
4. Assert mutation process tools are wrapped by approval in interactive/non-suppressed mode.
5. Assert suppressApprovalRequirements preserves existing auto-approved process automation behavior.
6. Assert `ToolContractCatalog.KnownToolNames` still includes all process tools.
7. Assert `ToolCapabilityRegistry` still classifies all process tools.
8. Run targeted unit/integration tests.

## Scope Exceptions

- Full process-core split is intentionally out of scope.
- Full driver-pack architecture is intentionally out of scope.

## Do Not Do

- Do not change process dispatcher behavior.
- Do not start process core extraction.
- Do not introduce DotNet/SWDev/business process drivers.
- Do not remove or rename any process tool.

## Acceptance Checklist

- [ ] Every process tool from inventory is tested by exact name.
- [ ] Read tools approval-free behavior tested.
- [ ] Mutation tools approval-required behavior tested.
- [ ] MAF no-Processes behavior tested.
- [ ] Processes registered behavior tested.
- [ ] No existing test was weakened without replacement.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter AgentRuntimeToolProvider` transcript
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter AgentToolInvocationPolicy` transcript
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter AgentFrameworkExecutionCapabilityFiltering` transcript
- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`

## Browser Validation Logging

- No browser validation required unless runtime UI smoke reveals a rendered-regression risk. Record `N/A` in execution report if no browser route is exercised.


## Progression Gate

- Pass only when tests prove parity, policy, and architecture guardrails together.


## Suggested Agent Prompt

Use `shared-prompts/implementation-prompt.md`. Focus only on SB06. Do not start the next subbundle until the SB06 closure gate passes and proof artifacts are written.
