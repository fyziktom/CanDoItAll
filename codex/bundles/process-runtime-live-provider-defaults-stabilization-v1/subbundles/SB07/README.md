# SB07: Final release decision

## Status
- Status: Completed

## Objective
Final release decision

## Covered Inputs
- Original user stabilization request.
- Latest runtime-stable-live-blocked decision.
- Live provider model_not_found evidence for `5.4-mini`.

## Prerequisites
- SB06 boundary/no-extraction scans must pass.
- SB04 and SB05 must have honest live and deterministic/UI classifications.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Processes.Core
- repo://src/CanDoItAll.Processes.Contracts

## Scope

Produce final release decision.

Acceptance:
- If deterministic + UI + boundary + live pass: `runtime-stable-live-passed`.
- If deterministic + UI + boundary pass but provider rejects model/auth/quota: `runtime-stable-provider-config-blocked`.
- If deterministic/UI path fails: `not-runtime-stable`.
- Code/proof ratio can be advisory, not the only functional blocker.
- Decision must explicitly say whether humans can resume using processes and what caveats remain.


## Dependency Impact
- SB08 depends on this phase's release classification and caveats.
- Final bundle closure depends on this phase to align code proof, execution-report rows, and raw-note closure.

## Validation Depth
- Critical foundation.
- Require final classification backed by build/test/live/UI/boundary transcripts.
- Require `proof/SB07/manifest.md` and `proof/SB07/semantic-invariants.md`.

## Implementation Steps
- Review SB01-SB06 proof and classify the release as `runtime-stable-live-passed`, `runtime-stable-provider-config-blocked`, or `not-runtime-stable`.
- State whether humans can resume using processes and list concrete caveats.
- Ensure the decision separates deterministic/UI runtime failures from provider/model/auth/quota failures.
- Update execution report, raw-note closure, and root validation summary consistently.

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
- Release decision artifact citing build/test/live/UI/boundary proof paths.
- Semantic adequacy proof that rejects advisory-only/code-ratio classification as final behavior proof.
- Anti-stub audit transcript covering final classification text and release-decision files.
- Source assertion artifact proving no final decision contradicts earlier gate rows.

## Browser Validation Logging
- N/A for SB07 unless the final decision depends on fresh browser-visible behavior.
- If UI proof becomes necessary, cite the SB05 browser analytics row or rerun large desktop proof.

## Progression Gate
- Proceed to SB08 only after final release classification is fully supported by proof artifacts and raw-note closure.
- Reopen SB07 if SB08 or final validator finds inconsistent status, proof, or caveat language.

## Suggested Agent Prompt
Implement SB07 as a stabilization task. Prefer real source/test fixes only when needed. Keep process runtime stable and do not begin runtime-core extraction.
