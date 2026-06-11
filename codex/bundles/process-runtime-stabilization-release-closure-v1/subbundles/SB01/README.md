# SB01: Baseline reconciliation and previous blocker closure

## Status
Prepared.

## Objective
Reconcile current branch state, previous SB08 code-first blocker, and actual functional release posture before changing runtime behavior.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs

## Scope
Capture the exact bundle-start SHA. Compare current implementation against the previous blocked report. Ensure ratio guard uses explicit start SHA and counts only `src + tests` as implementation.

## Validation Depth
Critical. Requires focused tests, source assertions, anti-stub scan, boundary scan, and concise proof.

## Implementation Steps

- Add or repair guard tests so final closure cannot use a conservative `HEAD worktree` fallback unless explicitly marked blocked.
- Classify previous blocked SB08 as functional pass but release-process closure blocker.
- Record exact list of functional blockers and non-blockers.


## Acceptance Checklist
- Real `src` or `tests` changes land for this subbundle unless it is explicitly a release-decision-only phase.
- All process proof uses process-owned runtime surfaces.
- No forbidden driver/runtime side-effect surface is introduced.
- No Process Core dependency or vocabulary drift is introduced.

## Proof Required
- `git diff --numstat <bundle-start-sha>...HEAD` grouped by `src`, `tests`, `docs`, `codex/bundles`.
- Focused guard test showing implicit/stale baseline is rejected.
- Source scan proving no long-lived test reads concrete bundle files except intentional guard fixtures.

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
Implement SB01 as a code-first stabilization phase. Keep proof concise and source-backed. Preserve process runtime boundaries and do not start further Process Core extraction.
