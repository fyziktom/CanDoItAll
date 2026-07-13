# Assumptions And Risks

## Working Assumptions

- The process engine should support both general-purpose agents and step-specific suppression without changing the agent's main settings.
- A step can require proof receipts even when the agent already has the relevant tool available.
- Existing `ProcessCapabilityScope` should be evolved or composed with a new proof-contract model instead of replaced wholesale.
- Existing driver abstractions are the preferred extension point for domain-specific fallback behavior.
- MAF should remain a generic runtime capability and receipt enforcement layer.

## Critical Path Risks

- If required proof is modeled only as prompt text, retries will continue to produce artifact-only outcomes.
- If required tools are modeled only as allowed capabilities, HR readiness cannot distinguish "may use" from "must use and must produce a receipt".
- If fallback stays artifact-centric, manager recovery may incorrectly treat missing proof as a missing artifact.
- If all process-specific rules move into MAF, domain leaks return and common workspace tools become harder to reuse outside software delivery.
- If contract compilation rebuilds tool and MCP catalogs per step without caching, launch and dispatch performance can regress.

## Validation Risks

- A build-only validation would miss the original failure mode because the issue is runtime orchestration and proof gating.
- Unit tests that mock only successful tool access would miss missing-receipt outcomes.
- E2E testing must include a negative case where a QA recheck outcome claims success while required browser/image receipts are absent.
- Browser validation must verify actual receipts and artifact paths, not only text in the final report.

## Reopen Triggers

- Any process step can still accept `Completed` when required current-run receipts are missing.
- HR readiness can still launch or dispatch a step whose required MCP/tool/skill is unavailable or suppressed.
- Process templates still rely only on prose for Playwright, screenshot, image analysis, or domain-specific proof requirements.
- MAF workspace plugins regain software-delivery-specific instructions.
- Manager fallback repeats artifact-only attempts for a missing proof receipt without a typed diagnostic.
