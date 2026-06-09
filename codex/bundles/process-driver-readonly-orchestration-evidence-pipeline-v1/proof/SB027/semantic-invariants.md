# SB027 Semantic Invariants

## Invariant SB027-ARTIFACT-PROJECTION-VALIDATION-SATISFACTION-PARITY
- Invariant ID: `SB027-ARTIFACT-PROJECTION-VALIDATION-SATISFACTION-PARITY`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: Supplied artifact projection, validation, expected-artifact, and artifact-record descriptors flow through process batch orchestration into the artifact evidence driver, preserve mutation-free read-only behavior, and surface projection/order/lineage/trust/sensitivity/satisfaction diagnostics in both the artifact observation and aggregate lane summary.
- Disallowed shallow implementation: Testing only the artifact alpha verifier, omitting the process batch orchestrator, omitting Core expected-artifact and artifact-record snapshots, using ambiguous process-module snapshot types, dropping aggregate diagnostic categories, introducing runtime host/DI/file/storage/network behavior, or adding object/dynamic dispatch.
- Failing-first test: No deliberate P09 production compile/test failure was produced.
- Passing test: bundle://proof/SB027/transcripts/build-artifact-validation-rehearsal.txt, bundle://proof/SB027/transcripts/focused-p09-artifact-unit-tests.txt, bundle://proof/SB027/transcripts/focused-p09-artifact-integration-tests.txt, bundle://proof/SB027/transcripts/full-unit-p09.txt, and bundle://proof/SB027/transcripts/p09-source-scans.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs
- Production assertions: Existing artifact driver rules still call Core matcher and satisfaction rules; process payload builder still carries expected artifacts and artifact records into the artifact read-only payload.
- Red-team negative case: The P09 process integration test supplies duplicate/out-of-order projection descriptors, missing lineage, trust/sensitivity mismatch, and artifact-kind mismatch, then verifies all expected diagnostic categories and secret-free audit/diagnostic text.
- Downstream dependency check: P10 may start because artifact projection/validation and Core satisfaction descriptors now have process-batch integration proof without runtime or mutation-capable infrastructure.
