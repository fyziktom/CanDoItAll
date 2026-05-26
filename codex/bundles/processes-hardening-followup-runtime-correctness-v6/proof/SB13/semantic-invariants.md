# SB13 Semantic Invariants

## Invariant SB13-INV-001

- Invariant ID: SB13-INV-001
- Source raw note: RN06 and final operator observability.
- Expected behavior: Runtime invariant diagnostics expose actionable recovery and contract health in service, read-model, and component paths.
- Disallowed shallow implementation: Prompt-only changes, source-assertion-only proof, fixture-only branching, text-only heuristics, or tests that avoid the production runtime path.
- Failing-first test: bundle://proof/SB13/transcripts/failing-first.txt
- Passing test: bundle://proof/SB13/transcripts/passing.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeInvariantAuditor.cs
- Production assertions: bundle://proof/SB13/manifest.md records the exact source assertions, tests, anti-stub audit, and changed-file hashes for this invariant.
- Red-team negative case: bundle://proof/SB13/transcripts/failing-first.txt
- Downstream dependency check: SB14 uses diagnostics to close final red-team proof.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB13 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeInvariantAuditor.cs | bundle://proof/SB13/manifest.md | bundle://proof/SB13/transcripts/passing.txt | bundle://proof/SB13/transcripts/failing-first.txt |
