# SB39 - Provider-native browser coordinator migration to facets

## Status

Prepared.

## Objective

Replace host usage in provider-native browser coordinator while preserving copy/read diagnostics.

## Covered Inputs

- User request to continue gradual dispatcher isolation without Process Core.
- Current branch evidence from the completed projection coordinator split.
- Need to preserve all original behavior while preparing for future driver architecture only through documentation.

## Prerequisites

SB38

## Exact Source References

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/IProcessArtifactProjectionHost.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionHost.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionOrchestrator.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionContext.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- projection source coordinator files under `src/CanDoItAll.Modules.Processes/Automation/Dispatch/`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Implement or verify the scoped change for SB39.
- Update proof artifacts and execution report.
- Keep changes module-local under `CanDoItAll.Modules.Processes`.

## Dependency Impact

Downstream subbundles rely on this step preserving projection behavior and boundary direction. If this subbundle changes source-family behavior, all later proof is untrustworthy and must be reopened.

## Validation Depth

Focused validation appropriate to the touched source. Build/test/source scans required if production source changes.

## Implementation Steps

1. Re-read the objective and exact source references.
2. Make the smallest behavior-preserving change for this subbundle only.
3. Do not start downstream work until this subbundle closure gate is satisfied.
4. Update relevant proof files and execution report rows.
5. Run required focused tests/source scans.

## Scope Exceptions

No Process Core, no production driver API, no UI changes, no small/medium/mobile proof.

## Do Not Do

- Do not create `CanDoItAll.Processes.Core`.
- Do not introduce `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, `IProcessHelperDriver`, or driver packages.
- Do not change projection source-family order.
- Do not hide side effects in pure-looking helpers.
- Do not remove behavior or weaken tests.

## Acceptance Checklist

- [ ] Objective completed.
- [ ] Projection behavior preserved.
- [ ] No Core/driver/UI drift.
- [ ] Proof artifacts updated.
- [ ] Execution report row updated.
- [ ] Downstream dependencies checked.

## Proof Required

- Build transcript or explicit N/A with reason.
- Focused unit/integration tests when source moved.
- Source scan for no Process Core, no production driver API, no UI/prohibited viewport proof.
- Anti-stub scan.
- Source assertion proving exact objective.

## Browser Validation Logging

N/A expected. This is a runtime/service refactor. If any UI/Razor/CSS/JS/TS file changes, stop and reopen this subbundle.

## Progression Gate

Proceed only after local closure proof passes and no critical gate prerequisite is violated.

## Suggested Agent Prompt

Implement SB39 only. Preserve behavior. Update proof. Do not start the next subbundle until this subbundle is closed.
