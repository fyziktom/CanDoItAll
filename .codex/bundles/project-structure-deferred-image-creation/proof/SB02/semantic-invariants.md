# SB02 Semantic Invariants

- Invariant ID: `SB02-R5-R8`
- Source raw note: Create a generic project-structure function for nodes that wait for later data.
- Expected behavior: Deferred completion work updates the same canonical node through workbench persistence and storage binding.
- Disallowed shallow implementation: A client-only placeholder or a second replacement node is not acceptable.
- Failing-first test: `bundle://proof/shared/transcripts/failing-first-compile.md` records the compile break caught during the deferred worker extraction.
- Passing test: `bundle://proof/shared/transcripts/passing-tests-and-build.md` records tests that prove completion and failure both target the same node.
- Changed source files: `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureDeferredNodeCompletion.cs` and `repo://src/CanDoItAll.Modules.Workbench/Workbench/ProjectWorkbenchModels.cs`.
- Production assertions: The deferred queue carries typed completion work and `ProjectWorkbenchService.ReplaceObjectMediaAsync` owns media replacement and persisted binding updates.
- Red-team negative case: `bundle://proof/shared/transcripts/passing-tests-and-build.md` proves provider failure marks the existing placeholder node rather than hiding the error.
- Downstream dependency check: `bundle://proof/shared/transcripts/browser-right-click-comfyui.md` proves the generic completion path works from the live project structure canvas.

