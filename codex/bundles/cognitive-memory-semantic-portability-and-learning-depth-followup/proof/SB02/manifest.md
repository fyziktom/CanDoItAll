# SB02 Proof Manifest

## Status

- Subbundle: `SB02 - Failing-first Adversarial Corpus For Remaining Gaps`
- Status: `Completed`
- Owned requirements: `R-03`, `R-04`, `R-05`, `R-06`, `R-07`, `R-08`, `R-09`, `R-11`, `R-13`, `R-16`
- Raw notes: all remaining cognitive-memory gaps must have failing-first semantic tests before production behavior changes.
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`

## Changed File Hashes

Complete before/after file hashes are recorded in `bundle://proof/SB02/transcripts/changed-file-hashes.txt`.

Primary after-change SHA-256 values:

- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`
  - SHA-256: `b52c8d7783db16267cd8721922e5108a95e248f64df46905e11f074bf78feb96`
- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs`
  - SHA-256: `48450dc32a9209e5b2051b0e5ff8bf58c077697aa4a743328c3f76382fbaaac1`
- `bundle://proof/SB02/semantic-invariants.md`
  - SHA-256: `bf84704baef30f801314da08eb71a5011185bf7950e7e389535da1315931c20f`
- `bundle://proof/SB03/semantic-invariants.md`
  - SHA-256: `a6d4078e732376bc020ea75ba306735ce17e4fe4a357df21f02df55192545635`
- `bundle://proof/SB04/semantic-invariants.md`
  - SHA-256: `cc3262a8d1eeea4d6e6ca27be4b7398b7481d133cf21a6453b31ba586a79a0c1`
- `bundle://proof/SB05/semantic-invariants.md`
  - SHA-256: `074e0db1b051834ca1242cdffcf5a49cfd594652dfdd08616156693eaf86da92`
- `bundle://proof/SB06/semantic-invariants.md`
  - SHA-256: `8bbadcddf5c6bc72d8223f0b7cc6a78d8ea187b5cdc81e362e410ddada448e07`
- `bundle://proof/SB07/semantic-invariants.md`
  - SHA-256: `05492c4821dbbc042ecf39e4e32e8c13a81de29c7a94f034f7afe75939cb116c`
- `bundle://proof/SB08/semantic-invariants.md`
  - SHA-256: `773fca7258e96bf276ecea16fb48e27bb23c58541cca1f33e4386fdfecde3402`
- `bundle://proof/SB09/semantic-invariants.md`
  - SHA-256: `3be309928f7425433927552e76096e7909392df8e9ada4d697e71f73328aef3e`

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/production-diff-proof.txt`
- Source assertions transcript: `bundle://proof/SB02/transcripts/test-source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- Hash transcript: `bundle://proof/SB02/transcripts/changed-file-hashes.txt`

## Tests And Invariants

- Test name: `SemanticInvariant_CrossProjectWeeklyFormsOnlyPolicyAllowedCrossProjectClusters`
- Test name: `SemanticInvariant_ApproximateCandidateDiscoveryPairsParaphrasesWithoutExactSharedKeys`
- Test name: `SemanticInvariant_ClusterKeysExcludeSignalsBelowCoverageThreshold`
- Test name: `SemanticInvariant_DreamRunSeparatesUnrelatedClaimsSharingPrimaryClusterKey`
- Test name: `SemanticInvariant_DreamClaimSynthesisProducesStructuredSlots`
- Test name: `SemanticInvariant_DreamEntailmentRejectsNumericTemporalActorConditionalAndScopeReversals`
- Test name: `SemanticInvariant_CuratorCaptureNaturalProfessorQuestionAnswerAndShortCorrectionCreateAnchors`
- Test name: `SemanticInvariant_ProfessorAnchorScanRequiresAcceptedUseEventsInsteadOfSourceMapMentions`
- Test name: `SemanticInvariant_RecallBriefKeepsAggregateClaimLineageAtStatementLineLevel`
- Test name: `SemanticInvariant_NoProductionCognitiveMemoryCodeChangedInSB02`
- Test name: `SemanticInvariant_TestSourceAssertions`
- Test name: `SemanticInvariant_AntiStubAudit`
- Test name: `SemanticInvariant_ChangedFilesHaveSha256Hashes`

Invariant IDs covered by transcripts:

- `SB02-FAILING-CORPUS-01`
- `SB02-NO-PRODUCTION-02`
- `SB03-CROSS-PROJECT-01`
- `SB03-APPROX-CANDIDATES-02`
- `SB04-KEY-COVERAGE-01`
- `SB05-CLAIM-GROUPING-01`
- `SB05-STRUCTURED-SYNTHESIS-02`
- `SB06-DEEP-ENTAILMENT-01`
- `SB07-NATURAL-PROFESSOR-01`
- `SB08-EVENT-MASTERY-01`
- `SB09-RECALL-LINEAGE-01`

## Source Assertions

`bundle://proof/SB02/transcripts/test-source-assertions.txt` proves the targeted test names exist in the changed unit test files. The tests are mapped to downstream semantic invariant contracts in `bundle://proof/SB03/semantic-invariants.md` through `bundle://proof/SB09/semantic-invariants.md`.

## Red-Team Negative Proof

`bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt` shows all 14 targeted semantic cases fail against the current implementation. The failures are behavioral: no cross-project candidate, no approximate candidate pair, low-coverage cluster key exposed, unrelated dream claims merged, synthesis lacks structured slots, lexical entailment accepts reversals, short professor correction is ignored, source-map mentions count as mastery, and recall lineage collapses multiple aggregate claims into one line.

## Browser And Host Proof

Browser validation: N/A. SB02 changed unit tests and proof contracts only; no UI routes, components, host startup behavior, or browser-visible behavior changed.

## Downstream Dependency Check

`bundle://proof/SB02/transcripts/production-diff-proof.txt` proves no `src/CanDoItAll.Modules.CognitiveMemory` production file changed in SB02. SB03-SB09 must now make production changes driven by the initial adversarial corpus and replace each pending passing transcript in the downstream semantic invariant contracts.
