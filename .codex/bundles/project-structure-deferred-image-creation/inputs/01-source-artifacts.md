# Source Artifacts

| Artifact | Path | Notes |
| --- | --- | --- |
| Current project structure page | `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor` | Handles right-click create requests, surface patching, and canvas refresh. |
| Generated image create path | `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ImageGeneration.cs` | Resolves provider, prompt, model, size, quality, and output format. Currently waits for provider image before creating the node. |
| Create request composer | `src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureCreateRequestComposer.cs` | Converts canvas create requests into canonical `ProjectObjectCreateRequest` records. |
| Workbench persistence service | `src/CanDoItAll.Modules.Workbench/Workbench/ProjectWorkbenchModels.cs` | Owns object creation, metadata, status/progress updates, and media save on create. |
| Node binding storage | `src/CanDoItAll.Modules.Workbench/ProjectNodes/ProjectNodeBindings.cs` | Persists media route/content type/original filename and references. |
| Canvas graph adapter | `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureGraphAdapter.cs` | Maps image assets with media routes into canvas image previews. |
| Provider runtime image service | `src/CanDoItAll.AgentFramework.Maf/Runtime/Images/ProviderRuntimeImageGenerationService.cs` | Maps app-level request prompt and options to provider driver payload. |
| ComfyUI driver | `src/CanDoItAll.AgentFramework.Providers/Drivers/ComfyUiProviderDriver.cs` | Applies prompt to configured workflow node and downloads generated images. |
| Flux defaults | `src/CanDoItAll.AgentFramework.Models/Providers/ComfyUiProviderDefaults.cs` | Provides local ComfyUI Flux configuration, including positive prompt node `56:51`. |
| Existing component tests | `tests/CanDoItAll.Tests.Components/ProjectStructurePageSimpleMutationTests.cs` | Already covers generated-image provider selection and prompt-to-provider request. Must be updated for deferred completion. |
