# Provider Model Pricing Settings Repair

This bundle is a coordination and execution package for `provider-model-pricing-settings-v1`.

## Profile

- `feedback`

## Mission

- Repair provider settings so model inference pricing is a first-class workflow: users can keep manual per-model prices, refresh model/pricing rows from provider APIs when supported, and handle local LLM models without pretending local APIs supply pricing.

## Outcome Contract

- Requested outcome: provider settings can refresh model pricing rows from supported provider APIs and can manually maintain per-model prices for every provider model, including local LLMs.
- Hard constraints: preserve strongly typed pricing models, do not silently overwrite manual rows, do not claim exact API pricing when an API only exposes model names, keep existing provider settings surfaces intact, avoid schema migrations unless necessary.
- Evidence required before closure: prepared/completed validator output, targeted tests for API-price parsing and manual merge behavior, build or targeted test transcript, changed-file hashes, source assertions for both provider UI surfaces, and raw-note closure.
- Known blockers or explicit scope exceptions: no live external provider secret is required for proof; fixtures stand in for provider APIs. Browser validation may be recorded as a gap if the app host is unavailable.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-provider-model-pricing-settings`
2. Final raw-note closure and completed-stage validation.

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared validator passed`
- Execution status: `Implemented`
- Subbundle gate review: `SB01 passed`
- Final closure gate: `Completed validator passed`
- Browser validation analytics: `Passed on temporary local proof app route settings?tab=providers`
