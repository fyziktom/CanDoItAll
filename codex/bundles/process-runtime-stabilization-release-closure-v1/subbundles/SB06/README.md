# SB06: Final release matrix and merge-readiness decision

## Status
- Status: `Completed`
- Closure result: deterministic runtime matrix green; final release decision `not merge-ready` because the code-first ratio gate failed.

## Objective
Run the release matrix and make a clear decision whether processes are stable enough to merge/stabilize before further extraction.

## Covered Inputs
- `bundle://inputs/00-original-request.md`: make the final stabilization decision before further Process Core extraction.
- `bundle://requirements/01-normalized-requirements.md`: REQ-007 and REQ-008.

## Prerequisites
- SB05 closure gate must pass.
- All prior critical proof manifests and semantic invariant contracts must exist.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs

## Scope
- Run build, unit tests, focused template automation, Playwright launch-to-completion, scheduler/workflow, runtime-host readback, source scans, and live OpenAI classification.
- Produce the final release decision file and raw-note closure.

## Dependency Impact
- SB06 closes the bundle and decides whether the branch is merge-ready, runtime-ready-but-UI/live-blocked, or not merge-ready.
- If final matrix proof contradicts any prior phase, reopen the affected subbundle instead of closing the bundle.

## Validation Depth
- Critical final-closure subbundle.
- Requires build, unit/focused integration tests, Playwright proof, source assertions, anti-stub and boundary scans, live-smoke classification, red-team verifier artifact, semantic adequacy proof, and concise artifact-backed proof.

## Implementation Steps

- If live env is present, run bounded live template smoke with explicit model, timeout, and token cap.
- If live env is absent, classify as skipped and decide whether deterministic process-mock proof is sufficient for merge.
- Final decision must be one of: merge-ready, runtime-ready-but-UI/live-blocked, not merge-ready.


## Acceptance Checklist
- Real `src` or `tests` changes land for this subbundle unless it is explicitly a release-decision-only phase.
- All process proof uses process-owned runtime surfaces.
- No forbidden driver/runtime side-effect surface is introduced.
- No Process Core dependency or vocabulary drift is introduced.

## Proof Required
- Build 0 warnings/errors.
- Full unit suite green.
- Focused integration matrix green.
- Playwright large desktop launch-to-completion green.
- Live OpenAI template smoke run if explicit env is present; otherwise honestly skipped and not counted.
- Final release decision file.

## Browser Validation Logging
- Record final route, 1900x1200 viewport, Playwright MCP actions, screenshots, assertions, and result in `bundle://reviews/01-execution-report.md`.
- Reuse SB02 route only if it still proves the final release claim.

## Progression Gate
- Bundle closure may run only after the final release matrix, raw-note closure, red-team verifier artifact, semantic invariant contract, `bundle://proof/SB06/manifest.md`, and completed-stage validator are ready.
- If any required proof is missing or weak, mark the bundle blocked or reopen the owning subbundle.


## Do Not Do
- Do not extract Process Runtime Core or dispatcher into a new library in this bundle.
- Do not add execution-capable process drivers.
- Do not add reflection discovery, fallback selectors, dynamic object dispatch, or driver self-registration.
- Do not move template/domain vocabulary into `CanDoItAll.Processes.Core`.
- Do not use manual `SuppressAutomationDispatch = true` as representative automation proof.
- Do not generate large proof trees.


## Suggested Agent Prompt
Implement SB06 as a code-first stabilization phase. Keep proof concise and source-backed. Preserve process runtime boundaries and do not start further Process Core extraction.
