# SB11 Semantic Invariants

## Invariant SB11-INV-001

- Invariant ID: SB11-INV-001
- Source raw note: Refactoring checkpoint C.
- Expected behavior: Block classification, recovery health, and workflow/subprocess mapping are extracted into named services.
- Disallowed shallow implementation: Prompt-only changes, source-assertion-only proof, fixture-only branching, text-only heuristics, or tests that avoid the production runtime path.
- Failing-first test: bundle://proof/SB11/transcripts/failing-first.txt
- Passing test: bundle://proof/SB11/transcripts/passing.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessHealthInvariantAuditor.cs
- Production assertions: bundle://proof/SB11/manifest.md records the exact source assertions, tests, anti-stub audit, and changed-file hashes for this invariant.
- Red-team negative case: bundle://proof/SB11/transcripts/failing-first.txt
- Downstream dependency check: SB12 and SB13 consume the extracted services.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB11 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessHealthInvariantAuditor.cs | bundle://proof/SB11/manifest.md | bundle://proof/SB11/transcripts/passing.txt | bundle://proof/SB11/transcripts/failing-first.txt |
