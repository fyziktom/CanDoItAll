# SB036 Semantic Invariants

## Invariant SB036-PROCESS-MODULE-CORE-CONSUMER-MAP-EXACT
- Invariant ID: `SB036-PROCESS-MODULE-CORE-CONSUMER-MAP-EXACT`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: The process module has an explicit, source-backed map of files allowed to consume `CanDoItAll.Processes.Core`; the exact source-derived set matches the architecture map and test allow-list.
- Disallowed shallow implementation: Leaving a stale marker file in the allow-list, documenting a map without source-derived test enforcement, or allowing arbitrary dispatch files to import Core descriptors.
- Failing-first test: No deliberate P12 production compile/test failure was produced; the exact-set assertion would fail if any unlisted file imports Core or if a stale listed file starts importing Core again.
- Passing test: bundle://proof/SB036/transcripts/focused-p12-core-boundary-unit-tests.txt and bundle://proof/SB036/transcripts/p12-source-scans.txt
- Changed source files: repo://codex/bundles/process-driver-readonly-orchestration-evidence-pipeline-v1/architecture/05-process-module-core-descriptor-consumer-map.md and repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- Production assertions: `ProcessDomainEvidenceReadOnlyAdapters.cs` remains marker-only; approved Core consumers stay at process-module application edges and read-only verification adapters.
- Red-team negative case: Source scans reject stale marker Core/driver consumption and the architecture test rejects unlisted Core consumers.
- Downstream dependency check: SB037 may start from an exact process-module Core consumer map rather than a report-only allow-list.

## Invariant SB036-CORE-DRIVER-REVERSE-DEPENDENCY-AND-GLOBAL-USING-DRIFT-DENIED
- Invariant ID: `SB036-CORE-DRIVER-REVERSE-DEPENDENCY-AND-GLOBAL-USING-DRIFT-DENIED`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: Core remains driver-free, and production code cannot hide Process Core or driver consumption behind global usings or MSBuild global using declarations.
- Disallowed shallow implementation: Checking only `.csproj` references, ignoring source namespace usage, allowing `global using CanDoItAll.Processes.Core`, or relying on a manual scan that is not enforced by tests.
- Failing-first test: No deliberate P12 production compile/test failure was produced; the boundary unit test would fail on Core-to-driver references or production Core/driver global-using drift.
- Passing test: bundle://proof/SB036/transcripts/focused-p12-core-boundary-unit-tests.txt and bundle://proof/SB036/transcripts/p12-source-scans.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- Production assertions: `CanDoItAll.Processes.Core` has no `CanDoItAll.Processes.Drivers` references in project or source; production source has no Process Core/driver global using declarations.
- Red-team negative case: Source scans reject Core reverse dependency, global using drift, runtime host/DI, file/network/storage/workspace, object/dynamic dispatch, direct verifier construction, stubs, and UI/media drift.
- Downstream dependency check: P13 can add shared harness proof without weakening Core/driver boundary assumptions.

## Invariant SB036-PROCESS-DRIVER-CONSUMER-ALLOWLIST-EXACT
- Invariant ID: `SB036-PROCESS-DRIVER-CONSUMER-ALLOWLIST-EXACT`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: Process-module driver namespace consumers are exact-set guarded; the retained marker file is not approved as a driver consumer.
- Disallowed shallow implementation: Allowing stale driver consumer entries, checking only for unapproved additions, or letting direct verifier construction reappear in the process module.
- Failing-first test: No deliberate P12 production compile/test failure was produced; the integration exact-set assertion would fail if a stale entry remains or if a new unlisted process-module file imports driver namespaces.
- Passing test: bundle://proof/SB036/transcripts/focused-p12-driver-allowlist-integration-test.txt, bundle://proof/SB036/transcripts/full-unit-p12.txt, and bundle://proof/SB036/transcripts/p12-source-scans.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeEvidenceVerificationReadOnlyAdapterTests.cs and repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs
- Production assertions: Process read-only adapters continue using the typed verification gateway; `ProcessDomainEvidenceReadOnlyAdapters.cs` is not a driver consumer.
- Red-team negative case: Source scans reject direct process-module verifier construction and runtime host/DI drift.
- Downstream dependency check: SB037 can rely on exact process-module driver/Core consumer ownership while adding reusable harness coverage.
