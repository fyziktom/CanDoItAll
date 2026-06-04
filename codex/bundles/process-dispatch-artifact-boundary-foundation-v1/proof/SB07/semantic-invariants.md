# SB07 Semantic Invariants

- Invariant ID: SB07-INV-001
- Source raw note: Do not rush Process Core extraction; decompose dispatcher services gradually through abstractions and smaller isolation bundles; enforce gates; avoid small/medium/mobile proof.
- Expected behavior: The execution artifact projection path now creates a pure projection plan before storage placement and DB recording, while preserving external keys, lineage, trust status, and review text.
- Disallowed shallow implementation: A planner class unused by production code would pass file-existence checks but leave dispatcher projection behavior unchanged.
- Failing-first test: N/A for process/no behavior-change staged refactor; adversarial negative proof is cited in bundle://proof/SB11/transcripts/process-artifact-regression-slice.txt.
- Passing test: bundle://proof/SB10/transcripts/focused-dispatcher-artifact-helper-tests.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs and hash proof in bundle://proof/SB12/hashes/changed-file-hashes.txt.
- Production assertions: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs plus bundle://proof/SB12/source-assertions/final-source-scans.txt.
- Red-team negative case: bundle://proof/SB11/transcripts/process-artifact-regression-slice.txt rejects placeholder inventory, unused planner, weak guardrails, stranded validation service, or premature Core cutline depending on this subbundle.
- Downstream dependency check: bundle://proof/SB11/transcripts/process-artifact-regression-slice.txt and bundle://proof/SB12/transcripts/full-solution-build.txt.