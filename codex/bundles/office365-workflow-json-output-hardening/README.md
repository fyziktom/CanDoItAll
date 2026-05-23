# Office365 Workflow JSON Output Hardening

This bundle hardens workflow LLM JSON output for the Office365 category summary workflow after `summarize-office365` returned malformed JSON containing an invalid `+` token.

## Profile

- `feedback`

## Mission

Ensure JSON-shaped workflow LLM nodes request provider-enforced JSON response formatting before the model runs, keep strict post-response validation, and prove the Office365 category summary workflow can advance past `summarize-office365` without accepting malformed or prose-wrapped output.

## Outcome Contract

- Requested outcome: Office365 summary workflow LLM output is valid JSON under the existing workflow contract and downstream project-structure storage still receives the expected payload.
- Hard constraints: no silent JSON extraction or best-effort repair; invalid JSON must still fail predictably with actionable diagnostics; preserve project scope and `runContext.office365Processing`.
- Evidence required before closure: focused unit tests for provider response-format options and invalid-output rejection, source assertions, anti-stub audit, build/test transcript, and live Office365 workflow or API-level validation against the running app.
- Known blockers or explicit scope exceptions: live Office365 validation may be blocked by local auth/session/API availability; if blocked, record the exact failure and keep unit/API proof as closure evidence.

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

1. `subbundles/01-runtime-json-contract-hardening`
2. `subbundles/02-office365-live-validation`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Ready`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `API validation completed; browser UI not used`
