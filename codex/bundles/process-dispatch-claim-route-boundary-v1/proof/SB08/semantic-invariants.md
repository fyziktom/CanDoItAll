# SB08 Semantic Invariants

- Invariant ID: `SB08_INV_001`
- Source raw note: RN-001, RN-002, RN-003, and RN-004.
- Expected behavior: Gate B proves the concurrency helper exists, keeps pure rule semantics out of side-effect adapters, preserves execution-client polling in the dispatcher, and covers blocking, recoverable, stale, competing, fresh recovery, and busy exception semantics.
- Disallowed shallow implementation: A helper file that exists but contains side effects, omits competing/stale semantics, or lets async execution-client calls drift into the helper.
- Failing-first test: `bundle://proof/SB08/transcripts/sb08-failing-first-head-concurrency-gate.txt`.
- Passing test: `bundle://proof/SB08/transcripts/sb08-architecture-gate-b-tests.txt` and `bundle://proof/SB08/transcripts/sb08-concurrency-parity-integration-tests.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- Production assertions: `bundle://proof/SB08/source-assertions/gate-b-concurrency-parity.md`.
- Red-team negative case: `bundle://proof/SB08/transcripts/sb08-failing-first-head-concurrency-gate.txt` and `bundle://proof/SB08/transcripts/sb08-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB09-SB12 can rely on the helper boundary and wrapper contract without introducing Process Core or driver APIs.

- Invariant ID: `SB08_INV_002`
- Source raw note: RN-002 and RN-003.
- Expected behavior: Gate B rejects shallow wrapper migration by requiring wrapper delegation count, removing duplicated private blocking/recovery/trigger selection logic, and preserving the stale wrapper only as a compatibility facade for partial-class callers.
- Disallowed shallow implementation: Keeping duplicated `.Where(executionRun => IsBlockingAutomationExecutionRun...)` selection logic in `Concurrency.cs` while also adding a helper.
- Failing-first test: `bundle://proof/SB08/transcripts/sb08-failing-first-head-concurrency-gate.txt`.
- Passing test: `bundle://proof/SB08/transcripts/sb08-architecture-gate-b-tests.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- Production assertions: `bundle://proof/SB08/source-assertions/gate-b-concurrency-parity.md`.
- Red-team negative case: `bundle://proof/SB08/transcripts/sb08-failing-first-head-concurrency-gate.txt` and `bundle://proof/SB08/transcripts/sb08-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB09 claim/heartbeat work must keep concurrency selection pure and avoid reintroducing duplicated dispatcher branches.
