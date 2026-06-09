# SB039 Semantic Invariants

## Invariant SB039-REUSABLE-MULTI-DOMAIN-HARNESS-USES-PRODUCTION-ORCHESTRATOR
- Invariant ID: `SB039-REUSABLE-MULTI-DOMAIN-HARNESS-USES-PRODUCTION-ORCHESTRATOR`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: A reusable integration harness runs the production `ProcessReadOnlyVerificationBatchOrchestrator` across the five current supplied-payload lanes.
- Disallowed shallow implementation: Manual observation seeding, bypassing the production orchestrator, testing only one lane, or asserting only status counts without lane ownership.
- Failing-first test: No deliberate P13 production compile/test failure was produced; the harness assertions fail if any current lane is absent from the aggregate or response count.
- Passing test: bundle://proof/SB039/transcripts/focused-p13-multidomain-harness-integration-tests.txt and bundle://proof/SB039/transcripts/full-unit-p13.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/ProcessReadOnlyVerificationMultiDomainHarness.cs and repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs
- Production assertions: The harness calls `ProcessReadOnlyVerificationBatchOrchestrator.Verify` and verifies the real aggregate observation.
- Red-team negative case: Source scans reject runtime host, DI, direct verifier construction in the process module, object/dynamic dispatch, and side-effect APIs.
- Downstream dependency check: SB040 can start from a reusable harness that proves current multi-domain orchestration rather than only lane-by-lane assertions.

## Invariant SB039-OBSERVATION-PRODUCER-CONSUMER-MATRIX-IS-TYPED
- Invariant ID: `SB039-OBSERVATION-PRODUCER-CONSUMER-MATRIX-IS-TYPED`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: Each current read-only observation record has typed producer and consumer proof: concrete adapter `Type` as producer and `ProcessReadOnlyVerificationBatchOrchestrator` as consumer.
- Disallowed shallow implementation: Stringly typed producer names, prose-only producer/consumer proof, or omitting aggregate lane summary checks.
- Failing-first test: No deliberate P13 production compile/test failure was produced; the typed matrix assertions fail if producer/consumer ownership is changed without updating the harness.
- Passing test: bundle://proof/SB039/transcripts/focused-p13-multidomain-harness-integration-tests.txt and bundle://proof/SB039/transcripts/p13-source-scans.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/ProcessReadOnlyVerificationMultiDomainHarness.cs
- Production assertions: Transcript, runtime facts, artifact evidence, Office evidence, and business-analysis observations each have one producer-owned lane response and one aggregate summary.
- Red-team negative case: The source scan requires `ProcessReadOnlyVerificationLaneProducerConsumer`, concrete `typeof(...)` producer entries, all current lane enum values, and no stubs.
- Downstream dependency check: Docs and runtime-host denial phases can cite typed producer/consumer proof instead of prose-only observation ownership.
