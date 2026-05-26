# SB10 Semantic Invariants

## Invariant SB10-INV-001

- Invariant ID: SB10-INV-001
- Source raw note: RN06.
- Expected behavior: Recovery router persists deterministic executable next actions and escalates repeated no-progress recovery.
- Disallowed shallow implementation: Prompt-only changes, source-assertion-only proof, fixture-only branching, text-only heuristics, or tests that avoid the production runtime path.
- Failing-first test: bundle://proof/SB10/transcripts/failing-first.txt
- Passing test: bundle://proof/SB10/transcripts/passing.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRecoveryRouter.cs
- Production assertions: bundle://proof/SB10/manifest.md records the exact source assertions, tests, anti-stub audit, and changed-file hashes for this invariant.
- Red-team negative case: bundle://proof/SB10/transcripts/failing-first.txt
- Downstream dependency check: SB11 and SB13 consume recovery routing state.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB10 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRecoveryRouter.cs | bundle://proof/SB10/manifest.md | bundle://proof/SB10/transcripts/passing.txt | bundle://proof/SB10/transcripts/failing-first.txt |
