# SB06 Semantic Invariants

## Invariants

- Invariant ID: SB06-INV-001
- Source raw note: RN06 - Harden project-structure run folder projection and avoid noisy artifact subtree nodes.
- Expected behavior: Project-structure process-run projection uses one typed Workbench policy to expose current-run managed artifact roots and generated or external-delivery output roots without mirroring per-artifact receipt internals.
- Disallowed shallow implementation: Prompt-only wording, docs-only behavior, UI-only hiding of noisy nodes, leaving path selection buried in the projection contributor, accepting wrong-run paths, accepting traversal paths, or hardcoding Blazor/Tetris/project/run/user paths in production code.
- Failing-first test: bundle://proof/SB06/transcripts/failing-first.txt proves the old private projection helper is gone and adversarial wrong-run, dated receipt, and traversal paths are rejected by the policy test.
- Passing test: bundle://proof/SB06/transcripts/passing.txt proves the direct projection policy matrix and the Workbench structure surface test pass.
- Changed source files: repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessRunFolderProjectionPolicy.cs; repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAssemblyService.cs; repo://src/CanDoItAll.Modules.Workbench/README.md; repo://src/CanDoItAll.Modules.Processes/README.md; repo://tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs.
- Production assertions: `ProjectStructureAssemblyService` groups process-run output nodes only after `ProjectStructureProcessRunFolderProjectionPolicy.Resolve` returns a projectable root and projection kind; raw `external-target/...` aliases remain Processes grounding metadata until persisted as managed output evidence.
- Red-team negative case: A dated tool receipt folder without the current run id, a different run id inside `process-runs`, or a traversal path cannot create a projected child folder under the selected process run.
- Downstream dependency check: SB09 and SB13 can rely on navigable run/product folder projection after the noisy-folder negative proof; SB18 final red-team must keep this policy in its release-readiness checks.

## Production Behavior Artifact Matrix

| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Typed run folder projection policy | repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessRunFolderProjectionPolicy.cs `Resolve`; source proof bundle://proof/SB06/transcripts/source-assertions.txt | repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAssemblyService.cs output-folder grouping | bundle://proof/SB06/transcripts/passing.txt proves current-run managed roots, artifact roots, product roots, and direct output files map to stable folder roots | bundle://proof/SB06/transcripts/failing-first.txt proves wrong-run, dated receipt, and traversal paths are rejected |
| Project-structure run output folder nodes | repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAssemblyService.cs process projection contributor | Workbench project-structure surface and downstream template/observability flows | bundle://proof/SB06/transcripts/passing.txt proves the surface still exposes exactly the useful managed run/product/artifact folders | bundle://proof/SB06/transcripts/passing.txt proves noisy receipt and unrelated product folders are absent |
| Projection ownership docs | repo://src/CanDoItAll.Modules.Workbench/README.md and repo://src/CanDoItAll.Modules.Processes/README.md | SB09, SB12, SB13, and SB18 documentation and governance work | bundle://proof/SB06/transcripts/source-assertions.txt proves docs name the policy and external-delivery managed output boundary | bundle://proof/SB06/transcripts/anti-stub-audit.txt proves the docs are not placeholder closure |

## Validation

- Failing-first/adversarial proof: bundle://proof/SB06/transcripts/failing-first.txt.
- Passing proof: bundle://proof/SB06/transcripts/passing.txt.
- Source assertions: bundle://proof/SB06/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB06/transcripts/anti-stub-audit.txt.
- Changed-file hashes: bundle://proof/SB06/transcripts/changed-file-hashes.txt.
