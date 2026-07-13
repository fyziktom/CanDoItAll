# SB04 - Diagnostic Rework Packets

## Status

- `Completed`
- Critical foundation: no

## Objective

Generate targeted rework instructions from aggregate diagnostics and resolved launch variables. For the incident, the packet must tell the retrying agent or executor exactly which helper script is missing, where the resolved script path is, which readback failed, and what not to rerun blindly.

## Covered Inputs

- GPTPro diagnostic-specific rework packet finding.
- REQ-004, REQ-007, REQ-017, REQ-020.
- Incident evidence for missing helper receipt and failed `.slnx` membership.

## Prerequisites

- SB01 resolved values complete.
- SB02 aggregate diagnostics complete.
- SB03 recovery classifier complete.

## Exact Source References

- `bundle://codex/04-diagnostic-specific-rework-packets.md`
- `bundle://evidence/product-readback-empty-solution.md`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.CompletionIssueResults.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`

## Deliverables

- `IProcessStepRecoveryInstructionBuilder` or equivalent service.
- Structured repair packet model containing diagnostic code, missing receipts, failed readbacks, resolved target paths, retry policy, do-not-repeat actions, and budget state.
- Incident-specific packet guidance for writing/running the resolved `DotNetCreateProjectScript` helper and verifying solution membership.
- Packet text safe for manager escalation after budget exhaustion.
- Tests proving no unresolved placeholders appear in repair packets.

## Dependency Impact

- SB12 uses this packet as manual/equivalent incident proof.
- SB06 can include child packet content in parent subprocess diagnostics after child propagation is implemented.
- SB11 runtime-owned executor may reuse packet facts for deterministic repair reporting.

## Validation Depth

- Unit tests for packet construction plus integration test around the incident diagnostic set.
- Semantic proof must show packets are specific enough to repair the missing helper receipt class.

## Implementation Steps

1. Define packet input from aggregate diagnostics, recovery classifier output, resolved launch variables, and run/step identity.
2. Create builders for required receipt misses, product readback failures, product path failures, managed artifact issues, and child subprocess issues.
3. For required `workspace_pwsh_run_script`, include required tool name, resolved script ref, expected side effect, and verification command/readback.
4. For failed `.slnx` membership, include the expected project path and actual readback summary.
5. Include do-not-repeat guidance: do not rerun scaffold if project already exists; run the wiring helper and verify membership.
6. Include retry budget/fingerprint context.
7. Ensure manager escalation packets after budget exhaustion include attempted repair evidence.
8. Add tests for missing receipt only, readback only, combined incident, unresolved placeholder rejection, and budget-exhausted text.
9. Keep UI wording neutral and factual.

## Do Not Do

- Do not emit generic "try again" instructions.
- Do not include unresolved placeholders in packet text.
- Do not tell the agent to delete/recreate existing project output unless a typed recovery policy explicitly allows it.
- Do not mask the original diagnostic code.

## Acceptance Checklist

- [x] Incident packet names missing `workspace_pwsh_run_script`.
- [x] Incident packet includes resolved create-project helper script ref.
- [x] Incident packet includes failed solution membership readback.
- [x] Packet says not to repeat scaffold when project already exists.
- [x] Budget-exhausted manager packet includes attempted repair plan.
- [x] Tests assert packet contents without brittle full-string snapshots.

## Proof Required

- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`
- Packet construction tests.
- Incident packet fixture.
- Source assertions that packet uses structured diagnostics and resolved variables.
- Anti-stub audit proving packet content comes from actual gate data.

## Browser Validation Logging

- `N/A`; no browser surface is changed.

## Progression Gate

- SB12 may close only after repair packets are actionable for the incident and budget-exhausted escalation.

## C# Architecture Impact

Adds a focused builder over structured diagnostics.

## Boundary Ownership

Packet construction belongs near process runtime/application orchestration, not in template markdown or UI projection code.

## Dependency Direction

Builder must consume typed diagnostics without referencing Workbench-specific implementation.

## Pattern Decision

Use PSR-005: builder over structured diagnostics.

## Testability Contract

Packet tests must assert facts and absence of unresolved placeholders.

## Partial Class Policy

Avoid adapter partial growth; adapter should call the builder if integration is needed there.

## Architecture Proof Required

- Explain packet model placement.
- Confirm builder does not parse free-form diagnostic messages.

## Suggested Agent Prompt

```text
Execute SB04 only. Build diagnostic-specific current-step rework packets from aggregate diagnostics and resolved launch variables. Prove the calculator incident packet names the missing helper receipt, resolved script, failed readback, and do-not-repeat scaffold guidance.
```
