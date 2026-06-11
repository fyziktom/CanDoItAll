# SB05: Deterministic runtime and UI regression matrix

## Status
- Status: Completed

## Objective
Deterministic runtime and UI regression matrix

## Covered Inputs
- Original user stabilization request.
- Latest runtime-stable-live-blocked decision.
- Live provider model_not_found evidence for `5.4-mini`.

## Prerequisites
- SB04 live smoke must be passed or precisely classified.
- SB03 model-resolution policy tests must still pass after live-smoke changes.

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
- SB06 and SB07 depend on this phase proving deterministic runtime and UI behavior after the live-policy repair.
- If deterministic or UI proof fails, final classification must be `not-runtime-stable`.

## Validation Depth
- Critical foundation.
- Require focused deterministic integration proof and large desktop Playwright proof.
- Require `proof/SB05/manifest.md` and `proof/SB05/semantic-invariants.md`.

## Implementation Steps
- Run the representative Blazor, software-delivery, PostgreSQL business-plan, scheduler/workflow-origin, and read-only verification tests.
- Run the large desktop project/project-structure launch-to-completed-run Playwright proof.
- Capture screenshots and route/action/assertion details for UI proof.
- Classify any deterministic or browser failure separately from provider/model failures.

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

## Proof Required
- Command transcripts for deterministic process runtime integration tests.
- Playwright transcript and screenshot artifact for large desktop completed-run proof.
- Semantic adequacy proof that rejects status/count-only process proof.
- Anti-stub audit transcript covering runtime and UI proof surfaces.

## Browser Validation Logging
- Required for SB05.
- Use large desktop 1900x1200, record route, assertions, Playwright MCP evidence, and screenshot paths.

## Progression Gate
- Proceed to SB06 only after deterministic runtime and UI completed-run proof pass or are honestly classified as runtime/UI failures.
- Reopen SB05 if boundary scans or final decision expose a deterministic or UI regression missed by this phase.

## Suggested Agent Prompt
Implement SB05 as a stabilization task. Prefer real source/test fixes only when needed. Keep process runtime stable and do not begin runtime-core extraction.
