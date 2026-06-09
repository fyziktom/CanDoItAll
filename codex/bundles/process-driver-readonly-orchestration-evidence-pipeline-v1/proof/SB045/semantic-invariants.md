# SB045 Semantic Invariants

## Invariant SB045-PACKAGE-SAMPLES-MATCH-CURRENT-SOURCE
- Invariant ID: `SB045-PACKAGE-SAMPLES-MATCH-CURRENT-SOURCE`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: Package README samples describe the current verifier/request/gateway API shape and avoid stale invocation forms.
- Disallowed shallow implementation: Prose-only examples, reflection-free text checks, or samples that imply static methods or generic dispatch not present in source.
- Failing-first test: The stale observation aggregation README sample used `ProcessDriverObservationAggregator.Aggregate(request)`; P15 corrected it and source scans now reject that stale call.
- Passing test: bundle://proof/SB045/transcripts/focused-p15-readme-sample-tests.txt and bundle://proof/SB045/transcripts/full-unit-p15.txt
- Changed source files: repo://src/CanDoItAll.Processes.Drivers.ObservationAggregation/README.md, repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/README.md, and repo://tests/CanDoItAll.Tests.Unit/ProcessDriverPackageReadmeSamplesTests.cs
- Production assertions: README tests check real verifier/request `Verify` methods and the gateway `VerifyBatch(ProcessDriverVerificationBatchRequest)` method.
- Red-team negative case: Source scans reject stale static aggregation sample text and executable IO/network/service-registration sample code.
- Downstream dependency check: Release gates can cite source-backed package docs rather than prose-only examples.

## Invariant SB045-PROCESS-ADAPTER-MIGRATION-DOCS-TRACK-ORCHESTRATOR
- Invariant ID: `SB045-PROCESS-ADAPTER-MIGRATION-DOCS-TRACK-ORCHESTRATOR`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: The Processes module README documents the current process read-only batch orchestration path, payload builder methods, and focused read-only adapters.
- Disallowed shallow implementation: Documentation that says "use the orchestrator" without naming `ProcessReadOnlyVerificationBatchOrchestrator.Verify(ProcessReadOnlyVerificationBatchPayload)`, current payload builders, or current adapters.
- Failing-first test: No deliberate P15 production compile/test failure was produced; the README/source guard fails if the migration section drifts from the current orchestrator source or payload builder method names.
- Passing test: bundle://proof/SB045/transcripts/focused-p15-readme-sample-tests.txt and bundle://proof/SB045/transcripts/p15-source-scans.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/README.md and repo://tests/CanDoItAll.Tests.Unit/ProcessDriverPackageReadmeSamplesTests.cs
- Production assertions: The docs name the exact batch orchestrator, batch payload, five payload builder methods, and five focused read-only adapters.
- Red-team negative case: Source scans reject runtime-host approval claims, runtime host implementation hooks, Process Core reverse dependency, stubs, and UI/media drift.
- Downstream dependency check: SB046-SB054 can proceed from docs that match the current read-only orchestration code path.
