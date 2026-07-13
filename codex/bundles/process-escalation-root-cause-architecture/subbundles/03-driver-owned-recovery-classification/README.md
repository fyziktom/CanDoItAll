# Driver Owned Recovery Classification

## Status

- `Ready`

## Objective

- Route manager fallback and automatic recovery through typed failure categories and driver-owned recovery decisions instead of generic prompt heuristics.

## Success Criteria

- Every automatic recovery attempt records a failure category, source diagnostics, selected recovery policy, and reason.
- Generic runtime handles domain-neutral categories only.
- Domain drivers can contribute recovery playbooks without modifying dispatcher/runtime domain logic.

## Covered Inputs

- R05 Driver-Owned Recovery.
- R01 Runtime Diagnostics.
- R03 Capability Readiness Contract.
- User question whether manager fallback could recover missing artifact/tool/MCP cases.

## Prerequisites

- SB01 diagnostic records are available.
- SB02 readiness diagnostics are available.
- No recovery policy should be based only on unstructured prompt text.

## Exact Source References

- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Rework.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionState.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit`
- `repo://tests/Integration/CanDoItAll.Tests.Integration`

## Deliverables

- Domain-neutral failure category model.
- Recovery decision contract and driver strategy interface.
- Generic classifier for missing artifact, missing capability, denied capability, policy violation, timeout, provider failure, child-run blocked, instruction non-compliance, and unknown.
- Dispatcher integration that records recovery decisions before retry/rework.
- Tests proving no recovery happens when classification is unknown or unsafe.

## Dependency Impact

- SB04 depends on this to put .NET recovery in a .NET/software-delivery driver instead of generic runtime.
- SB05 depends on recovery categories to simplify templates and avoid prose-only fallback.
- SB06 depends on recovery records to assert E2E root causes.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Add characterization tests for current manager fallback where the root cause is not classified.
2. Define `ProcessFailureCategory` and `ProcessRecoveryDecision` as generic contracts.
3. Implement generic classification from SB01 diagnostics and SB02 readiness results.
4. Add a driver strategy interface for domain-specific recovery contributions.
5. Integrate classification into dispatcher retry/rework paths without adding domain switches.
6. Persist and project the recovery decision.
7. Add tests for safe retry, no retry, manager-required, and blocked terminal decisions.

## Scope Exceptions

- Do not implement .NET-specific recovery playbooks here beyond fake driver tests.
- Do not refactor templates in this subbundle.

## Do Not Do

- Do not add a switch on process definition key in generic dispatcher for .NET, Blazor, screenshot, or Playwright.
- Do not silently retry a step without a recorded failure category.
- Do not convert missing required proof into `Completed`.
- Do not parse long prompt text as the primary recovery contract.

## Acceptance Checklist

- Missing artifact is classified and recovery decision is explicit.
- Missing tool/MCP is classified and either preflight-blocked or manager-actioned with evidence.
- Provider timeout/failure is classified separately from instruction non-compliance.
- Child-run blocked is classified and propagated to parent with actionable diagnostics.
- Driver fake can supply domain-specific recovery without runtime knowing the domain.

## Proof Required

- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter Recovery`
- `dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter Process`
- Projection/API sample showing recovery classification and decision for a synthetic blocked step.
- Architecture scan proving no domain switches were added to generic dispatcher/runtime.

## Browser Validation Logging

- N/A: no browser-visible behavior is expected in this subbundle.

## Progression Gate

- SB04 may start only when manager fallback/recovery decisions are typed, persisted, projected, and driver-extensible.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Use SB01 diagnostics and SB02 readiness results to classify process failures before any automatic manager recovery. Add a generic recovery decision contract and driver strategy interface. Prove safe retry and no-retry cases with tests, keep generic runtime domain-neutral, and stop if recovery requires process-key string switches or prompt parsing.
```
