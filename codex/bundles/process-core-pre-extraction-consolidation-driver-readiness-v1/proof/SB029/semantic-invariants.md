# SB029 Semantic Invariants

## Invariants

- Invariant ID: `SB029-INV-001`
- Source raw note: `Add/adjust tests that will guard future Core dependencies but do not create Core.`
- Expected behavior: The active bundle contains a future Core allow/deny list and an architecture guard that rejects Core project creation, production driver tokens, public interface examples, DI examples, and collapsed SB028/SB029 accountability.
- Disallowed shallow implementation: Leaving tests pointed at an older bundle, creating production Core, or documenting production interfaces/registrations as part of the rehearsal.
- Failing-first test: `N/A - documentation/test-only rehearsal; no production behavior change was intended.`
- Passing test: `bundle://proof/SB029/transcripts/core-allow-deny-architecture-test.txt`
- Changed source files: `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/architecture/04-core-candidate-contract-map.md`, `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/architecture/05-future-core-allow-deny-list.md`, `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/analysis/04-static-wrapper-inventory.md`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB029/transcripts/core-allow-deny-source-assertions.txt`
- Red-team negative case: Pointing the guard at `process-core-contract-candidate-driver-readiness-prep-v1`, adding `public interface` examples, or creating `src/CanDoItAll.Processes.Core` fails SB029 proof.
- Downstream dependency check: `SB030` may run Core rehearsal closure because docs and guard are in place.

## Raw Note Closure

- Architecture tests for future Core allow/deny lists: `Solved for SB029 with active-bundle guard and allow/deny docs.`
- No production Core: `Partially solved with guard proof; SB030 owns critical closure.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
