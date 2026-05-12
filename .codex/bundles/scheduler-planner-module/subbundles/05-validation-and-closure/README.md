# 05-validation-and-closure

## Status

- `Ready`

## Objective

- Prove the Scheduler/Planner feature end to end, close gaps found during execution, and prepare the final implementation report.

## Success Criteria

- All normalized requirements have proof or documented residual risk.
- Full solution build passes.
- Integration, component, and Playwright/browser validation cover the critical paths.
- Quartz DB recovery proof is recorded clearly.
- CRON description package/version and Quartz compatibility proof are recorded.
- Execution report is complete and ready for handoff.

## Covered Inputs

- SPM-R001 through SPM-R016

## Prerequisites

- `01-scheduler-domain-and-persistence` complete.
- `02-quartz-db-recovery-and-fire-dispatch` complete.
- `03-process-and-workflow-run-adapters` complete.
- `04-scheduler-planner-ui` complete.

## Exact Source References

- `C:\repositories\CanDoItAll\.codex\bundles\scheduler-planner-module\requirements\01-normalized-requirements.md`
- `C:\repositories\CanDoItAll\.codex\bundles\scheduler-planner-module\traceability\01-requirement-traceability.md`
- `C:\repositories\CanDoItAll\.codex\bundles\scheduler-planner-module\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright`

## Deliverables

- Final build/test/browser proof.
- Requirement traceability review.
- Execution report updated with commands, screenshots, decisions, and residual risks.
- Fixes for validation failures that are within SchedulerPlanner scope.
- Final closure summary for the user.

## Dependency Impact

- This is the closure gate. If it is weak, the feature may appear complete while missing restart recovery, target correlation, or UI behavior.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Read `requirements/01-normalized-requirements.md` and traceability map.
2. Verify each subbundle's execution report entries and evidence.
3. Run full solution build.
4. Run integration tests, including Automation runtime and SchedulerPlanner tests.
5. Run component tests.
6. Run Playwright/browser validation for SchedulerPlanner route and tabs.
7. Inspect screenshots manually for layout, clipping, and text overlap.
8. Fix in-scope failures and rerun targeted proof.
9. Update execution report with final evidence and residual risks.

## Scope Exceptions

- Do not expand into unrelated Automation, Processes, or AgentFramework refactors.
- Do not suppress flaky tests without root-cause notes and explicit residual risk.

## Do Not Do

- Do not close the bundle if Quartz DB recovery has not been proven.
- Do not treat screenshots as proof if they are not manually reviewed.
- Do not declare CRON description complete without Quartz-style CRON compatibility evidence.

## Acceptance Checklist

- Every SPM requirement has matching proof in traceability or execution report.
- Build passes.
- Integration tests pass or failures are explicitly unrelated and documented.
- Component tests pass.
- Browser proof covers all SchedulerPlanner tabs.
- Final report identifies package versions, DB provider behavior, and known limitations.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`
- Browser screenshots for SchedulerPlanner route and tabs.
- Updated execution report.

## Browser Validation Logging

- Target route: final SchedulerPlanner route.
- Required viewport passes: wide desktop and narrower layout.
- Required actions: navigate page, inspect each tab, validate form error state, validate CRON description preview, search history.
- Required evidence: screenshot paths in execution report.
- Screenshot review questions: Does the page match existing product density? Are controls usable and non-overlapping? Is failure/dead-letter state visible? Are scheduled run rows scannable?

## Progression Gate

- Bundle may close only when every must-have requirement is proven or a named blocker is documented with exact remaining work.

## Suggested Agent Prompt

```text
Execute subbundle 05 only. Validate all Scheduler/Planner requirements end to end, fix in-scope failures, update the execution report with evidence, and do not close if Quartz DB recovery, target correlation, or tabbed UI proof is missing.
```
