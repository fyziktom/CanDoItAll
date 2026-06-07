# SB003 Semantic Invariants

- Invariant ID: `SB003-NARROW-CORE-ROUTE-RULES`
- Source raw note: Do not rush Process Core unless justified; preserve behavior while preparing future drivers safely.
- Expected behavior: The baseline remains green with only narrow route read-model and rule code allowed in Core.
- Disallowed shallow implementation: Creating broad Core, driver APIs, registries, DI selectors, or hiding unstable dispatch behavior behind silent fallback code.
- Failing-first test: N/A process/no production behavior; this is an architecture-preserving extraction guarded by negative source scans.
- Passing test: bundle://proof/common/transcripts/unit-architecture.txt and bundle://proof/common/transcripts/integration-dispatch.txt
- Changed source files: repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRoutePlanner.cs and repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- Production assertions: Dispatch behavior stays in the Processes module; Core contains pure route planning, stage order, and eligibility checks only.
- Red-team negative case: bundle://proof/common/transcripts/core-forbidden-scan.txt and bundle://proof/common/transcripts/production-driver-token-scan.txt reject broad Core and driver drift.
- Downstream dependency check: bundle://proof/common/transcripts/build-solution.txt proves downstream projects still compile.
