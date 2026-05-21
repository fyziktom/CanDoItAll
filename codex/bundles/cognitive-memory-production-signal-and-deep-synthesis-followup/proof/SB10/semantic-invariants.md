# SB10 Semantic Invariants

## Invariant SB10-END-TO-END-LIFECYCLE-01

- Invariant ID: `SB10-END-TO-END-LIFECYCLE-01`
- Source raw note: Prove the corrected cognitive-memory loop end to end with production pathways.
- Expected behavior: Natural Czech professor teaching creates an anchor, comparison review resolves it, accepted-use emission publishes production signals, automatic scan assimilates/fades the anchor, and reference resolution returns exact curator evidence.
- Disallowed shallow implementation: Passing only isolated helper tests or manually seeding accepted-use signals.
- Failing-first test: SB02 red baseline proved each component gap before production changes.
- Passing test: `ProfessorLearningLifecycle_CzechCaptureReviewAcceptedUseAssimilatesAndResolvesReferences`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` plus production files listed in the manifest.
- Production assertions: The test calls real curator, review, accepted-use emitter, anchor service scan, and reference resolver services.
- Red-team negative case: Direct anchor memory is not accepted as derived use in SB03, and final E2E avoids manual accepted-use seeding.
- Downstream dependency check: Final lifecycle proof closes SB03-SB08 dependency chain.

## Invariant SB10-NO-MANUAL-ACCEPTED-USE-SEED-02

- Invariant ID: `SB10-NO-MANUAL-ACCEPTED-USE-SEED-02`
- Source raw note: Final proof must not manually seed accepted-use signals.
- Expected behavior: Accepted-use signals in the final E2E are created by `CognitiveMemoryProfessorAcceptedUseSignalEmitter.EmitAsync`.
- Disallowed shallow implementation: Calling `SeedAcceptedProfessorUseEventsAsync` in the final E2E.
- Failing-first test: SB02 red baseline identified manual signal seeding as insufficient.
- Passing test: `ProfessorLearningLifecycle_CzechCaptureReviewAcceptedUseAssimilatesAndResolvesReferences`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs`.
- Production assertions: The final test calls `EmitAsync` twice with distinct accepted outcome ids and asserts two persisted accepted-use signals.
- Red-team negative case: Anti-stub transcript audits the final test body.
- Downstream dependency check: Completed validator proof can rely on producer-backed accepted use.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Full professor learning lifecycle | `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` E2E producer calls | Production services listed in `bundle://proof/SB10/transcripts/source-assertions.txt` | E2E assertion reaches faded anchor and resolved reference | `bundle://proof/SB10/transcripts/anti-stub.txt` |
