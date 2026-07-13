# Recovery Taxonomy And Upstream Repair Router

## Status

- `Completed`

## Objective

Replace unsafe retry heuristics with a typed recovery taxonomy and router that sends missing inputs to upstream producers or manager action, sends missing/denied tools to manager access or reassignment, and allows same-step retry only for proven current-step transient/idempotent failures.

## Covered Inputs

- R08, R09, R10, R11, R14
- US04, US05, US09, US10
- EX01, EX02, EX04, EX05, EX06, EX07, EX08, EX12, EX16
- Architect notes that retry is useless when artifacts or other inputs are missing and should return to the previous step with manager help.

## Prerequisites

- SB04 progression gate passed.
- SB02 lineage and SB04 finalization facts are durable and testable.
- Current automatic retry behavior is characterized by SB01.

## Exact Source References

- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Rework.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessManagerPorts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessManagerControlLoop.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Recovery.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Subprocess.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.CompletionIssueResults.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionRetryPolicy.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs`

## Deliverables

- Typed recovery failure categories, ownership, retry eligibility, and route actions.
- Runtime router that chooses current-step retry, upstream step rework, manager access grant/reassignment, child blocker propagation, template/plan invalid block, or terminal block.
- Removal or neutralization of automatic retry conversion from manager-required adapter results.
- Manager-visible diagnostics with actionable state and masked sensitive data.
- Loop budget and repeated-repair handling.
- Tests for missing input, missing produced artifact, denied capability, missing receipt, transient provider failure, child blocker, unknown failure, and upstream producer selection.

## Dependency Impact

- SB06 must isolate driver-specific classification policy after this router is defined.
- SB08 closure depends on this subbundle proving the main hardening goal.

## Validation Depth

- `Process-critical closure`

## Implementation Steps

1. Define typed recovery categories and route actions from finalization and adapter facts.
2. Extract recovery decision logic out of broad runtime engine partial behavior into a cohesive runtime-owned service.
3. Change same-step retry eligibility to require current-step ownership, idempotency, retry budget, and no missing upstream/access prerequisites.
4. Route missing connected input to the producer selected from artifact lineage or to manager unresolved-lineage action.
5. Route denied/missing capability to manager grant, reassignment, or terminal policy block.
6. Preserve transient provider/runtime retry only when safe.
7. Update dispatch/manager orchestration to consume route decisions.
8. Add failing-first and passing tests; update proof manifest and execution report.

## Scope Exceptions

- Does not redesign manager UI unless required to expose new recovery route diagnostics.
- Does not implement domain-specific repair actions; drivers/templates may add detail in SB06.
- Does not replace all existing retry tests at once; it must update tests affected by taxonomy changes.

## Do Not Do

- Do not convert `NeedsManager` to `Ready` just because diagnostics are marked safe by an adapter.
- Do not retry a consumer step when a required upstream artifact is missing.
- Do not retry missing/denied capability without a manager grant or reassignment.
- Do not treat unknown failures as retryable fallback.

## Acceptance Checklist

- Missing connected input routes to upstream producer or manager unresolved-lineage action.
- Denied tool routes to manager or terminal policy, not retry.
- Missing required receipt caused by current-step omission can retry only when tool is available and operation is idempotent.
- Transient provider timeout can retry only within budget and with idempotency proof.
- Unknown failure becomes manager-required diagnostic.
- Repeated repair loops stop predictably.

## Proof Required

- `bundle://proof/SB05/manifest.md` with changed-file hashes, commands, and recovery route examples.
- `bundle://proof/SB05/semantic-invariants.md` describing retry and routing invariants.
- Failing-first test proving missing upstream artifact currently retries or blocks incorrectly.
- Passing test proving missing upstream artifact selects upstream producer/manager and does not retry consumer.
- Passing tests for denied tool, missing tool, missing receipt, transient provider, unknown failure, and loop budget.
- Source assertion that old automatic retry conversion is removed or bypassed by typed taxonomy.

## Browser Validation Logging

- Route: `N/A unless recovery diagnostics are surfaced in UI`
- Viewports: if UI touched, large desktop plus affected responsive width
- Playwright evidence: recovery view shows route owner/category when UI changed
- Screenshots: record concrete paths if UI changed
- Review questions: can an operator distinguish same-step retry, upstream repair, manager access, and terminal block?

## Progression Gate

- SB06 and SB08 may proceed only when missing input/access/tool failures cannot silently same-step retry.
- Recovery route tests must prove the typed owner and action.

## C# Architecture Impact

This is a core runtime architecture change. Recovery logic must become independently unit-testable and must not depend on Module or AgentFramework types.

## Boundary Ownership

Runtime owns generic taxonomy and routing. Application coordinates manager commands. Drivers can add domain-specific classification details through explicit contracts.

## Dependency Direction

Runtime remains inward. Module/driver adapter code translates external facts into generic recovery facts; Runtime does not call adapter code.

## Pattern Decision

Use a strategy/router service with typed result records. Do not use chained string diagnostics as primary control flow.

## Testability Contract

Router tests must cover every category and owner. Dispatch integration tests must prove routes affect scheduling/rework/manager decisions.

## Partial Class Policy

Move recovery classification out of `ProcessRuntimeEngine.ResultHelpers.cs` where feasible. Do not add a new partial file as the final architecture.

## Architecture Proof Required

- Recovery router unit tests.
- Dispatch/manager integration tests.
- Source assertion for no unsafe `NeedsManager` to `Ready` conversion.
- Dependency/source assertion for runtime neutrality.

## Suggested Agent Prompt

```text
Implement SB05 only. Replace unsafe retry heuristics with typed recovery taxonomy and upstream repair routing. Prove missing input/access/tool failures do not retry the wrong step. Keep runtime generic and unit-testable.
```
