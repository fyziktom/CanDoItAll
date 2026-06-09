# SB010 Semantic Invariants

## Invariant SB010-TYPED-BATCH-ENVELOPE
- Invariant ID: `SB010-TYPED-BATCH-ENVELOPE`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: The verification gateway package has explicit typed request and response envelopes for transcript, runtime evidence, artifact evidence, Office evidence, and business-analysis verification lanes.
- Disallowed shallow implementation: A batch envelope that stores `object`, uses `dynamic`, relies on lane selectors or dictionaries for runtime dispatch, exposes mutable arrays, or is prose-only without focused tests.
- Passing test: bundle://proof/SB012/transcripts/build-typed-batch-gateway-explicit-lanes.txt, bundle://proof/SB012/transcripts/focused-p04-gateway-tests-explicit-lanes.txt, and bundle://proof/SB012/transcripts/full-unit-p04-explicit-lanes-rerun.txt
- Changed source files: repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationBatch.cs, repo://tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationGatewayTests.cs
- Production assertions: `ProcessDriverVerificationBatchRequest` and `ProcessDriverVerificationBatchResponse` copy supplied lane lists into read-only snapshots and expose no runtime host surface.
- Red-team negative case: bundle://proof/SB012/transcripts/p04-source-scans-explicit-lanes.txt rejects object/dynamic dispatch, side-effect APIs, Core reverse dependencies, UI/media drift, and stubs.
- Downstream dependency check: SB011 may route these typed envelopes through the gateway without introducing runtime selectors.
