# SB04 Semantic Invariants

- Invariant ID: `SB04-INV-001`
- Source raw note: Continue small dispatcher isolation steps while keeping Process Core and production driver APIs out of scope.
- Expected behavior: Architecture guard verifies local dispatch boundary constraints before downstream helper extraction.
- Disallowed shallow implementation: A source-only edit that omits no-core, no-driver, no-UI, or prohibited viewport proof checks is rejected.
- Failing-first test: N/A process guard; no production behavior moved in this gate.
- Passing test: Focused unit guard plus source assertions prove the boundary locks.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: The guard checks the bundle cutline and source scans verify no prohibited boundary appeared.
- Red-team negative case: Source assertions reject Process Core creation, driver API tokens, UI file changes, and prohibited viewport proof.
- Downstream dependency check: SB05-SB20 proceeded after this gate passed.

