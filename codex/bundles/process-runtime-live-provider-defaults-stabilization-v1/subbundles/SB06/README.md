# SB06: Boundary and no-extraction scans

## Status
- Status: Completed

## Objective
Boundary and no-extraction scans

## Covered Inputs
- Original user stabilization request.
- Latest runtime-stable-live-blocked decision.
- Live provider model_not_found evidence for `5.4-mini`.

## Prerequisites
- SB05 deterministic and UI regression proof must be passed or precisely classified.
- SB01 through SB04 proof must not show a runtime-core extraction or provider bypass.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Processes.Core
- repo://src/CanDoItAll.Processes.Contracts

## Scope

Ensure stabilization did not start premature architecture extraction or driver execution.

Acceptance:
- Process Core has no runtime/template/provider/EF/UI/driver/domain leakage.
- No new Process Runtime Core package/library extraction.
- No dispatcher/outbox/finalizer move.
- No execution-capable driver host.
- No fallback selector/reflection discovery/self-registration.
- No scheduler/workflow direct driver hook.
- No secret leakage.


## Dependency Impact
- SB07 final decision depends on this phase proving boundary/no-extraction constraints.
- SB08 stabilization ledger depends on this phase to define what remains frozen.

## Validation Depth
- Critical foundation.
- Require boundary scans for Process Core leakage, runtime extraction, driver execution, fallback selectors, direct provider bypass, and secret leakage.
- Require `proof/SB06/manifest.md` and `proof/SB06/semantic-invariants.md`.

## Implementation Steps
- Scan Process Core for runtime/template/provider/EF/UI/driver/domain leakage.
- Scan the repo for new Process Runtime Core package/library extraction and dispatcher/outbox/finalizer moves.
- Scan for execution-capable driver host, fallback selector/reflection discovery/self-registration, scheduler/workflow direct driver hooks, and secret leakage.
- Record source assertions and command transcripts under `proof/SB06/`.

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
- Boundary scan transcripts for each disallowed drift category.
- Source assertion artifact naming allowed and disallowed surfaces.
- Semantic adequacy proof that rejects scanning only project names while missing source references.
- Anti-stub audit transcript covering production paths in the scans.

## Browser Validation Logging
- N/A for SB06 unless a boundary repair changes browser-visible behavior.
- If UI proof becomes necessary, use large desktop 1900x1200 and record route, assertions, and screenshot paths.

## Progression Gate
- Proceed to SB07 only after no-extraction and no-driver/bypass boundaries are source-backed.
- Reopen SB06 if final review or release decision discovers boundary drift.

## Suggested Agent Prompt
Implement SB06 as a stabilization task. Prefer real source/test fixes only when needed. Keep process runtime stable and do not begin runtime-core extraction.
