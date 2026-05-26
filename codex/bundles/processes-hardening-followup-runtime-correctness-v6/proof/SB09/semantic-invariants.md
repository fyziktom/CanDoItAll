# SB09 Semantic Invariants

## Invariant SB09-INV-001

- Invariant ID: SB09-INV-001
- Source raw note: RN07.
- Expected behavior: Workflow and subprocess artifact projection uses explicit persisted output mappings and blocks ambiguous projection.
- Disallowed shallow implementation: Prompt-only changes, source-assertion-only proof, fixture-only branching, text-only heuristics, or tests that avoid the production runtime path.
- Failing-first test: bundle://proof/SB09/transcripts/failing-first.txt
- Passing test: bundle://proof/SB09/transcripts/passing.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/WorkflowSubprocessArtifactMapper.cs
- Production assertions: bundle://proof/SB09/manifest.md records the exact source assertions, tests, anti-stub audit, and changed-file hashes for this invariant.
- Red-team negative case: bundle://proof/SB09/transcripts/failing-first.txt
- Downstream dependency check: SB11 extracts the mapper boundary and SB14 red-team closure depends on it.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB09 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/WorkflowSubprocessArtifactMapper.cs | bundle://proof/SB09/manifest.md | bundle://proof/SB09/transcripts/passing.txt | bundle://proof/SB09/transcripts/failing-first.txt |
