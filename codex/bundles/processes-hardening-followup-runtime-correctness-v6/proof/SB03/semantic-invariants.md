# SB03 Semantic Invariants

## Invariant SB03-INV-001

- Invariant ID: SB03-INV-001
- Source raw note: RN02.
- Expected behavior: Manual/API completion cannot bypass shared completion artifact validation.
- Disallowed shallow implementation: Prompt-only changes, source-assertion-only proof, fixture-only branching, text-only heuristics, or tests that avoid the production runtime path.
- Failing-first test: bundle://proof/SB03/transcripts/failing-first.txt
- Passing test: bundle://proof/SB03/transcripts/passing.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs
- Production assertions: bundle://proof/SB03/manifest.md records the exact source assertions, tests, anti-stub audit, and changed-file hashes for this invariant.
- Red-team negative case: bundle://proof/SB03/transcripts/failing-first.txt
- Downstream dependency check: SB07 and SB08 consume the shared validation boundary.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB03 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs | bundle://proof/SB03/manifest.md | bundle://proof/SB03/transcripts/passing.txt | bundle://proof/SB03/transcripts/failing-first.txt |
