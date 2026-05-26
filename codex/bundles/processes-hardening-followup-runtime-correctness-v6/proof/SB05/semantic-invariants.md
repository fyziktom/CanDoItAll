# SB05 Semantic Invariants

## Invariant SB05-INV-001

- Invariant ID: SB05-INV-001
- Source raw note: RN06.
- Expected behavior: Typed block causes distinguish own-output artifact recovery from upstream-input materialization waits.
- Disallowed shallow implementation: Prompt-only changes, source-assertion-only proof, fixture-only branching, text-only heuristics, or tests that avoid the production runtime path.
- Failing-first test: bundle://proof/SB05/transcripts/failing-first.txt
- Passing test: bundle://proof/SB05/transcripts/passing.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunBlockState.cs
- Production assertions: bundle://proof/SB05/manifest.md records the exact source assertions, tests, anti-stub audit, and changed-file hashes for this invariant.
- Red-team negative case: bundle://proof/SB05/transcripts/failing-first.txt
- Downstream dependency check: SB10, SB11, and SB13 consume typed block cause state.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB05 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunBlockState.cs | bundle://proof/SB05/manifest.md | bundle://proof/SB05/transcripts/passing.txt | bundle://proof/SB05/transcripts/failing-first.txt |
