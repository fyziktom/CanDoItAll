# SB10 Semantic Invariants

- Invariant ID: SB10-INV-001
- Source raw note: Do not rush Process Core extraction; decompose dispatcher services gradually through abstractions and smaller isolation bundles; enforce gates; avoid small/medium/mobile proof.
- Expected behavior: Selected producer-mode and durable-path validation rules are centralized in ProcessArtifactEvidenceValidationRules and consumed through existing validation wrappers.
- Disallowed shallow implementation: A validation service that is tested directly but not consumed by the dispatcher would not protect required artifact satisfaction.
- Failing-first test: N/A for process/no behavior-change staged refactor; adversarial negative proof is cited in bundle://proof/SB11/transcripts/process-artifact-regression-slice.txt.
- Passing test: bundle://proof/SB10/transcripts/focused-dispatcher-artifact-helper-tests.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs and hash proof in bundle://proof/SB12/hashes/changed-file-hashes.txt.
- Production assertions: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactEvidenceValidationRules.cs plus bundle://proof/SB12/source-assertions/final-source-scans.txt.
- Red-team negative case: bundle://proof/SB11/transcripts/process-artifact-regression-slice.txt rejects placeholder inventory, unused planner, weak guardrails, stranded validation service, or premature Core cutline depending on this subbundle.
- Downstream dependency check: bundle://proof/SB11/transcripts/process-artifact-regression-slice.txt and bundle://proof/SB12/transcripts/full-solution-build.txt.