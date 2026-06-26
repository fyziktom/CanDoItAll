# Current State

## Prompt And Provider Path

- `ProjectStructurePage.ImageGeneration.cs` resolves generated-image settings from `CanvasWorkbenchCreateActionRequest`.
- The prompt comes from `request.Notes?.Trim()`.
- The provider field comes from `imageProviderProfileId`.
- Model defaults to the selected provider's default model when `imageModel` is empty.
- Size, quality, and output format are normalized from typed fields and fail explicitly on unsupported values.
- `ProviderRuntimeImageGenerationService.GenerateAsync` passes `request.Prompt`, `Size`, `Quality`, and `Format` to `ProviderImageGenerationRequest`.
- `ComfyUiProviderDriver.ApplyPrompt` writes the prompt into `options.PositivePromptNodeId` and `options.PositivePromptInputName`.
- Existing Flux unit proof asserts prompt text is written to workflow node `56:51`.

## Blocking Create Behavior

- `TryCreateGeneratedImageAssetAsync` calls `ImageGenerationService.GenerateAsync` before calling `CreateObjectAsync`.
- The canvas node is not created until Comfy/OpenAI returns bytes.
- Provider slowness or timeout keeps the create dialog workflow blocked.
- Failure creates no node, so the user has no durable object representing the requested image.

## Persistence And Canonicity

- `ProjectWorkbenchService.CreateObjectAsync` is the canonical path for user-authored project structure objects.
- The service already persists object metadata, status, progress, node references, positions, and media bindings.
- `ProjectNodeBindingStorage` stores media route, content type, original filename, and storage object reference separately from the object record.
- There is no focused method to replace media after the object has already been created.

## Surface Patch Behavior

- `ApplyCreatedSurfaceNodeAsync` patches the new node into the in-memory surface without a full reload.
- `ApplySurfaceNodeUpdatesAsync` patches existing nodes after status/progress/metadata edits.
- The generated-image completion path can reuse `ApplySurfaceNodeUpdatesAsync` when completion is observed inside the current page, or the page can pick up changes on reload.

## Architecture Gap

The missing primitive is a deferred node completion mechanism: create a canonical node now, enqueue slow completion work, then update the same node with new data or a failure state.
