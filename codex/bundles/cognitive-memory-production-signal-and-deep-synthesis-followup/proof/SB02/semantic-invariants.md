# SB02 Semantic Invariants

## Invariant SB02-ACCEPTED-USE-01

- Invariant ID: `SB02-ACCEPTED-USE-01`
- Source raw note: Production accepted-use evidence must be emitted by real recall/workflow acceptance paths and assimilation must run automatically.
- Expected behavior: Tests require a production accepted-use emitter contract, production `ProfessorAnchorAcceptedUse` signal emission, and scheduled automation assimilation wiring.
- Disallowed shallow implementation: Keeping only enum/evaluator/test-seeded `ProfessorAnchorAcceptedUse` rows.
- Failing-first test: `SemanticInvariant_AcceptedUseSignalHasProductionEmitterAndScheduledAssimilation` in `bundle://proof/SB02/transcripts/failing-first.txt`.
- Passing test: `SB02-NO-PRODUCTION-DIFF-07` in `bundle://proof/SB02/transcripts/passing.txt`; production pass is owned by SB03.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` with hash `D790886E2DD65ADF2C7D4B75FB63435EFC2D59F4EE088FFA901EFBA94D536B27`.
- Production assertions: The test asserts the future producer, consumer signal assignment, recall-trace source kind, and scheduled `ScanAssimilationAsync` lifecycle path.
- Red-team negative case: `bundle://proof/SB02/transcripts/failing-first.txt` proves current code has no production emitter and no scheduled scan.
- Downstream dependency check: SB03 must make this test pass without manually seeding accepted-use signals in positive feature tests.

## Invariant SB02-COMPARISON-REVIEW-02

- Invariant ID: `SB02-COMPARISON-REVIEW-02`
- Source raw note: Professor anchors must not remain stranded in `Comparing` after reviewable dream validation states.
- Expected behavior: Tests require an explicit comparison-review resolution API and lifecycle audit signal writes.
- Disallowed shallow implementation: Auto-clearing `Comparing` without actor, reason, outcome, or audit signal.
- Failing-first test: `SemanticInvariant_ProfessorComparisonReviewResolutionIsExplicitAndAudited` in `bundle://proof/SB02/transcripts/failing-first.txt`.
- Passing test: `SB02-NO-PRODUCTION-DIFF-07` in `bundle://proof/SB02/transcripts/passing.txt`; production pass is owned by SB04.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` with hash `D790886E2DD65ADF2C7D4B75FB63435EFC2D59F4EE088FFA901EFBA94D536B27`.
- Production assertions: The test requires `CognitiveMemoryProfessorComparisonReviewOutcome`, `ResolveComparisonAsync`, `ProfessorAnchorLifecycleTransition`, and `Comparing` handling.
- Red-team negative case: `bundle://proof/SB02/transcripts/failing-first.txt` proves the current review service lacks the explicit resolution surface.
- Downstream dependency check: SB04 must make this test pass while preserving direct anchor hiding rules.

## Invariant SB02-MULTILINGUAL-CAPTURE-03

- Invariant ID: `SB02-MULTILINGUAL-CAPTURE-03`
- Source raw note: Natural Czech and Q&A professor teaching with diacritics must create structured temporary anchors.
- Expected behavior: Czech diacritics and example/counterexample teaching produce an active professor anchor while preserving original diacritic text.
- Disallowed shallow implementation: Matching only ASCII Czech phrases or explicit `zapamatuj si` instructions.
- Failing-first test: `SemanticInvariant_CuratorCaptureCzechDiacriticsAndNaturalScopeCreatesProfessorAnchor` in `bundle://proof/SB02/transcripts/failing-first.txt`.
- Passing test: `SB02-NO-PRODUCTION-DIFF-07` in `bundle://proof/SB02/transcripts/passing.txt`; production pass is owned by SB05.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` with hash `D790886E2DD65ADF2C7D4B75FB63435EFC2D59F4EE088FFA901EFBA94D536B27`.
- Production assertions: The test exercises the real curator conversation service and expects an active anchor.
- Red-team negative case: `bundle://proof/SB02/transcripts/failing-first.txt` proves the current implementation drops this natural Czech teaching flow.
- Downstream dependency check: SB05 must make this test pass without stripping stored diacritics.

## Invariant SB02-DREAM-PROVENANCE-04

