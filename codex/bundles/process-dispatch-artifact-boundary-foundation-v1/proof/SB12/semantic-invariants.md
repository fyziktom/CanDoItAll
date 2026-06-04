# SB12 Semantic Invariants

- Invariant ID: SB12-INV-001
- Source raw note: Do not rush Process Core extraction; decompose dispatcher services gradually through abstractions and smaller isolation bundles; enforce gates; avoid small/medium/mobile proof.
- Expected behavior: Final closure keeps the next cutline narrow: additional full source projection migration is follow-up work, not a Process Core extraction.
- Disallowed shallow implementation: A final report that treats this as Core readiness would contradict the original request and skip remaining projection-source migration.
- Failing-first test: N/A for process/no behavior-change staged refactor; adversarial negative proof is cited in bundle://proof/SB12/transcripts/anti-stub-audit.txt.
- Passing test: bundle://proof/SB12/transcripts/full-solution-build.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs and hash proof in bundle://proof/SB12/hashes/changed-file-hashes.txt.
- Production assertions: repo://codex/bundles/process-dispatch-artifact-boundary-foundation-v1/reviews/01-execution-report.md plus bundle://proof/SB12/source-assertions/final-source-scans.txt.
- Red-team negative case: bundle://proof/SB12/transcripts/anti-stub-audit.txt rejects placeholder inventory, unused planner, weak guardrails, stranded validation service, or premature Core cutline depending on this subbundle.
- Downstream dependency check: bundle://proof/SB11/transcripts/process-artifact-regression-slice.txt and bundle://proof/SB12/transcripts/full-solution-build.txt.