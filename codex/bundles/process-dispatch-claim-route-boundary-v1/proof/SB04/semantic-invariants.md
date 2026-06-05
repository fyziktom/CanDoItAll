# SB04 Semantic Invariants

- Invariant ID: `SB04_INV_001`
- Source raw note: RN-002 and RN-004.
- Expected behavior: Gate A blocks Process Core, production driver API drift, MAF back-dependencies, stale inventory, and prohibited viewport proof before production refactoring starts.
- Disallowed shallow implementation: A guardrail that only checks project existence but does not prove live route/concurrency inventory or production-source drift.
- Failing-first test: `bundle://proof/SB04/transcripts/sb04-failing-first-live-inventory-gate.txt`.
- Passing test: `bundle://proof/SB04/transcripts/sb04-new-architecture-tests.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- Production assertions: `bundle://proof/SB04/source-assertions/gate-a-architecture-guardrails.md`.
- Red-team negative case: `bundle://proof/SB04/transcripts/sb04-production-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB05 may start only because Gate A now proves the live inventory and architecture drift guardrails.

- Invariant ID: `SB04_INV_002`
- Source raw note: RN-003.
- Expected behavior: Gate A rejects placeholder or stale inventories so later helper extraction uses live source facts.
- Disallowed shallow implementation: Keeping seeded inventory prose such as `Codex must fill this` or `Initial candidate methods` while marking Gate A complete.
- Failing-first test: `bundle://proof/SB04/transcripts/sb04-failing-first-live-inventory-gate.txt`.
- Passing test: `bundle://proof/SB04/transcripts/sb04-new-architecture-tests.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- Production assertions: `bundle://proof/SB04/source-assertions/gate-a-architecture-guardrails.md`.
- Red-team negative case: `bundle://proof/SB04/transcripts/sb04-production-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB05/SB06 helper extraction must preserve the SB02/SB03 live inventory cutlines.
