# SB006 Semantic Invariants

## Invariant SB006-INV-001
- Invariant ID: `SB006-INV-001`
- Source raw note: `Prepare next phases toward complete stable Process Core`.
- Expected behavior: Core public API growth is intentional, reviewable, and guarded by an executable snapshot before downstream diagnostics and driver-readiness work depend on it.
- Disallowed shallow implementation: add prose-only API inventory without a failing guard for new public Core types or members.
- Failing-first test: N/A for process/non-production proof because this phase adds docs/test guardrails only and changes no production behavior.
- Passing test: `bundle://proof/SB006/transcripts/architecture-api-guard-tests-rerun.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`; `bundle://architecture/04-core-public-api-inventory.md`.
- Production assertions: `bundle://proof/SB006/transcripts/core-forbidden-token-scan.txt` proves Core remains free of forbidden side-effect dependencies and driver tokens.
- Red-team negative case: the snapshot guard rejects unapproved public Core API additions; `bundle://proof/SB006/transcripts/source-assertions.txt` cites the guard and inventory.
- Downstream dependency check: SB007-SB009 may proceed because the Core API surface now has a human-readable inventory and an executable compatibility guard.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| N/A | No production signal, persisted state, durable record, or domain event is introduced. | N/A | N/A | `bundle://proof/SB006/transcripts/anti-stub-audit.txt` |
