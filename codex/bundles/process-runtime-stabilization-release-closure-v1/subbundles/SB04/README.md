# SB04: Runtime-host operator readback closure

## Status
Prepared.

## Objective
Close the explicit runtime-host UI/readback gap recorded in the previous bundle.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs

## Scope
Expose manager/runtime-host readback for selected process run/step through an operator-visible surface or a stable API route with a UI follow-up clearly eliminated or reclassified. Prefer adding a compact run-detail diagnostics panel if feasible.

## Validation Depth
Critical. Requires focused tests, source assertions, anti-stub scan, boundary scan, and concise proof.

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
Use large desktop 1900×1200 when the subbundle changes or proves user-visible process launch/readback. Otherwise record N/A with reason.

## Progression Gate
Downstream work must stop if this subbundle cannot prove its outcome without weakening the scope.


## Do Not Do
- Do not extract Process Runtime Core or dispatcher into a new library in this bundle.
- Do not add execution-capable process drivers.
- Do not add reflection discovery, fallback selectors, dynamic object dispatch, or driver self-registration.
- Do not move template/domain vocabulary into `CanDoItAll.Processes.Core`.
- Do not use manual `SuppressAutomationDispatch = true` as representative automation proof.
- Do not generate large proof trees.


## Suggested Agent Prompt
Implement SB04 as a code-first stabilization phase. Keep proof concise and source-backed. Preserve process runtime boundaries and do not start further Process Core extraction.
