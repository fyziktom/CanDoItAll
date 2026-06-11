# SB05: Scheduler/workflow process-owned lifecycle closure

## Status
- Status: `Completed`

## Objective
Prove scheduler/workflow-origin process starts and read-only verification jobs work through process-owned paths without driver hooks.

## Covered Inputs
- `bundle://inputs/00-original-request.md`: determine whether scheduler/workflow process starts still work like before without further extraction.
- `bundle://requirements/01-normalized-requirements.md`: REQ-006.

## Prerequisites
- SB04 closure gate must pass.
- Runtime-host readback proof must not expose mutating verification behavior.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs

## Scope
- Harden tests and source boundaries for `StartRunFromTriggerAsync`, scheduler/workflow metadata persistence, start-run/outbox records, verification job lifecycle, and audit/readback.
- Keep verification jobs read-only and process-owned.

## Dependency Impact
- SB06 depends on this phase for scheduler/workflow release confidence and boundary scan proof.
- A failure here reopens SB03/SB04 if representative automation or readback assumptions are contradicted.

## Validation Depth
- Critical subbundle.
- Requires focused integration tests, lifecycle assertions, source assertions, anti-stub scan, boundary scan, semantic adequacy proof, and concise artifact-backed proof.

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
- N/A unless the phase changes browser-visible behavior; otherwise record N/A with reason in `bundle://reviews/01-execution-report.md`.

## Progression Gate
- SB06 may start only after scheduler/workflow lifecycle proof, read-only verification proof, semantic invariant contract, and `bundle://proof/SB05/manifest.md` exist.
- Downstream work must stop if this subbundle cannot prove its outcome without weakening the scope.


## Do Not Do
- Do not extract Process Runtime Core or dispatcher into a new library in this bundle.
- Do not add execution-capable process drivers.
- Do not add reflection discovery, fallback selectors, dynamic object dispatch, or driver self-registration.
- Do not move template/domain vocabulary into `CanDoItAll.Processes.Core`.
- Do not use manual `SuppressAutomationDispatch = true` as representative automation proof.
- Do not generate large proof trees.


## Suggested Agent Prompt
Implement SB05 as a code-first stabilization phase. Keep proof concise and source-backed. Preserve process runtime boundaries and do not start further Process Core extraction.
