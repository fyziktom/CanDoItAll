# SB03 Source Assertions

## Project-Structure Boundary

- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ImageGeneration.cs:35` injects `IAgentImageGenerationService`; the Blazor project-structure page does not instantiate or call ComfyUI directly.
- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ImageGeneration.cs:69` handles the generated image asset create action.
- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ImageGeneration.cs:81` calls `ImageGenerationService.GenerateAsync`.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Images/ProviderRuntimeImageGenerationService.cs:7` implements `IAgentImageGenerationService` through the provider runtime.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Images/ProviderRuntimeImageGenerationService.cs:11` validates and dispatches image-generation requests through provider drivers.

## Live Project-Structure Proof

- `repo://tests/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs:2191` defines the opt-in live ComfyUI Flux project-structure proof.
- `repo://tests/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs:2202` resolves `IAgentImageGenerationService` from the application service provider.
- `repo://tests/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs:2215` generates image bytes through the seeded ComfyUI Flux provider.
- `repo://tests/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs:2253` reads back the created project-structure asset content.
- `repo://tests/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs:2744` writes the proof summary and copied PNG when the live proof artifact directory is configured.

## Asset Proof Values

- Provider: `Local ComfyUI Flux`
- Model: `flux1-dev.safetensors`
- Project id: `266c627b-3eb2-4ad4-8eb9-8e7b6e4eb3ba`
- Node id: `custom:7299cf6d60224dcf83b1fdc40a1b7972`
- Media path: `managed-files/project-media/images/266c627b3eb24ad48eb98e7b6e4eb3ba/live-comfyui-flux-proof-b2d16a4bb38f417eadc3d0feaeee4ebc.png`
- Content type: `image/png`
- Content length: `665005`
- SHA-256: `30AA6AF773ABF9E83054001FD0FADB9AC7CB60DFE6098FBF893C5CA26123D553`

## Hashes

- `repo://tests/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs`: `4BB05D1575BD8008CDB4BBDDB2DCB9B29A8E1F9E4680F43C0CE4B61CC6683FE0`
- `bundle://proof/SB03/transcripts/project-structure-comfyui-image-asset.txt`: `6C095C5059F155D342B0DE22A3A5F19D3C2DBC0D17C257237E7B00382E4C59C0`
- `bundle://proof/SB03/transcripts/asset-content-readback.txt`: `1C8946445CFD18DE90ADEC56A4DF2C589817B94F4DFBD02DEA2487F25BBCA264`
- `bundle://proof/SB03/generated/project-structure-live-comfyui-flux-summary.json`: `07BDA559E783C24EDB57C9B45C96AB9263A8634AA208ACA7B19B0F695485905A`
- `bundle://proof/SB03/generated/project-structure-live-comfyui-flux.png`: `30AA6AF773ABF9E83054001FD0FADB9AC7CB60DFE6098FBF893C5CA26123D553`
