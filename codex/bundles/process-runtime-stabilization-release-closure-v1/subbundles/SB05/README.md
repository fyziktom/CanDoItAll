# SB05: Scheduler/workflow process-owned lifecycle closure

## Status
Prepared.

## Objective
Prove scheduler/workflow-origin process starts and read-only verification jobs work through process-owned paths without driver hooks.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs

## Scope
Harden tests and source boundaries for `StartRunFromTriggerAsync`, scheduler/workflow metadata persistence, start-run/outbox records, verification job lifecycle, and audit/readback.

## Validation Depth
Critical. Requires focused tests, source assertions, anti-stub scan, boundary scan, and concise proof.

## Implementation Steps

- Add lifecycle result object if current job runner returns insufficient status/provenance.
- Ensure verification jobs remain read-only and cannot mutate process state.
- Include failure diagnostics for stuck scheduler/workflow runs.


## Acceptance Checklist
- Real `src` or `tests` changes land for this subbundle unless it is explicitly a release-decision-only phase.
- All process proof uses process-owned runtime surfaces.
- No forbidden driver/runtime side-effect surface is introduced.
- No Process Core dependency or vocabulary drift is introduced.

## Proof Required
- Focused integration tests for SchedulerPlan and WorkflowRun origins.
- Verification job lifecycle states/timestamps/provenance/readback.
- Source scan proving scheduler/workflow paths do not call driver runtime hooks directly.

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
Implement SB05 as a code-first stabilization phase. Keep proof concise and source-backed. Preserve process runtime boundaries and do not start further Process Core extraction.
