# SB01 Semantic Invariants

## Invariant SB01-FLUX-LIVE

- Invariant ID: `SB01-FLUX-LIVE`
- Source raw note: `N004` says "test that it is possible to use local image generation"; `N005` says the new flow uses Flux and "We must use that one"; `N010` says to stop if the ComfyUI connection cannot be solved.
- Expected behavior: a reachable ComfyUI API accepts `ImageGenerationFlux.json`, mutates Flux prompt node `56:51`, completes the queued prompt, exposes an image under output node `9`, and returns non-empty image bytes from the ComfyUI view endpoint.
- Disallowed shallow implementation: checking only the ComfyUI system-stats endpoint, citing old sample generated images, using SD3.5 or OpenAI, or accepting a transcript without a current `prompt_id` and generated image bytes.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-missing-flux-node.txt` removes required Flux node `56:51` and exits non-zero before enqueue.
- Passing test: `bundle://proof/SB01/transcripts/comfyui-flux-live-generation.txt` executes `SB01-FLUX-LIVE`, receives prompt id `e39b85c8-abb7-4a66-9859-b45ff4b5cc47`, downloads `Flux.1_Dev_00081_.png`, and saves `bundle://proof/SB01/generated/flux-live-image.png`.
- Changed source files: no production source files changed in SB01; proof artifacts and copied source artifacts are hashed in `bundle://proof/SB01/manifest.md`.
- Production assertions: no production code path was changed; live proof established the external provider prerequisite for later production integration.
- Red-team negative case: missing required Flux positive node `56:51` is rejected in `bundle://proof/SB01/transcripts/failing-first-missing-flux-node.txt`.
- Downstream dependency check: SB02 may proceed because live ComfyUI Flux generation completed with non-empty bytes and no SB01 blocker remains.
