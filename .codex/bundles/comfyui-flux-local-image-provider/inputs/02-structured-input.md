# Structured Input

## Raw Notes

| Raw note | Exact source wording | Normalized requirement ids | Owning subbundle | Closure status |
| --- | --- | --- | --- | --- |
| `N001` | "we have some basic driver for ComfyUI" | `R001` | `SB02` | `Not started` |
| `N002` | "Here is much better application where I tested better ComfyUI. It worked also over the local network." | `R002`, `R003` | `SB01` | `Not started` |
| `N003` | "I updated the ComfyUI. It should run on same port. Otherwise you can restart it and change its setting for correct port and expose API." | `R002`, `R004` | `SB01` | `Not started` |
| `N004` | "test that it is possible to use local image generation" | `R002`, `R010` | `SB01`, `SB03` | `Not started` |
| `N005` | "I created new flow in comfyUI ... ImageGenerationFlux.json ... It uses Flux model ... We must use that one." | `R003`, `R006` | `SB01`, `SB02` | `Not started` |
| `N006` | "analyze our actual driver" | `R001` | `SB02` | `Not started` |
| `N007` | "based on that design architecture improvements and implement driver changes" | `R005`, `R008`, `R009` | `SB02` | `Not started` |
| `N008` | "add provider for comfyui" | `R006`, `R007` | `SB02` | `Not started` |
| `N009` | "test image generation via project structure" | `R010` | `SB03` | `Not started` |
| `N010` | "If you cannot solve comfyui connection do not continue. Stop and we must solve it." | `R004` | `SB01` | `Not started` |

## Hard Constraints

- Use the Flux workflow in `ImageGenerationFlux.json`; do not keep targeting the older SD3.5-style example.
- ComfyUI connectivity and image generation proof must pass before CanDoItAll production driver/provider changes start.
- Do not silently fall back to another image provider or another workflow.
- Keep the implementation aligned with existing AgentFramework provider architecture and project-structure image asset creation.

## Primary Validation Expectations

- A live ComfyUI HTTP API call enqueues the Flux workflow and downloads a non-empty image.
- Driver tests prove Flux-style workflow mutation, missing configuration failure, bad workflow failure, timeout failure, and source-image rejection.
- The seeded or default ComfyUI provider appears as an enabled image-generation provider with explicit Flux configuration.
- Project-structure image asset creation stores bytes generated through the ComfyUI Flux provider.
