# SB03 Proof Manifest - Failing-first semantic corpus

## Subbundle

- Subbundle: `03-03-failing-first-semantic-corpus`
- Status: `Completed`
- Owned requirements: `R-03`, plus failing-first coverage for `R-05` through `R-17`
- Owned raw note: `Fix remaining cognitive-memory issues`
- Browser/host proof: `N/A - backend tests only`
- Test name: `ClusterPlanner_SplitsBridgeChainsInsteadOfMergingUnrelatedEndpoints`
- Test name: `ClusterPlanner_RoutesContradictionOnlyRelationToReviewCluster`
- Test name: `ClusterPlanner_UsesHighFanoutFallbackForParaphrasedSemanticPair`
- Test name: `DreamRun_IntegratesComplementaryProcedureClaimsIntoSingleAggregateStatement`
- Test name: `DreamRun_ProducesModeSpecificStructuredOutputsBeyondTitlePrefix`
- Test name: `DreamValidation_RejectsNegatedClaimDespiteHighTokenOverlap`
- Test name: `CuratorCapture_NaturalProfessorGuidanceCreatesStructuredTemporaryAnchor`
- Test name: `ProfessorAnchor_AssimilationRequiresMasteryEvidenceBeyondIndependentSupport`
- Test name: `RecallSynthesis_SeparatesConflictingClaimsIntoCaveatStatements`
- Test name: `ReferenceResolver_LimitsAggregateExpansionToRequestedClaimLineage`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryQualityFoundationTests.cs` | `FC78F0CEB8C132F1F5F28DA7038A9FE295C8A296D054854D20A0569A5F6D2185` |
| `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs` | `02DB33DF11647221C94686ED5F7CE742A23FC09D39C33CBEC8CEA49C26E0E0FD` |

## Proof Artifacts

- Failing-first transcript: `proof/SB03/transcripts/failing-first-targeted-tests.txt`
- Anti-stub audit transcript: `proof/SB03/transcripts/production-diff-check.txt`
- Bundle prepared-stage validator transcript: `proof/SB03/transcripts/prepared-validator-after-sb03.txt`
- Passing transcript: `proof/SB10/transcripts/passing-targeted-end-to-end-quality-tests.txt`

## Coverage Map

| Owner | Tests |
|---|---|
| SB04 clustering | `ClusterPlanner_SplitsBridgeChainsInsteadOfMergingUnrelatedEndpoints`; `ClusterPlanner_RoutesContradictionOnlyRelationToReviewCluster`; `ClusterPlanner_UsesHighFanoutFallbackForParaphrasedSemanticPair` |
| SB05 dreaming | `DreamRun_IntegratesComplementaryProcedureClaimsIntoSingleAggregateStatement`; `DreamRun_ProducesModeSpecificStructuredOutputsBeyondTitlePrefix`; `DreamValidation_RejectsNegatedClaimDespiteHighTokenOverlap` |
| SB06 professor capture | `CuratorCapture_NaturalProfessorGuidanceCreatesStructuredTemporaryAnchor` |
| SB07 assimilation/fading | `ProfessorAnchor_AssimilationRequiresMasteryEvidenceBeyondIndependentSupport` |
| SB08 recall/reference | `RecallSynthesis_SeparatesConflictingClaimsIntoCaveatStatements`; `ReferenceResolver_LimitsAggregateExpansionToRequestedClaimLineage` |

## Semantic Adequacy

- Raw note owned: `Fix remaining cognitive-memory issues`.
- Shipped behavior: ten adversarial tests now encode the remaining semantic behavior before production fixes start.
- Source proof: the two test files listed in the hash table contain the new corpus.
- Test proof: `failing-first-targeted-tests.txt` exits non-zero with all ten new tests failing against current production behavior.
- Shallow-pass trap: existing tests could pass representative-copy dreams, union-find bridge clusters, keyword-only professor capture, direct assimilation, title-grouped recall, and broad reference expansion.
- Adversarial negative proof: the failing-first transcript records failures for bridge splitting, contradiction-only review labeling, high-fanout paraphrase fallback, integrated synthesis, mode-specific dream structure, negation entailment, natural professor capture, mastery-gated assimilation, conflict recall, and claim-level lineage.
- Semantic positive proof: deferred to SB04-SB08 by design and closed by the SB10 targeted transcript, which passes the full failing-first corpus without weakening the tests.
- Anti-stub audit: `production-diff-check.txt` proves no cognitive-memory production source changed in SB03.

## Progression Decision

SB03 closure passes as a failing-first corpus. SB04-SB08 must cite these tests and may not weaken or skip them.
