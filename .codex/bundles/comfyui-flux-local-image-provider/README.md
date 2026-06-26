# ComfyUI Flux Local Image Provider

This bundle coordinates the ComfyUI Flux driver and provider work for CanDoItAll.

## Profile

- `initiative`

## Mission

Make local ComfyUI Flux image generation usable from CanDoItAll without hiding connection failures or depending on the earlier SD-style workflow assumptions.

## Outcome Contract

- Requested outcome: the existing ComfyUI driver is analyzed, local Flux image generation is proven through the provided ZyphoNote sample/testbed, CanDoItAll gets a usable ComfyUI Flux image provider configuration, and project-structure image asset creation is proven with the local provider.
- Hard constraints: use `C:\programovani\csharp\zyphonote_marketing_prompts\ImageGenerationFlux.json`; do not continue production driver work if ComfyUI cannot be reached and used to generate an image; keep provider logic strongly typed and explicit; do not add silent fallback behavior.
- Evidence required before closure: SB01 live ComfyUI Flux generation transcript and generated image artifact; SB02 driver/provider tests and source assertions; SB03 project-structure generated image asset proof; final bundle validator output.
- Known blockers or explicit scope exceptions: live proof depends on the local or LAN ComfyUI server exposing the HTTP API, usually on port `8188`.

## Bundle Layout

- `inputs/` raw request, copied sample artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-comfyui-flux-connectivity-gate`
2. `subbundles/02-flux-provider-configuration-and-driver-hardening`
3. `subbundles/03-project-structure-image-asset-proof`

## Dependency And Validation Map

- The dependency map, critical-subbundle notes, and phase gates are in `plan/01-phase-plan.md`.
- If the bundle resumes after compaction or by a different agent, use this README, the active subbundle README, and `reviews/01-execution-report.md` as durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed by validate_bundle.py --stage prepared on 2026-06-26`
- Execution status: `SB01, SB02, and SB03 completed; final completed-stage validation passed`
- Subbundle gate review: `SB01 passed; SB02 passed; SB03 passed`
- Final closure gate: `Passed by validate_bundle.py --stage completed on 2026-06-26`
- Browser validation analytics: `Host/API and service integration proof passed; no UI browser route was required`
