# SB04 Semantic Invariants

- Invariant ID: `SB04-GATE-A-ARCHITECTURE`
- Source raw note: Do not rush Process Core and do not create driver APIs prematurely.
- Expected behavior: Architecture tests prove helper locality, dispatcher delegation, no Process Core, no production driver API, and no prohibited viewport proof paths.
- Disallowed shallow implementation: Adding helper files that compile but depend on storage, process core, production driver contracts, or forbidden proof paths.
- Failing-first test: N/A - process architecture gate proof; no standalone production behavior was added in SB04.
- Passing test: `bundle://proof/SB04/transcripts/gate-a-architecture.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: Production helper boundaries are validated before broader dispatcher migration.
- Red-team negative case: Source scans and architecture tests reject broad dependency movement and proof-policy violations.
- Downstream dependency check: SB05-SB13 migrations depend on this gate passing.
