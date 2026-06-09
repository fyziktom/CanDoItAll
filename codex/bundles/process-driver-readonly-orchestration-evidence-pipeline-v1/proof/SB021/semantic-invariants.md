# SB021 Semantic Invariants

## Invariant SB021-AGGREGATE-SNAPSHOT-PARITY-IMMUTABILITY
- Invariant ID: `SB021-AGGREGATE-SNAPSHOT-PARITY-IMMUTABILITY`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: Process batch verification maps the gateway-backed aggregate into a process-level snapshot that preserves process identity, caller context, response counts, accepted/denied counts, diagnostics counts, mutation-free flags, lane summaries, evidence references, redaction, and contract version.
- Disallowed shallow implementation: Returning the lower-level adapter observation directly, dropping lane summaries, exposing mutable lists, losing process/run identity, introducing runtime host/DI/file/storage/network behavior, or adding object/dynamic dispatch.
- Failing-first test: bundle://proof/SB021/transcripts/build-aggregate-snapshot.txt
- Passing test: bundle://proof/SB021/transcripts/build-aggregate-snapshot-fixed.txt, bundle://proof/SB021/transcripts/focused-p07-integration-tests.txt, bundle://proof/SB021/transcripts/focused-p07-aggregation-unit-tests.txt, and bundle://proof/SB021/transcripts/full-unit-p07.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationAggregateObservation.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationBatchOrchestrator.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeEvidenceVerificationReadOnlyAdapterTests.cs, repo://tests/CanDoItAll.Tests.Unit/ProcessDriverObservationAggregationTests.cs
- Production assertions: The mapper creates a dedicated process aggregate envelope; batch observation no longer exposes `ProcessDriverObservationAggregationReadOnlyObservation` as the aggregate property; aggregate lists are read-only snapshots.
- Red-team negative case: bundle://proof/SB021/transcripts/p07-source-scans.txt rejects lower-level aggregate leakage from the batch observation, runtime/DI/manager tokens, file/storage/network APIs, object/dynamic dispatch, Core reverse dependencies, UI/media drift, and stubs.
- Downstream dependency check: P08 may start because aggregate process snapshots now preserve lane-summary parity and immutability.
