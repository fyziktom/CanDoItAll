# SB04: Runtime-host operator readback closure

## Status
- Status: `Completed`

## Objective
Close the explicit runtime-host UI/readback gap recorded in the previous bundle.

## Covered Inputs
- `bundle://inputs/00-original-request.md`: identify what the refactor left incomplete and close the runtime-host readback gap.
- `bundle://requirements/01-normalized-requirements.md`: REQ-005.

## Prerequisites
- SB03 closure gate must pass.
- Representative process runs and step ids must be available for runtime-host readback tests.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs

## Scope
- Expose manager/runtime-host readback for selected process run/step through an operator-visible surface or a stable API route with a UI follow-up clearly eliminated or reclassified.
- Prefer adding a compact run-detail diagnostics panel if feasible.

## Dependency Impact
- SB05 depends on runtime-host readback remaining read-only and process-owned.
- SB06 depends on this phase to remove or honestly classify the previous run-detail UI gap.

## Validation Depth
- Critical subbundle.
- Requires focused service/API tests, UI proof if a panel is added, source assertions, anti-stub scan, semantic adequacy proof, and concise artifact-backed proof.

## Implementation Steps

- Do not add mutating manager commands.
- Do not expose execution-capable approval.
- Ensure readback uses existing `IProcessManagerReadOnlyVerificationFacade` and process-owned services.


## Acceptance Checklist
- Real `src` or `tests` changes land for this subbundle unless it is explicitly a release-decision-only phase.
- All process proof uses process-owned runtime surfaces.
- No forbidden driver/runtime side-effect surface is introduced.
- No Process Core dependency or vocabulary drift is introduced.

## Proof Required
- Focused service/API tests for runtime-host readback tied to real process run/step ids.
- Large-desktop screenshot if UI panel is added.
- Readback includes audit id/hash, capability key, denial category/code, evidence count, no-mutation flags, and dry-run denial details.

## Browser Validation Logging
- If UI is added, record route, 1900x1200 viewport, Playwright MCP actions, screenshot, assertions, and result in `bundle://reviews/01-execution-report.md`.
- If API-only closure is selected, record N/A with the reason and proof that the UI gap was eliminated or explicitly reclassified.

## Progression Gate
- SB05 may start only after runtime-host readback proof, no-mutation proof, semantic invariant contract, and `bundle://proof/SB04/manifest.md` exist.
- Downstream work must stop if this subbundle cannot prove its outcome without weakening the scope.


## Do Not Do
- Do not extract Process Runtime Core or dispatcher into a new library in this bundle.
- Do not add execution-capable process drivers.
- Do not add reflection discovery, fallback selectors, dynamic object dispatch, or driver self-registration.
- Do not move template/domain vocabulary into `CanDoItAll.Processes.Core`.
- Do not use manual `SuppressAutomationDispatch = true` as representative automation proof.
- Do not generate large proof trees.


## Suggested Agent Prompt
Implement SB04 as a code-first stabilization phase. Keep proof concise and source-backed. Preserve process runtime boundaries and do not start further Process Core extraction.
