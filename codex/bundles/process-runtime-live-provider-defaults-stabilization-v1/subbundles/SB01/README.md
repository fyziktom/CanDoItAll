# SB01: Current state and blocker taxonomy

## Status
- Status: Completed

## Objective
Current state and blocker taxonomy

## Covered Inputs
- Original user stabilization request.
- Latest runtime-stable-live-blocked decision.
- Live provider model_not_found evidence for `5.4-mini`.

## Prerequisites
- Prepared-stage bundle validator must pass before implementation starts.
- No earlier subbundle is required; this is the first dependency-map phase.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Processes.Core
- repo://src/CanDoItAll.Processes.Contracts

## Scope

Audit the latest branch state and classify blockers. Confirm whether failures are deterministic runtime failures, UI failures, boundary failures, provider/model failures, or advisory proof/churn failures.

Acceptance:
- Read latest execution report and release decision.
- Confirm deterministic runtime status.
- Confirm live provider status.
- Confirm no Process Runtime Core extraction has started.
- Update release taxonomy only if needed.


## Dependency Impact
- SB02 through SB08 depend on this phase's runtime/provider/blocker taxonomy.
- If the classification changes, downstream proof and final release classification must be reopened.

## Validation Depth
- Critical foundation.
- Require source-backed command transcripts and source scans for runtime/core extraction status.
- Require `proof/SB01/manifest.md` and `proof/SB01/semantic-invariants.md`.

## Implementation Steps
- Read the latest bundle execution report, raw request, and current-state analysis.
- Audit named runtime, UI, core, and integration source references for current shape.
- Classify the current blocker as deterministic runtime, UI, boundary, provider/model, advisory proof, or unknown.
- Record source assertions and command transcripts under `proof/SB01/`.

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
- Prepared-stage validator transcript for the repaired bundle.
- Source scan transcript for Process Core extraction and direct-provider bypass status.
- Semantic adequacy proof that rejects a shallow "status text only" classification.
- Anti-stub audit transcript covering production and test paths touched by this phase.

## Browser Validation Logging
- N/A for SB01 unless the taxonomy audit discovers a UI change in this phase.
- If UI proof becomes necessary, use large desktop 1900x1200 and record route, assertions, and screenshot paths.

## Progression Gate
- Proceed to SB02 only after the current blocker taxonomy is source-backed and no runtime-core extraction has started.
- Reopen SB01 if later subbundles prove the initial classification wrong.

## Suggested Agent Prompt
Implement SB01 as a stabilization task. Prefer real source/test fixes only when needed. Keep process runtime stable and do not begin runtime-core extraction.
