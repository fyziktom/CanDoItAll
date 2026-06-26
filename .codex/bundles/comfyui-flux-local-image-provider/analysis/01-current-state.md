# Current State

## Repository Observations

- `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/ComfyUiProviderDriver.cs` already implements `IProviderHealthDriver` and `IProviderImageGenerationDriver` for `ProviderKind.ComfyUi`.
- The current driver loads workflow JSON from provider configuration, writes a required positive prompt node, optionally writes a negative prompt node, optionally writes sampler seed and latent size inputs, enqueues `/prompt`, polls `/history/{promptId}`, and downloads `/view`.
- `repo://tests/CanDoItAll.Tests.Unit/AgentFramework/Providers/ComfyUiProviderDriverTests.cs` covers the basic SD-style happy path, missing workflow JSON, HTTP failure, timeout, and source-image rejection.
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs` seeds OpenAI image generation plus local and remote Ollama chat providers, but no seeded ComfyUI Flux image provider.
- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ImageGeneration.cs` creates generated image project assets by selecting an enabled `ProviderProfilePurpose.ImageGeneration` provider, calling `IAgentImageGenerationService`, and storing the returned image bytes as an uploaded project asset.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Images/ProviderRuntimeImageGenerationService.cs` dispatches image generation through the provider runtime pool and concrete provider driver registry.

## External Sample Observations

- `bundle://inputs/sample/ImageGenerationFlux.json` is a Flux workflow with positive prompt node `56:51`, sampler node `56:52`, latent size node `56:50`, output node `9`, and a negative conditioning node `56:54` that is not text-prompt based.
- `bundle://inputs/sample/ComfyUiService.cs` uses a simple direct ComfyUI protocol: load workflow, set positive prompt text, optionally set negative prompt text when configured, randomize seed, POST `/prompt`, poll `/history/{promptId}`, and download images from `/view`.
- `bundle://inputs/sample/ComfyUiOptions.cs` shows a prior working local/LAN base URL pattern and Flux node ids: `BaseUrl`, `WorkflowPath`, `PositivePromptNodeId = "56:51"`, `SamplerNodeId = "56:52"`, `TimeoutSeconds = 180`.

## Initial Architecture Assessment

- The existing CanDoItAll driver is not fundamentally wrong; it is close to the sample protocol.
- The driver and provider seed are missing a durable, explicit Flux configuration path, which makes the previous SD-style example easy to misconfigure.
- The Flux workflow has no negative prompt text input. Any implementation that requires a negative prompt node would be wrong for this flow.
- Project-structure proof should exercise the existing `IAgentImageGenerationService` path rather than inventing a separate image-generation service.
