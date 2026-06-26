# Scope Inventory

## In Scope

- `ProjectStructurePage.ImageGeneration.cs`
- `ProjectStructurePage.razor` create and surface patch hooks only as needed
- `ProjectWorkbenchService` media replacement/status metadata methods
- `ProjectObjectMetadataEnvelope` operational deferred completion overlay
- Workbench DI registration
- Component and focused unit tests
- Playwright validation on the restarted 5032 app

## Out Of Scope

- Reworking the provider dropdown component behavior
- Changing ComfyUI workflow defaults unless proof shows the prompt node is wrong
- Introducing a persistent global job table in this bundle
- Replacing the process/workflow runtime queue
- Broad refactors of project structure graph assembly

## Related Surfaces To Smoke

- Existing quick note create patching
- Existing status/progress updates through selection/signals
- Existing image asset upload preview
- Generated image provider list in node context actions
- Project structure reload after a generated image node exists
