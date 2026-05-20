# SB02 Semantic Invariants

## Invariant SB02-FAILING-CORPUS-01

- Invariant ID: `SB02-FAILING-CORPUS-01`
- Source raw note: Remaining cognitive-memory gaps must be represented by failing-first semantic tests before production code changes.
- Expected behavior: The targeted `SemanticInvariant_*` test corpus covers cross-project clustering, approximate candidate discovery, cluster-key coverage, unrelated claim separation, structured synthesis, deep entailment negatives, natural professor capture, event-backed mastery, and recall line-level lineage.
- Disallowed shallow implementation: Adding broad object-count tests, tests that pass against current shallow behavior, or tests not tied to named downstream invariants.
- Failing-first test: `SemanticInvariant_*` suite in `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`.
- Passing test: `SemanticInvariant_NoProductionCognitiveMemoryCodeChangedInSB02` in `bundle://proof/SB02/transcripts/production-diff-proof.txt` proves SB02 stayed tests-only; feature passing transcripts are owned by SB03-SB09.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs`.
- Production assertions: `bundle://proof/SB02/transcripts/test-source-assertions.txt` proves the targeted tests exist and are named for downstream invariants.
- Red-team negative case: The failing-first transcript shows the current implementation fails all 14 targeted cases for the intended behavioral reasons.
- Downstream dependency check: SB03-SB09 semantic invariant contracts cite these tests and must not close until their targeted passing transcripts exist.

## Invariant SB02-NO-PRODUCTION-02

- Invariant ID: `SB02-NO-PRODUCTION-02`
- Source raw note: SB02 must write tests only and must not change production cognitive-memory code.
- Expected behavior: `src/CanDoItAll.Modules.CognitiveMemory` has no git diff after SB02 test additions.
- Disallowed shallow implementation: Editing production code before failing-first tests are captured, or hiding production changes in the SB02 phase.
- Failing-first test: `SemanticInvariant_*` suite fails in `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt` before production implementation.
- Passing test: `SemanticInvariant_NoProductionCognitiveMemoryCodeChangedInSB02` in `bundle://proof/SB02/transcripts/production-diff-proof.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs`.
- Production assertions: `bundle://proof/SB02/transcripts/production-diff-proof.txt` shows an empty production cognitive-memory diff.
- Red-team negative case: Any production cognitive-memory file listed by the diff command would fail the SB02 closure gate.
- Downstream dependency check: SB03 may now begin production changes using the failing tests as implementation gates.
