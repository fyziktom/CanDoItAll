# SB04 Semantic Invariants

- Invariant ID: `SB04-R10`
- Source raw note: Rebuild and restart the 5032 app, then test the right-click Generate image path with Playwright MCP.
- Expected behavior: The live 5032 UI exposes providers, accepts the prompt, creates the waiting node, and updates it after ComfyUI returns image media.
- Disallowed shallow implementation: API-only validation or opening the create dialog without using the right-click Assets menu is insufficient.
- Failing-first test: `bundle://proof/shared/transcripts/failing-first-compile.md` records the failing compile checkpoint that had to be repaired before browser validation.
- Passing test: `bundle://proof/shared/transcripts/passing-tests-and-build.md` records clean build and targeted tests.
- Changed source files: `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ImageGeneration.cs`, `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureDeferredNodeCompletion.cs`, and `repo://tests/CanDoItAll.Tests.Components/ProjectStructurePageSimpleMutationTests.cs`.
- Production assertions: `bundle://proof/shared/transcripts/browser-right-click-comfyui.md` records live form provider options, prompt entry, immediate node creation, queued status, and final PNG media.
- Red-team negative case: `bundle://proof/shared/transcripts/passing-tests-and-build.md` records same-node provider failure behavior.
- Downstream dependency check: `bundle://proof/shared/transcripts/browser-right-click-comfyui.md` confirms the rebuilt 5032 instance and Local ComfyUI Flux provider completed a real image.

