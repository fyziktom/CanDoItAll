# Target Solution

## End State

- CanDoItAll has a usable ComfyUI Flux provider profile/configuration that targets `ImageGenerationFlux.json` semantics.
- The ComfyUI driver accepts Flux-style workflows where the positive prompt, sampler seed, latent size, and output nodes are explicit, and the negative prompt node is optional.
- Project-structure generated image assets can be created from the ComfyUI Flux provider through the existing `IAgentImageGenerationService` path.

## Boundaries

- Provider protocol code stays in `CanDoItAll.AgentFramework.Providers`.
- Provider seed/catalog changes stay in `CanDoItAll.AgentFramework.Persistence`.
- Project-structure asset storage stays in `CanDoItAll.Modules.Workbench` and should not gain direct ComfyUI HTTP knowledge.
- Runtime dispatch stays in `CanDoItAll.AgentFramework.Maf`.

## Minimal Edit Strategy

- Prefer adding a small typed Flux provider configuration/default helper over spreading magic node ids through tests and UI code.
- Keep the existing generic ComfyUI workflow mutation model; do not hardcode Flux-only behavior into the driver when configuration can express it safely.
- Add or adjust tests around the real Flux workflow shape instead of adding broad abstractions.
- Add live proof scripts or transcripts under the bundle, not production test code that depends on a developer workstation ComfyUI server.

## Error Handling

- Missing ComfyUI base URL, missing workflow JSON/path, missing prompt node, failed `/prompt`, failed `/history`, failed `/view`, timeouts, and unsupported source images must fail explicitly.
- No fallback to OpenAI image generation, Ollama, a different ComfyUI workflow, or stale generated files is allowed.
