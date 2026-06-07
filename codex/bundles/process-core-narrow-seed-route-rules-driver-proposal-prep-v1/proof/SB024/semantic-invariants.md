# SB024 Semantic Invariants

- Invariant ID: `SB024-DRIVER-DOCS-ONLY`
- Source raw note: Prepare future drivers safely without shipping production driver APIs prematurely.
- Expected behavior: Driver concepts remain proposal/docs and test guardrails only.
- Disallowed shallow implementation: Introducing `IProcessDriverPack`, registries, DI selection, manager commands, or execution-capable helper drivers.
- Failing-first test: N/A process/no production behavior; production source is scanned for forbidden driver tokens.
- Passing test: bundle://proof/common/transcripts/unit-architecture.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- Production assertions: No production driver API or runtime selector was introduced.
- Red-team negative case: bundle://proof/common/transcripts/production-driver-token-scan.txt rejects forbidden production driver tokens.
- Downstream dependency check: bundle://proof/common/transcripts/build-solution.txt proves no downstream project depends on driver APIs.
