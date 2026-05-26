# SB04 Semantic Invariants

## Invariant SB04-INV-001

- Invariant ID: SB04-INV-001
- Source raw note: RN01, RN03, and refactoring checkpoint A.
- Expected behavior: Metadata, contract, and grounding logic are extracted behind named testable boundaries.
- Disallowed shallow implementation: Prompt-only changes, source-assertion-only proof, fixture-only branching, text-only heuristics, or tests that avoid the production runtime path.
- Failing-first test: bundle://proof/SB04/transcripts/failing-first.txt
- Passing test: bundle://proof/SB04/transcripts/passing.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.OperationContractResolver.cs
- Production assertions: bundle://proof/SB04/manifest.md records the exact source assertions, tests, anti-stub audit, and changed-file hashes for this invariant.
- Red-team negative case: bundle://proof/SB04/transcripts/failing-first.txt
- Downstream dependency check: SB05 and SB06 consume the extracted metadata and contract boundaries.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB04 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.OperationContractResolver.cs | bundle://proof/SB04/manifest.md | bundle://proof/SB04/transcripts/passing.txt | bundle://proof/SB04/transcripts/failing-first.txt |
