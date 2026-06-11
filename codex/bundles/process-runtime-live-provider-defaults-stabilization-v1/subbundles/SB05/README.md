# SB05: Deterministic runtime and UI regression matrix

## Status
Prepared.

## Objective
Deterministic runtime and UI regression matrix

## Covered Inputs
- Original user stabilization request.
- Latest runtime-stable-live-blocked decision.
- Live provider model_not_found evidence for `5.4-mini`.

## Prerequisites
Follow dependency map in `plan/01-phase-plan.md`.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Processes.Core
- repo://src/CanDoItAll.Processes.Contracts

## Scope

Rerun deterministic process runtime proof after live policy changes.

Acceptance:
- Representative Blazor automation passes.
- Canonical multi-team/software-delivery automation passes.
- PostgreSQL business-plan automation passes.
- Scheduler/workflow origin starts pass.
- Read-only verification jobs pass.
- Large-desktop project/project-structure launch-to-completed-run Playwright proof passes.


## Dependency Impact
Downstream subbundles cannot claim stabilization until this subbundle's classification/proof is complete.

## Validation Depth
Critical. Require source-backed tests or command transcripts, plus source scans where applicable.

## Do Not Do
- Do not extract dispatcher/runtime core into a new library.
- Do not add execution-capable drivers.
- Do not add fallback provider/driver selectors.
- Do not bypass managed providers with raw OpenAI calls.
- Do not count skipped live tests as live proof.
- Do not leak secrets.

## Acceptance Checklist
- Functional behavior is verified or blocker is precisely classified.
- No Process Core leakage.
- No hidden runtime extraction.
- No direct provider bypass.
- Proof is concise and source-backed.

## Browser Validation Logging
Use N/A unless this subbundle affects UI. For UI proof use large desktop 1900x1200 and record route, assertions, and screenshot paths.

## Progression Gate
Proceed only after source/test/build/browser/live evidence is honestly classified.

## Suggested Agent Prompt
Implement SB05 as a stabilization task. Prefer real source/test fixes only when needed. Keep process runtime stable and do not begin runtime-core extraction.
