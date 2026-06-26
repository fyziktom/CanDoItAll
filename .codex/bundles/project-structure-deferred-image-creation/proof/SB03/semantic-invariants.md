# SB03 Semantic Invariants

- Invariant ID: `SB03-R3-R4-R6`
- Source raw note: The node must appear immediately with a dummy waiting image and later receive the real image or explicit failure.
- Expected behavior: Save creates a persisted image asset node immediately, displays waiting image media, and completes or fails that same node.
- Disallowed shallow implementation: Blocking the dialog until provider completion or drawing an unpersisted canvas-only node is not acceptable.
- Failing-first test: `bundle://proof/shared/transcripts/failing-first-compile.md` records the failing compile checkpoint during the page conversion.
- Passing test: `bundle://proof/shared/transcripts/passing-tests-and-build.md` records tests for immediate placeholder media, same-node completion, and same-node failure.
- Changed source files: `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ImageGeneration.cs`, `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`, and `repo://tests/CanDoItAll.Tests.Components/ProjectStructurePageSimpleMutationTests.cs`.
- Production assertions: The page builds an SVG placeholder containing the waiting text, creates the canonical image asset, enqueues completion, and patches the open surface when the handle completes.
- Red-team negative case: `bundle://proof/shared/transcripts/passing-tests-and-build.md` includes failure coverage proving failed provider work does not delete or recreate the waiting node.
- Downstream dependency check: `bundle://proof/shared/transcripts/browser-right-click-comfyui.md` proves the live canvas shows the waiting node and then a completed PNG.

