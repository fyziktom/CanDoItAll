# SB12 Semantic Invariants

## Invariant SB12-INV-001

- Invariant ID: SB12-INV-001
- Source raw note: RN02, RN04, RN06, RN07, and RN08.
- Expected behavior: Strict/compatibility contract policy is persisted and linter gates enforce required template operation metadata.
- Disallowed shallow implementation: Prompt-only changes, source-assertion-only proof, fixture-only branching, text-only heuristics, or tests that avoid the production runtime path.
- Failing-first test: bundle://proof/SB12/transcripts/failing-first.txt
- Passing test: bundle://proof/SB12/transcripts/passing.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs
- Production assertions: bundle://proof/SB12/manifest.md records the exact source assertions, tests, anti-stub audit, and changed-file hashes for this invariant.
- Red-team negative case: bundle://proof/SB12/transcripts/failing-first.txt
- Downstream dependency check: SB13 and SB14 rely on strict template coverage.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB12 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs | bundle://proof/SB12/manifest.md | bundle://proof/SB12/transcripts/passing.txt | bundle://proof/SB12/transcripts/failing-first.txt |
