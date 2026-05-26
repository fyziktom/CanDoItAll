# SB06 Semantic Invariants

## Invariant SB06-INV-001

- Invariant ID: SB06-INV-001
- Source raw note: RN04.
- Expected behavior: Script side-effect manifests and post-execution audits block product-root mutations that regex inspection misses.
- Disallowed shallow implementation: Prompt-only changes, source-assertion-only proof, fixture-only branching, text-only heuristics, or tests that avoid the production runtime path.
- Failing-first test: bundle://proof/SB06/transcripts/failing-first.txt
- Passing test: bundle://proof/SB06/transcripts/passing.txt
- Changed source files: repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ProcessScriptSideEffectAnalyzer.cs
- Production assertions: bundle://proof/SB06/manifest.md records the exact source assertions, tests, anti-stub audit, and changed-file hashes for this invariant.
- Red-team negative case: bundle://proof/SB06/transcripts/failing-first.txt
- Downstream dependency check: SB07 extracts the policy analyzer and authorizer boundary.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB06 verified runtime behavior | repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ProcessScriptSideEffectAnalyzer.cs | bundle://proof/SB06/manifest.md | bundle://proof/SB06/transcripts/passing.txt | bundle://proof/SB06/transcripts/failing-first.txt |
