# Project Structure Image Asset Proof

## Status

- `Completed`

## Objective

- Prove that CanDoItAll project-structure image generation can use the ComfyUI Flux image provider and store the generated image as a project asset with readable non-empty content.

## Covered Inputs

- `N004`: test that local image generation is possible.
- `N009`: test image generation via project structure.

## Prerequisites

- SB01 is `Completed` with live Flux generation proof.
- SB02 is `Completed` with focused provider/driver tests and a usable ComfyUI Flux image provider.
- The CanDoItAll web host or component/test host can access the provider catalog and project-structure services.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ImageGeneration.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Images/ProviderRuntimeImageGenerationService.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ImageGeneration/AgentImageGenerationContracts.cs`
- `repo://src/CanDoItAll.Web/ProjectStructureAgentApi.cs`
- `repo://docs/api-control-plane.md`
- `repo://tests/CanDoItAll.Tests.Components/ProjectStructurePageSimpleMutationTests.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs`

## Deliverables

- Project-structure proof that selects the ComfyUI Flux provider and requests image generation.
- Generated asset metadata proof and content byte proof.
- Execution-report rows closing the project-structure raw note.
- SB03 semantic invariant contract and proof manifest.

## Dependency Impact

- This subbundle closes the user-facing requirement; weak proof here means driver tests succeeded but the application workflow requested by the user remains unproven.

## Validation Depth

- Critical end-to-end closure with host/API or browser proof, content readback, semantic positive and negative cases, anti-stub audit, and final raw-note closure.

## Implementation Steps

1. Confirm the available project-structure route or UI path for generated image asset creation.
2. Start the CanDoItAll host if needed and verify API access status.
3. Create or select a test project and acquire a project-structure lease when mutating shared structure.
4. Trigger generated image asset creation using the ComfyUI Flux provider.
5. Read back the specific project-structure node/asset metadata.
6. Read back asset content and assert non-empty image bytes/content.
7. Record transcripts, generated asset ids, source assertions, and final raw-note closure.

## Scope Exceptions

- If project-structure HTTP routes do not expose the generated-image create action directly, use the existing Blazor/component path or runtime tool path and record the exact route/window used.
- Do not broaden this phase into unrelated project-structure UI redesign.

## Do Not Do

- Do not prove project-structure storage with manually uploaded fixture bytes.
- Do not call ComfyUI directly from project-structure proof while bypassing `IAgentImageGenerationService`.
- Do not mark raw note `N009` solved unless asset metadata and content readback both pass.

## Acceptance Checklist

- `Passed`: Project-structure request path uses an enabled ComfyUI image-generation provider.
- `Passed`: Generated image bytes come from the provider runtime service path.
- `Passed`: Project-structure asset metadata includes the generated image title/content type.
- `Passed`: Project-structure asset content readback is non-empty.
- `Passed`: Execution report closes `N004` and `N009` with proof paths.
- `Passed`: Final completed-stage bundle validation passes or any failure is reopened.

## Proof Required

- `proof/SB03/transcripts/project-structure-comfyui-image-asset.txt`
- `proof/SB03/transcripts/asset-content-readback.txt`
- `proof/SB03/transcripts/anti-stub-audit.txt`
- `proof/SB03/source-assertions.md`
- `proof/SB03/semantic-invariants.md`
- `proof/SB03/manifest.md`
- Browser screenshots are `N/A`; proof used host/service integration rather than UI automation.

## Browser Validation Logging

- Route/window: project-structure UI route, HTTP API, component harness, or runtime tool path used for generated image asset creation.
- Viewport: large desktop browser proof if UI is used; `N/A` for pure host/API proof.
- Actions/assertions: select/create project, select ComfyUI provider, submit image prompt, verify asset node, read asset content.
- Screenshots/artifacts: screenshot paths if browser UI is used; otherwise command transcripts and generated asset content proof.
- Review questions: Did project structure call the runtime image service, did it store provider-produced bytes, and can the asset be read back?

## Progression Gate

- `Passed`: Final bundle closure may start because SB03 is `Completed`, raw notes `N004` and `N009` are closed with asset proof, and the generated asset content readback exists.

## Suggested Agent Prompt

```text
Implement SB03 only after SB01 and SB02 pass. Prove project-structure image asset creation with the ComfyUI Flux provider through the existing service boundary. Capture metadata and content readback proof, update raw-note closure, and run final validators.
```
