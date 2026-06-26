# SB01 Semantic Invariants

- Invariant ID: `SB01-R1-R2`
- Source raw note: Check whether the generated-image form truly transfers prompt and provider information.
- Expected behavior: The typed prompt and selected provider fields are present on the request passed to image generation.
- Disallowed shallow implementation: A test that only verifies dropdown rendering without observing the generated image request is insufficient.
- Failing-first test: `bundle://proof/shared/transcripts/failing-first-compile.md` records the compile break found while moving generation code.
- Passing test: `bundle://proof/shared/transcripts/passing-tests-and-build.md` records focused generated-image component and ComfyUI driver tests.
- Changed source files: `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ImageGeneration.cs` and `repo://tests/CanDoItAll.Tests.Components/ProjectStructurePageSimpleMutationTests.cs`.
- Production assertions: `ProjectStructurePage.ImageGeneration.cs` resolves prompt from `CanvasWorkbenchCreateActionRequest.Notes` and input values from keyed fields before queueing the generated image request.
- Red-team negative case: `bundle://proof/shared/transcripts/passing-tests-and-build.md` includes the failure test path proving an explicit failed completion does not masquerade as success.
- Downstream dependency check: `bundle://proof/shared/transcripts/browser-right-click-comfyui.md` proves the right-click form exposes ComfyUI Flux and produces an image through the live provider.

