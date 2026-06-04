# SB02 Semantic Invariants

- Invariant ID: SB02-INV-001
- Source raw note: Do not rush Process Core extraction; decompose dispatcher services gradually through abstractions and smaller isolation bundles; enforce gates; avoid small/medium/mobile proof.
- Expected behavior: Artifact/projection/validation methods are classified before production movement, and the inventory maps pure helpers separately from storage and finalization paths.
- Disallowed shallow implementation: A placeholder inventory with method names only would miss side effects and allow unsafe migration order.
- Failing-first test: N/A for process/no behavior-change staged refactor; adversarial negative proof is cited in bundle://proof/SB12/source-assertions/final-source-scans.txt.
- Passing test: bundle://proof/SB11/transcripts/process-artifact-regression-slice.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs and hash proof in bundle://proof/SB12/hashes/changed-file-hashes.txt.
- Production assertions: repo://codex/bundles/process-dispatch-artifact-boundary-foundation-v1/inventories/02-artifact-method-classification-template.md plus bundle://proof/SB12/source-assertions/final-source-scans.txt.
- Red-team negative case: bundle://proof/SB12/source-assertions/final-source-scans.txt rejects placeholder inventory, unused planner, weak guardrails, stranded validation service, or premature Core cutline depending on this subbundle.
- Downstream dependency check: bundle://proof/SB11/transcripts/process-artifact-regression-slice.txt and bundle://proof/SB12/transcripts/full-solution-build.txt.