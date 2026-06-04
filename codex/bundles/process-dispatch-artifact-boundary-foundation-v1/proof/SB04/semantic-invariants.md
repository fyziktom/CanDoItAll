# SB04 Semantic Invariants

- Invariant ID: SB04-INV-001
- Source raw note: Do not rush Process Core extraction; decompose dispatcher services gradually through abstractions and smaller isolation bundles; enforce gates; avoid small/medium/mobile proof.
- Expected behavior: Architecture guardrails prove no premature Process Core or driver-pack project, no hidden MAF product dependency, and no prohibited viewport proof path.
- Disallowed shallow implementation: A guardrail that only checks bundle prose would miss actual project files or source references.
- Failing-first test: N/A for process/no behavior-change staged refactor; adversarial negative proof is cited in bundle://proof/SB12/source-assertions/final-source-scans.txt.
- Passing test: bundle://proof/SB04/transcripts/unit-architecture-guardrails.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs and hash proof in bundle://proof/SB12/hashes/changed-file-hashes.txt.
- Production assertions: repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs plus bundle://proof/SB12/source-assertions/final-source-scans.txt.
- Red-team negative case: bundle://proof/SB12/source-assertions/final-source-scans.txt rejects placeholder inventory, unused planner, weak guardrails, stranded validation service, or premature Core cutline depending on this subbundle.
- Downstream dependency check: bundle://proof/SB11/transcripts/process-artifact-regression-slice.txt and bundle://proof/SB12/transcripts/full-solution-build.txt.