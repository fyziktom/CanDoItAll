# SB02: UI project/project-structure launch-to-completed-run proof

## Status
Prepared.

## Objective
Prove that a user can launch a process from project/project-structure and observe a completed run with steps/artifacts/readback, not only run creation.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs

## Scope
Extend the large-desktop Playwright path to wait for process-mock automation completion. Use a representative template that can finish deterministically within a bounded timeout. Verify run status, visible completed steps, artifacts, and no Blazor error UI.

## Validation Depth
Critical. Requires focused tests, source assertions, anti-stub scan, boundary scan, and concise proof.

## Implementation Steps

- Reuse existing project-structure launch test where possible.
- Add a polling helper that waits until run status is Completed and outbox rows are completed.
- Add screenshot after completion and artifact visibility/readback.
- Keep viewport large desktop only.


## Acceptance Checklist
- Real `src` or `tests` changes land for this subbundle unless it is explicitly a release-decision-only phase.
- All process proof uses process-owned runtime surfaces.
- No forbidden driver/runtime side-effect surface is introduced.
- No Process Core dependency or vocabulary drift is introduced.

## Proof Required
- Playwright focused test with screenshots for start, assignment, executing, completed run summary, completed steps, artifacts/readback.
- API readback confirming run status Completed, outbox completed, artifacts persisted.
- No manual transition proof.

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
Implement SB02 as a code-first stabilization phase. Keep proof concise and source-backed. Preserve process runtime boundaries and do not start further Process Core extraction.
