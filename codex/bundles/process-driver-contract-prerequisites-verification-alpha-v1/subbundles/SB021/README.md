# SB021 — Gate G: .NET/Rust verifier readiness

## Status

Prepared.

## Objective

Prove the alpha lane only inspects existing transcripts and cannot execute dotnet, shell, package restore, file writes, or process transitions.

## Covered Inputs

- Raw user request to continue toward stable Process Core and domain drivers.
- Latest completed bundle proof from `process-core-evidence-descriptors-driver-contract-roadmap-v1`.

## Prerequisites

- Previous subbundle in phase completed.
- If this is a gate, all preceding phase subbundles must be closed.

## Exact Source References

- `/src/CanDoItAll.Processes.Core`
- `/src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- `/tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `/tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `/codex/bundles/process-core-evidence-descriptors-driver-contract-roadmap-v1`

## Deliverables

- Production/source changes only when explicitly required by this subbundle.
- Proof files under `proof/SB021/`.
- Updated execution report row.
- Updated architecture/analysis documents if this subbundle owns a decision.

## Dependency Impact

Critical foundation. Downstream phases become invalid if this subbundle changes permission semantics, Core dependency hygiene, route order, driver API availability, or proof assumptions.

## Validation Depth

- Build when source changes.
- Unit architecture tests when contracts, Core, or driver proposal docs change.
- Focused integration tests when module adapters or process behavior are touched.
- Source scans for Core forbidden dependencies, production driver APIs, UI/media drift, and stubs.

## Implementation Steps

1. Re-open the exact source references.
2. Make only the changes required by the objective.
3. Preserve existing process behavior.
4. Keep Core deterministic and dependency-clean.
5. Keep driver work non-production unless this subbundle is an explicit decision document.
6. Record proof before moving downstream.

## Scope Exceptions

- No production driver runtime.
- No broad Core runtime extraction.
- No UI proof unless UI files change unexpectedly.

## Do Not Do

- Do not add `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, driver DI registration, runtime selector, or manager command.
- Do not add shell execution, Graph/Office runtime calls, workspace writes, storage writes, or process mutation.
- Do not move EF, AgentFramework execution, finalizer application, claims, transitions, or storage into Core.
- Do not create small/medium/mobile proof.

## Acceptance Checklist

- [ ] Objective completed.
- [ ] Behavior preserved.
- [ ] No forbidden Core dependency.
- [ ] No production driver API.
- [ ] No UI/media drift.
- [ ] Proof recorded.
- [ ] Execution report row updated.

## Proof Required

- Source assertions.
- Build/test transcript when source changes.
- Negative proof when permissions, driver semantics, or Core boundaries are touched.
- Anti-stub scan.
- Gate manifest and semantic invariants for critical gates.

## Browser Validation Logging

N/A - backend/Core/service architecture only unless UI/media files unexpectedly change.

## Progression Gate

Do not proceed until the acceptance checklist is green and the execution report row is updated.

## Suggested Agent Prompt

Implement SB021 for phase P07 (.NET/Rust transcript verifier alpha preparation). Keep changes scoped to: Prove the alpha lane only inspects existing transcripts and cannot execute dotnet, shell, package restore, file writes, or process transitions.