- Invariant ID: `SB02-DREAM-PROVENANCE-04`
- Source raw note: Dream synthesis must store useful domain knowledge and claim-specific provenance, not meta-evidence text or record-wide source maps.
- Expected behavior: Aggregate memory text excludes `source-backed observation` boilerplate and source-map generation has a claim-specific boundary.
- Disallowed shallow implementation: `Conclusion: ... supported by N source-backed observation(s)` and `SelectMany(unit => unit.SourceMaps)` assigned to every claim.
- Failing-first test: `DreamRun_CanonicalAggregateMemoryContainsDomainKnowledgeWithoutDiagnosticBoilerplate` and `SemanticInvariant_DreamConsolidationCreatesClaimSpecificSourceMaps` in `bundle://proof/SB02/transcripts/failing-first.txt`.
- Passing test: `SB02-NO-PRODUCTION-DIFF-07` in `bundle://proof/SB02/transcripts/passing.txt`; production pass is owned by SB06.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` with hash `122C91531BF0286CE3184611E53D81AA89D923600E83AF12746C407E13690D4D`.
- Production assertions: The tests cover dream run output and source-level claim-specific source-map boundaries.
- Red-team negative case: `bundle://proof/SB02/transcripts/failing-first.txt` proves current dreams still expose source-backed observation template text and lack a claim-specific map boundary.
- Downstream dependency check: SB06 must make these tests pass with production synthesis and provenance changes.

## Invariant SB02-SEMANTIC-CLUSTERING-05

- Invariant ID: `SB02-SEMANTIC-CLUSTERING-05`
- Source raw note: Approximate clustering must use embedding/ranker-backed semantic providers when available and expose deterministic continuation diagnostics.
- Expected behavior: Tests require an approximate candidate provider boundary with embedding and continuation cursor support.
- Disallowed shallow implementation: Relying only on lexical signal overlap and silently exhausting pair budgets.
- Failing-first test: `SemanticInvariant_ClusterDiscoveryHasEmbeddingBackedApproximateCandidateProvider` in `bundle://proof/SB02/transcripts/failing-first.txt`.
- Passing test: `SB02-NO-PRODUCTION-DIFF-07` in `bundle://proof/SB02/transcripts/passing.txt`; production pass is owned by SB07.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` with hash `122C91531BF0286CE3184611E53D81AA89D923600E83AF12746C407E13690D4D`.
- Production assertions: The test requires `ICognitiveMemoryApproximateClusterCandidateProvider`, embedding usage, continuation cursor diagnostics, and approximate pair metrics.
- Red-team negative case: `bundle://proof/SB02/transcripts/failing-first.txt` proves current quality source lacks the provider boundary.
- Downstream dependency check: SB07 must make this test pass without unbounded all-pairs comparison.

## Invariant SB02-RECALL-LINEAGE-06

- Invariant ID: `SB02-RECALL-LINEAGE-06`
- Source raw note: Recall synthesis must use the real user query/intent and preserve exact statement-to-claim-to-source lineage.
- Expected behavior: Tests require `CognitiveMemoryRecallSynthesisRequest` to carry query/intent and `CognitiveMemoryRecallSynthesisService` to pass `request.QueryText` instead of context title/summary.
- Disallowed shallow implementation: Composing recall briefs from context pack title/summary and attaching broad lineage.
- Failing-first test: `SemanticInvariant_RecallSynthesisRequestCarriesRealQueryIntentAndLineage` in `bundle://proof/SB02/transcripts/failing-first.txt`.
- Passing test: `SB02-NO-PRODUCTION-DIFF-07` in `bundle://proof/SB02/transcripts/passing.txt`; production pass is owned by SB08.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` with hash `122C91531BF0286CE3184611E53D81AA89D923600E83AF12746C407E13690D4D`.
- Production assertions: The test asserts query/intent contract fields and rejects the old title/summary query construction.
- Red-team negative case: `bundle://proof/SB02/transcripts/failing-first.txt` proves the current synthesis request lacks real query/intent fields.
- Downstream dependency check: SB08 must make this test pass and add behavior-level reference-lineage proof.

## Invariant SB02-NO-PRODUCTION-DIFF-07

- Invariant ID: `SB02-NO-PRODUCTION-DIFF-07`
- Source raw note: SB02 must protect later production work without changing production behavior itself.
- Expected behavior: `repo://src/CanDoItAll.Modules.CognitiveMemory` has no production diff after SB02.
- Disallowed shallow implementation: Sneaking production fixes into the failing-first corpus phase.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first.txt` records the failing corpus.
- Passing test: `git diff -- src\CanDoItAll.Modules.CognitiveMemory` in `bundle://proof/SB02/transcripts/passing.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`.
- Production assertions: The no-production-diff command produced no output and exit code 0.
- Red-team negative case: Production source remains untouched in SB02, so later subbundles must implement real behavior.
- Downstream dependency check: SB03-SB08 can cite the SB02 failing-first transcript as their red baseline.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ProfessorAnchorAcceptedUse` future producer | `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` asserts future producer contract | `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` asserts production signal assignment | `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` asserts scheduled scan wiring | `bundle://proof/SB02/transcripts/failing-first.txt` proves current implementation fails |
