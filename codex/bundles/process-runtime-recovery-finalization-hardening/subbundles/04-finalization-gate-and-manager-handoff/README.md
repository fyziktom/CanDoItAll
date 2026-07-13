# Finalization Gate And Manager Handoff

## Status

- `Completed`

## Objective

Add a runtime-verifiable finalization gate and manager handoff state so a step cannot advance downstream until required inputs, outputs, receipts, branch rules, and configured manager confirmation are satisfied.

## Covered Inputs

- R06, R07, R08, R09, R10, R14
- US03, US04, US09, US10
- EX06, EX09, EX10, EX11, EX12, EX14, EX16
- Architect notes about finalization, manager confirmation before next step, lost deliverables, child subprocess blockers, and preventing incomplete artifacts from reaching later agents.

## Prerequisites

- SB02 progression gate passed for connected artifact lineage.
- SB03 progression gate passed for fresh step contract retrieval.
- Current manager-control and adapter completion behavior characterized by SB01.

## Exact Source References

- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeScheduler.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimePorts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.CompletionIssueResults.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifactEvidence.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs`

## Deliverables

- Typed finalization requirement and result model.
- Runtime gate that checks required connected input inspection/readback, required output production, required receipts, branch/finalizer rules, child subprocess state, and manager handoff rules.
- Explicit handoff state for manager confirmation when configured.
- Adapter/driver conversion that returns typed finalization facts instead of relying on prompt text alone.
- Persistence/projection updates for finalization and handoff receipts.
- Tests proving downstream steps do not become ready before finalization and required handoff.

## Dependency Impact

- SB05 recovery taxonomy consumes finalization failures.
- SB06 driver isolation must separate generic finalization from adapter-specific evidence policy.
- SB08 closure proof depends on finalization state being durable and observable.

## Validation Depth

- `Process-critical closure`

## Implementation Steps

1. Define generic finalization requirements and result categories.
2. Add runtime state/receipt support for finalization and handoff.
3. Evaluate finalization before marking a step completed or making produced artifacts available to consumers.
4. Require manager confirmation when the step contract or driver policy says handoff is required.
5. Convert existing adapter completion issues into typed finalization facts.
6. Update scheduler/readiness to respect pending handoff.
7. Add failing-first and passing tests for each critical finalization failure path.
8. Update proof manifest and execution report.

## Scope Exceptions

- Does not implement upstream repair routing; SB05 owns routing finalization failures to the responsible owner.
- Does not fully decompose AgentFramework adapter; SB06 owns broader extraction.
- Manager UI improvements are only in scope if needed to expose the new handoff state.

## Do Not Do

- Do not accept `Completed` status without required finalization facts.
- Do not treat manager-required finalization issues as automatic retry.
- Do not inline domain-specific software-development requirements into runtime finalization.
- Do not skip child subprocess blockers.

## Acceptance Checklist

- Missing required input inspection/readback blocks completion.
- Missing required produced artifact blocks completion.
- Missing required tool receipt blocks completion unless explicitly waived by typed policy.
- Required manager handoff blocks downstream readiness until confirmed.
- Child subprocess active or blocked state is handled explicitly.
- Unknown finalization issue becomes manager-required diagnostic.

## Proof Required

- `bundle://proof/SB04/manifest.md` with changed-file hashes, commands, and finalization/handoff receipts.
- `bundle://proof/SB04/semantic-invariants.md` describing completion and handoff invariants.
- Failing-first test where `Completed` without output evidence does not advance.
- Passing test where accepted finalization and manager confirmation allow downstream scheduling.
- Tests for missing receipt, ungrounded artifact, active child, blocked child, and unknown issue.
- Source assertions for runtime neutrality.

## Browser Validation Logging

- Route: `N/A unless manager handoff state is surfaced in UI`
- Viewports: if UI touched, large desktop plus affected responsive width
- Playwright evidence: manager view shows pending/confirmed handoff state if UI changed
- Screenshots: record concrete paths if UI changed
- Review questions: can an operator distinguish finalization failure from manager handoff wait?

## Progression Gate

- SB05 may proceed only when finalization failures are typed and durable enough for recovery routing.
- Downstream scheduling must be blocked by failed or pending finalization.

## C# Architecture Impact

Adds runtime state-machine behavior and possibly persistence/projection state. This must be isolated from adapter-specific evidence details.

## Boundary Ownership

Runtime owns generic gate evaluation and state transitions. Drivers/Modules provide adapter-specific evidence facts. Application coordinates persistence and manager commands.

## Dependency Direction

No Runtime references to Module, AgentFramework, MAF, UI, or domain-specific processes.

## Pattern Decision

Use typed state transition and receipt models. Do not use prompt-only finalization or booleans without failure category.

## Testability Contract

Finalization gate tests must run at Runtime level. Adapter evidence conversion tests must run separately at Module/driver level.

## Partial Class Policy

Do not expand current partial clusters as the final shape. If existing partials must be edited, extraction targets must be recorded for SB06.

## Architecture Proof Required

- State transition tests.
- Source assertions.
- Anti-stub audit proving downstream readiness comes from production finalization/handoff state.
- Updated dependency proof if project references change.

## Suggested Agent Prompt

```text
Implement SB04 only. Add typed finalization and manager handoff gates using SB02 lineage and SB03 contract retrieval. Prove incomplete steps cannot advance downstream. Do not implement full recovery routing beyond producing typed finalization facts for SB05.
```
