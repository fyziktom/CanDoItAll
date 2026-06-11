# SB03: Representative template automation regression hardening

## Status
Prepared.

## Objective
Keep Blazor/.NET, canonical multi-team/software-delivery, and business-analysis process automation green and unambiguous.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs

## Scope
Strengthen backend E2E so representative tests assert launch plan approval/execution, outbox completed, execution-run per active automated step, finalizer summaries, artifacts, assignments, branch outcomes, and managed file readback.

## Validation Depth
Critical. Requires focused tests, source assertions, anti-stub scan, boundary scan, and concise proof.

## Implementation Steps

- If PostgreSQL is available, make the business automation proof PostgreSQL-backed; otherwise classify as explicit environment blocker rather than replacing with in-memory proof.
- Preserve manual tests only as state/contract tests.
- Add diagnostics for dead-lettered outbox rows and failed runs.


## Acceptance Checklist
- Real `src` or `tests` changes land for this subbundle unless it is explicitly a release-decision-only phase.
- All process proof uses process-owned runtime surfaces.
- No forbidden driver/runtime side-effect surface is introduced.
- No Process Core dependency or vocabulary drift is introduced.

## Proof Required
- Focused integration matrix for `Blazor_app_delivery_template_SB03_INV`, `Software_delivery_template_SB04_INV`, `Business_plan_process_SB05_INV`.
- Source scan proving representative automation proof methods do not contain `SuppressAutomationDispatch = true`.
- Manual-transition tests renamed/classified as `manual_contract` or equivalent.

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
Implement SB03 as a code-first stabilization phase. Keep proof concise and source-backed. Preserve process runtime boundaries and do not start further Process Core extraction.
