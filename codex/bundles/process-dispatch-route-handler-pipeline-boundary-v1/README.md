# Process Dispatch Route Handler Pipeline Boundary v1

Status: Prepared for Codex implementation  
Profile: Initiative  
Prepared date: 2026-06-06

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `N/A - runtime/service refactor`

## Mission

Continue the `maf-processes-refactor` branch with one more safe module-local dispatcher isolation step.

The previous bundle successfully extracted claim storage/lifecycle and reduced the main dispatch facade. The next bottleneck is the route execution body: `ExecuteClaimedDispatchRouteAsync` still sequentially owns route-stage decisions and handoffs for fresh recovery skip, database requirement, upstream materialization, stranded artifact recovery, subprocess, start transition, workflow, direct-agent execution, competing execution, run-closed guard and finalizer transition.

This bundle must split that route flow into explicit module-local route handlers and route-handler infrastructure **without** creating Process Core and **without** production process driver APIs.

## Hard constraints

- Do not create `CanDoItAll.Processes.Core`.
- Do not add `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, `IProcessHelperDriver`, driver packages, or production driver APIs.
- Do not change UI, Razor, CSS, JavaScript, TypeScript, image, screenshot or mobile/browser proof files.
- Browser validation is `N/A` for this runtime/service refactor.
- Keep route stage order exactly:
  1. FreshRecoverySkip
  2. DatabaseRequirement
  3. UpstreamMaterialization
  4. StrandedArtifactRecovery
  5. Subprocess
  6. StartTransition
  7. Workflow
  8. DirectAgentExecution
  9. CompetingExecutionGuard
  10. RunClosedGuard
  11. FinalizerTransition
- Do not hide EF writes, transition calls, service-scope calls, finalizer calls, or external agent execution inside classes named `Rules`.
- Preserve all original behavior. This is refactoring/hardening only.

## Required closure

Codex must close every subbundle row individually. Do **not** collapse `SB001-SB112` into one row in the execution report.

Every critical gate must include:
- manifest
- semantic invariants
- source scan transcript
- build or focused test transcript
- raw-note closure proof
- anti-stub scan

## Primary source references

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePipeline.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchClaimLease.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExceptionClosure.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
