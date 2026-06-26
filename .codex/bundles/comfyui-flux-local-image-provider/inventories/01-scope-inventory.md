# Scope Inventory

| Area | Files or paths | Notes |
| --- | --- | --- |
| ComfyUI provider driver | `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/ComfyUiProviderDriver.cs` | Existing production protocol implementation and provider options. |
| Driver tests | `repo://tests/CanDoItAll.Tests.Unit/AgentFramework/Providers/ComfyUiProviderDriverTests.cs` | Focused tests for prompt enqueue, history polling, download, failures, and unsupported edits. |
| Provider seed | `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs` | Existing seed catalog lacks a ComfyUI Flux image provider. |
| Seed normalization | `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedNormalizer.cs` | Existing merge behavior preserves existing non-managed provider configuration. |
| Feature matrix | `repo://src/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs` and `repo://tests/CanDoItAll.Tests.Unit/ProviderFeatureMatrixTests.cs` | ComfyUI is already treated as private image provider behavior. |
| Runtime image service | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Images/ProviderRuntimeImageGenerationService.cs` | Dispatches image requests through concrete provider drivers. |
| Project-structure image path | `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ImageGeneration.cs` | Creates project assets from `IAgentImageGenerationService` output. |
| External Flux workflow | `bundle://inputs/sample/ImageGenerationFlux.json` and `C:\programovani\csharp\zyphonote_marketing_prompts\ImageGenerationFlux.json` | Required workflow for live proof and driver tests. |
| External sample driver | `bundle://inputs/sample/ComfyUiService.cs` | Working reference for direct ComfyUI protocol. |
