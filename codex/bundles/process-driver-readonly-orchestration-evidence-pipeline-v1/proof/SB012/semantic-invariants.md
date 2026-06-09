# SB012 Semantic Invariants

## Invariant SB012-TYPED-BATCH-NO-GENERIC-DISPATCH
- Invariant ID: `SB012-TYPED-BATCH-NO-GENERIC-DISPATCH`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: The verification gateway accepts a typed batch request, executes each supplied lane through explicit typed methods, returns typed read-only response lists plus `AllResponses`, and optionally aggregates only those verification responses.
- Disallowed shallow implementation: A batch API that uses `Verify(object)`, `dynamic`, `Func<object>`, a lane selector, a registry, DI/service lookup, a manager command, or a private generic helper that obscures the typed route.
- Failing-first test: bundle://proof/SB012/transcripts/full-unit-p04.txt
- Passing test: bundle://proof/SB012/transcripts/build-typed-batch-gateway-explicit-lanes.txt, bundle://proof/SB012/transcripts/focused-p04-gateway-tests-explicit-lanes.txt, and bundle://proof/SB012/transcripts/full-unit-p04-explicit-lanes-rerun.txt
- Changed source files: repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationBatch.cs, repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationGateway.cs, repo://tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationGatewayTests.cs, repo://tests/CanDoItAll.Tests.Unit/ProcessDriverObservationAggregationTests.cs
- Production assertions: The batch request has five strongly typed request lists; the response has five strongly typed response lists, `AllResponses`, and optional aggregate; gateway routing calls `VerifyTranscriptBatch`, `VerifyRuntimeEvidenceBatch`, `VerifyArtifactEvidenceBatch`, `VerifyOfficeEvidenceBatch`, and `VerifyBusinessAnalysisBatch`.
- Red-team negative case: bundle://proof/SB012/transcripts/p04-source-scans-explicit-lanes.txt rejects generic helper dispatch, runtime object dispatch, lane selector dispatch, side-effect APIs, Core reverse dependencies, UI/media drift, and stubs.
- Downstream dependency check: P05 may start because process orchestration can depend on a typed gateway batch API without introducing runtime execution, selectors, or hidden mutation.
