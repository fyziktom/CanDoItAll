# Normalized Requirements

| Requirement | Statement | Acceptance signal | Owning subbundle |
| --- | --- | --- | --- |
| `R001` | Analyze the existing CanDoItAll ComfyUI driver and preserve its provider-runtime boundary unless a real defect requires change. | Current-state analysis names the existing driver, tests, service path, and project-structure caller. | `SB02` |
| `R002` | Prove ComfyUI connectivity with the provided sample/testbed before production code changes. | Transcript shows `/system_stats`, `/prompt`, `/history`, `/view`, and a non-empty generated image from Flux. | `SB01` |
| `R003` | Use `ImageGenerationFlux.json` and Flux node ids, not the older SD3.5 example. | Workflow source proof and prompt payload proof include nodes `56:51`, `56:52`, `56:50`, and output `9`. | `SB01`, `SB02` |
| `R004` | Stop the workflow if ComfyUI connection or Flux generation cannot be solved. | SB01 closure is `Blocked` and later subbundles remain unstarted when live Flux generation fails. | `SB01` |
| `R005` | Improve the driver architecture only where needed for explicit Flux configuration, validation, and predictable failures. | Driver code and tests avoid silent fallback and keep configuration typed through provider options/defaults. | `SB02` |
| `R006` | Add a ComfyUI image-generation provider configuration for the Flux workflow. | Seed/provider test shows an enabled `ProviderKind.ComfyUi` profile with image-generation purpose and Flux workflow settings. | `SB02` |
| `R007` | Ensure provider discovery exposes ComfyUI as an image provider without treating it as a chat/tool provider. | Feature matrix and seed tests prove `SupportsImageGeneration` and no chat/tool capability assumptions. | `SB02` |
| `R008` | Preserve strongly typed, explicit error handling for missing workflow, missing nodes, bad HTTP status, timeout, and unsupported source-image edits. | Focused driver tests cover each case with meaningful exception messages. | `SB02` |
| `R009` | Keep project-structure image generation on the existing service boundary. | Code proof shows project structure still calls `IAgentImageGenerationService` and does not instantiate ComfyUI directly. | `SB03` |
| `R010` | Prove local image generation through project structure. | Live or host-backed proof creates a project-structure image asset whose content bytes are non-empty and produced by the ComfyUI provider. | `SB03` |
