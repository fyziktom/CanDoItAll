# process-runtime-live-provider-defaults-stabilization-v1

## Status
Prepared for Codex implementation.

## Purpose
Close the final stabilization gap after representative process runtime, UI, scheduler/workflow, and boundary proofs are green, but live OpenAI process-run proof is blocked by forcing an invalid model (`5.4-mini`).

This bundle must verify that processes and agents continue to use the CanDoItAll/MAF managed provider profiles, not direct OpenAI calls or ad-hoc provider bypasses. It must repair the live smoke so it can use the repository's managed OpenAI default provider/model by default, while still supporting explicit bounded overrides when the override is valid.

## Current evidence from review
- Deterministic process runtime, UI, build, unit, integration, and boundary proof are green.
- Live OpenAI test reached provider execution through `OpenAI default`, MAF streaming, process dispatch, finalizer policy, and process usage observation plumbing.
- The live provider failed because the forced model `5.4-mini` was rejected by OpenAI Responses with HTTP 400 `model_not_found`.
- This is currently classified as `runtime-stable-live-blocked`, not as a deterministic runtime regression.
- Next work must not extract Process Runtime Core or dispatcher into a new library. Stabilization first.

## Hard constraints
- Do not begin Process Runtime Core extraction.
- Do not move dispatcher/outbox/finalizer/runtime services into new process-core packages.
- Do not add execution-capable drivers.
- Do not add driver fallback selectors, reflection discovery, self-registration, or hidden scheduler/manager driver hooks.
- Do not bypass MAF/CanDoItAll provider profiles with direct OpenAI client calls from process tests.
- Do not count skipped live tests as live proof.
- Do not treat `5.4-mini` as valid unless the configured provider actually accepts it.
- Keep all code comments in English.

## Final expected outcome
The branch should end with one of these explicit decisions:

1. `runtime-stable-live-passed`: deterministic + UI + boundary + live OpenAI process-run proof pass.
2. `runtime-stable-provider-config-blocked`: deterministic + UI + boundary pass, live fails because configured provider/model/API rejects the request with precise diagnostics.
3. `not-runtime-stable`: deterministic/UI/process runtime path fails independent of provider/model selection.
