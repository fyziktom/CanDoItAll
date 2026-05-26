# SB07 Semantic Invariants

## Invariant SB07-INV-001

- Invariant ID: SB07-INV-001
- Source raw note: RN02, RN04, and refactoring checkpoint B.
- Expected behavior: Tool policy and artifact validation are extracted into explicit services with regression coverage.
- Disallowed shallow implementation: Prompt-only changes, source-assertion-only proof, fixture-only branching, text-only heuristics, or tests that avoid the production runtime path.
- Failing-first test: bundle://proof/SB07/transcripts/failing-first.txt
- Passing test: bundle://proof/SB07/transcripts/passing.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs
- Production assertions: bundle://proof/SB07/manifest.md records the exact source assertions, tests, anti-stub audit, and changed-file hashes for this invariant.
- Red-team negative case: bundle://proof/SB07/transcripts/failing-first.txt
- Downstream dependency check: SB08 consumes the artifact validator boundary.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB07 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs | bundle://proof/SB07/manifest.md | bundle://proof/SB07/transcripts/passing.txt | bundle://proof/SB07/transcripts/failing-first.txt |
