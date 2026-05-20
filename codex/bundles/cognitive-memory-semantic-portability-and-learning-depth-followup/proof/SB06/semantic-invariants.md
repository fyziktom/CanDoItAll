# SB06 Semantic Invariants

## Invariant SB06-DEEP-ENTAILMENT-01

- Invariant ID: `SB06-DEEP-ENTAILMENT-01`
- Source raw note: Entailment and contradiction validation must go beyond lexical overlap.
- Expected behavior: Dream validation rejects unsupported reversals involving numbers, temporal order, actor/action roles, conditional predicates, optional versus required language, and scope boundaries.
- Disallowed shallow implementation: Passing a claim because enough lexical tokens overlap with source text.
- Failing-first test: `SemanticInvariant_DreamEntailmentRejectsNumericTemporalActorConditionalAndScopeReversals` in `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`.
- Passing test: `bundle://proof/SB06/transcripts/passing-semantic-tests.txt` and `bundle://proof/SB06/transcripts/regression-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateConfidenceCalibrator.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityAlgorithmOptions.cs`, and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`.
- Production assertions: `bundle://proof/SB06/transcripts/source-assertions.txt` proves semantic profiles, numeric/temporal/role/condition/scope blockers, claim-level issue reasons, and semantic aggregate-confidence calibration are present.
- Red-team negative case: `Run migration after traffic restoration` must not be entailed by a source requiring migration before traffic restoration.
- Downstream dependency check: SB08 assimilation must not use aggregates that passed through shallow entailment.
