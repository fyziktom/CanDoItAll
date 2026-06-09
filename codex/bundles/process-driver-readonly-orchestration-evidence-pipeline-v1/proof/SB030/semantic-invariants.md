# SB030 Semantic Invariants

## Invariant SB030-OFFICE-BUSINESS-BATCH-SUPPLIED-EVIDENCE-PARITY
- Invariant ID: `SB030-OFFICE-BUSINESS-BATCH-SUPPLIED-EVIDENCE-PARITY`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: Supplied Office evidence metadata/text and supplied business-analysis deliverables/evidence flow through process batch orchestration into typed read-only observations and aggregate lane summaries without external sources, storage, workspace writes, or process mutation.
- Disallowed shallow implementation: Testing only alpha verifier classes, bypassing `ProcessReadOnlyVerificationBatchOrchestrator`, hand-seeding aggregate summaries, dropping evidence hash assertions, introducing runtime host/DI/file/storage/network behavior, or adding object/dynamic dispatch.
- Failing-first test: No deliberate P10 production compile/test failure was produced.
- Passing test: bundle://proof/SB030/transcripts/build-office-business-rehearsal.txt, bundle://proof/SB030/transcripts/focused-p10-office-business-integration-tests.txt, bundle://proof/SB030/transcripts/focused-p10-process-domain-integration-tests.txt, bundle://proof/SB030/transcripts/focused-p10-office-business-unit-tests.txt, bundle://proof/SB030/transcripts/full-unit-p10.txt, and bundle://proof/SB030/transcripts/p10-source-scans.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs
- Production assertions: Existing process payload builders still create `OfficeEvidencePayload` and `BusinessAnalysisPayload` supplied-content envelopes with `OfficeReadonlyArtifact` and `BusinessReadonlyArtifact` evidence references; the batch orchestrator still invokes typed Office and business adapters.
- Red-team negative case: See `SB030-OFFICE-BUSINESS-NO-EXTERNAL-MUTATION-CLOSURE`.
- Downstream dependency check: P11 may start because Office/business process orchestration now has supplied-evidence and aggregate-lane proof without runtime or mutation-capable infrastructure.

## Invariant SB030-OFFICE-BUSINESS-NO-EXTERNAL-MUTATION-CLOSURE
- Invariant ID: `SB030-OFFICE-BUSINESS-NO-EXTERNAL-MUTATION-CLOSURE`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: Office and business process-batch verification denies external-call attempts before analysis, denies business record mutation, keeps all responses mutation-free, and does not leak raw adversarial supplied text in diagnostics or audit summaries.
- Disallowed shallow implementation: Checking only a denial reason, omitting audit fact scope flags, omitting aggregate denied counts, allowing `NoIssueDetected` on denied responses, or failing to prove raw text remains absent from diagnostics and audit facts.
- Failing-first test: No deliberate P10 production compile/test failure was produced.
- Passing test: bundle://proof/SB030/transcripts/focused-p10-office-business-integration-tests.txt and bundle://proof/SB030/transcripts/focused-p10-process-domain-integration-tests.txt
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs
- Production assertions: Existing verifier policies produce `ExternalCallDenied`, `MutationDenied`, and `MutationAttemptDenied`; process observations and aggregate summaries preserve denial counts, diagnostic categories, and mutation-free flags.
- Red-team negative case: `Process_readonly_verification_batch_orchestrator_SB030_INV_002_denies_office_and_business_external_calls_without_mutation` supplies `fixture-secret reviewer@example.invalid` while requesting `CallOfficeGraph` and `MutateBusinessRecord`, then asserts denial, mutation-free audit facts, aggregate denied counts, and no raw leakage.
- Downstream dependency check: SB031 can start from a closed no-external-call Office/business process rehearsal gate.
