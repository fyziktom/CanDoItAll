# SB08 Semantic Invariants

## Invariant SB08-INV-001

- Invariant ID: SB08-INV-001
- Source raw note: RN08 and RN02.
- Expected behavior: Artifact validation reads managed storage content instead of trusting workspace path metadata alone.
- Disallowed shallow implementation: Prompt-only changes, source-assertion-only proof, fixture-only branching, text-only heuristics, or tests that avoid the production runtime path.
- Failing-first test: bundle://proof/SB08/transcripts/failing-first.txt
- Passing test: bundle://proof/SB08/transcripts/passing.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs
- Production assertions: bundle://proof/SB08/manifest.md records the exact source assertions, tests, anti-stub audit, and changed-file hashes for this invariant.
- Red-team negative case: bundle://proof/SB08/transcripts/failing-first.txt
- Downstream dependency check: SB09 and SB14 depend on storage-backed artifact validation.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB08 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs | bundle://proof/SB08/manifest.md | bundle://proof/SB08/transcripts/passing.txt | bundle://proof/SB08/transcripts/failing-first.txt |
