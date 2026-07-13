# Fresh Step Contract And Context Retrieval Tool

## Status

- `Completed`

## Objective

Give agents and finalizers a durable, scoped way to re-fetch the current step contract and connected input package after context compression or handoff, so completion is based on fresh runtime facts rather than stale prompt context.

## Covered Inputs

- R04, R05, R06, R11, R12, R14
- US01, US02, US03, US08
- EX06, EX09, EX10, EX13, EX16
- Architect notes about agents losing context, missing original deliverables, and needing a tool to access process step instructions and required artifacts during finalization.

## Prerequisites

- SB01 progression gate passed.
- SB02 contract shape is available or this subbundle coordinates with SB02 before exposing connected input artifacts.
- Required capability/tool receipt behavior is characterized.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepBriefBuilder.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeStepAssignments.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimePorts.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs`

## Deliverables

- Strongly typed durable step contract projection.
- Scoped retrieval service/tool for the current assignment and finalizer.
- Contract content includes instruction, expected outputs, required input artifacts, required receipts/tools, branch outcomes, finalization requirements, and handoff rule.
- Authorization/scope checks so a caller can only fetch the relevant run/step/assignment.
- Sensitivity-aware artifact metadata and retrieval handles.
- Tests for context retrieval, stale assignment rejection, missing contract diagnostics, and required artifact listing.

## Dependency Impact

- SB04 finalization depends on fresh contract retrieval.
- SB07 context packaging uses the same contract/package facts.
- SB06 driver isolation depends on knowing which parts are generic contract versus adapter-specific rendering.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Define the durable step contract shape from existing runtime assignment, plan, artifact, branch, and capability data.
2. Decide the owner project for each contract type using the boundary map.
3. Implement retrieval through a generic runtime/application service and expose it through the Module/driver tool surface.
4. Add authorization and stale assignment checks.
5. Update AgentFramework prompt/tool wiring so finalizers can request the contract during finishing.
6. Add tests for successful retrieval, missing assignment, stale assignment, forbidden access, sensitivity filtering, and required artifact listing.
7. Update proof manifest and execution report.

## Scope Exceptions

- Does not implement finalization gate enforcement; SB04 owns that.
- Does not implement full context budgeting; SB07 owns packaging policy.
- Does not redesign process UI unless host-visible tool/projection changes require it.

## Do Not Do

- Do not rely only on prompt text.
- Do not expose unbounded product files through the contract retrieval path.
- Do not expose sensitive artifact content when metadata or retrieval handles are enough.
- Do not add a service-locator-style tool that accepts arbitrary string commands.

## Acceptance Checklist

- An agent/finalizer can retrieve the current step contract after losing conversational context.
- The contract lists expected outputs and required artifacts from durable state.
- Unauthorized or stale access fails explicitly.
- Missing contract state produces a manager-visible diagnostic, not fallback prompt behavior.
- Tests prove the tool/service returns typed data, not ad hoc text blobs.

## Proof Required

- `bundle://proof/SB03/manifest.md` with changed-file hashes, commands, and tool/service evidence.
- `bundle://proof/SB03/semantic-invariants.md` describing retrieval and authorization invariants.
- Failing-first test for finalizer missing required contract details.
- Passing tests for current assignment retrieval and stale/unauthorized rejection.
- Source assertions for generic runtime neutrality and no magic string command routing.

## Browser Validation Logging

- Route: `N/A unless retrieval is visible in a process UI/tool host`
- Viewports: if UI touched, large desktop plus affected responsive width
- Playwright evidence: required only if UI/tool host visible behavior changed
- Screenshots: record concrete paths when applicable
- Review questions: can the visible tool state be inspected without leaking sensitive artifact content?

## Progression Gate

- SB04 may proceed only when finalizers can retrieve a fresh typed step contract.
- The contract must include required artifacts, expected outputs, receipt requirements, and handoff rules.

## C# Architecture Impact

Introduces a contract projection and tool/service surface that crosses runtime/application/module boundaries. The split must be explicit and testable.

## Boundary Ownership

Runtime/Core own generic contract facts. Application coordinates retrieval. Drivers/Modules expose the retrieval to specific agent frameworks.

## Dependency Direction

Runtime must not reference Module or AgentFramework. Module consumes contracts through Application/driver abstractions.

## Pattern Decision

Use explicit query/service contracts with typed request/response records. Do not use string commands or generic dictionaries.

## Testability Contract

Generic retrieval tests must run without AgentFramework. Module tests must prove adapter/tool wiring separately.

## Partial Class Policy

Do not add retrieval behavior directly into the AgentFramework adapter partial cluster. Extract a focused builder/service if current prompt builder needs to share logic.

## Architecture Proof Required

- Dependency/source assertion proof.
- Test transcript for retrieval and authorization.
- Anti-stub audit proving contract data came from runtime state, not test-only hand-written text.

## Suggested Agent Prompt

```text
Implement SB03 only. Add a durable, typed current-step contract retrieval path for agents and finalizers. Keep generic contracts separate from AgentFramework wiring. Prove context-loss recovery, authorization, and required artifact visibility.
```
