# SB07 Proof Manifest

## Status

- Subbundle: `SB07 - Natural Professor Capture And Anchor Semantics`
- Status: `Completed`
- Owned requirements: `R-09`, `R-10`, `R-16`
- Raw notes: natural professor learning must capture Q&A teaching, short corrections, explicit professor captures, examples/counterexamples, and scope corrections; ordinary curator captures must not become active professor anchors; default recall must hide active professor direct quote memories.
- Semantic invariant contract: `bundle://proof/SB07/semantic-invariants.md`

## Changed File Hashes

Complete after-change SHA-256 values are recorded in `bundle://proof/SB07/transcripts/changed-file-hashes.txt`.

Primary after-change SHA-256 values:

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs`
  - SHA-256: `E0CBA0E51920021DF4971E86769F195111C40E2F594F9EF40571DCDBDB825F0A`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs`
  - SHA-256: `915A0A4E2C985E1565CD88E80EFEED098BBF63C6C859393AAD366125FEF5C240`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs`
  - SHA-256: `EFF16ED431F22C81CE0053134B68665130F6EACCB79F414CDAF65D60E98EA96F`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedEntities.cs`
  - SHA-256: `76E5809E1B511B932470E9221BCEA8DBD791D321CFB7A6EFAB2F75DE3071988C`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallDataLoading.cs`
  - SHA-256: `397EBAD503B7D85273FE58721AF974780240C4956D2937D71AEA30A9A2022F24`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs`
  - SHA-256: `A82CA3264F20A6BB84867444EA54F6F7EF4CCF4CE45CA7FB41C3B665E3818155`
- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs`
  - SHA-256: `C325EE672FD219C6487435B378E6D9B04BA489BA027C320C63652BAB389164A1`
- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryRecallOrchestratorTests.cs`
  - SHA-256: `E9494C9B00763B74C762AE0E697DDC961AD03683131F7D654E244C0D29DA10F0`
- `bundle://proof/SB07/semantic-invariants.md`
  - SHA-256: `6DAB4A92509C01A5CDDDCB2B178F3B01A9275FFCDAC8E080DBC105473CF62FA6`

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`
- Passing transcript: `bundle://proof/SB07/transcripts/passing-semantic-tests.txt`
- Regression transcript: `bundle://proof/SB07/transcripts/regression-tests.txt`
- Source assertions transcript: `bundle://proof/SB07/transcripts/source-assertions.txt`
- No-migration proof transcript: `bundle://proof/SB07/transcripts/no-migration-proof.txt`
- Anti-stub audit transcript: `bundle://proof/SB07/transcripts/anti-stub-audit.txt`
- Hash transcript: `bundle://proof/SB07/transcripts/changed-file-hashes.txt`
- Prepared validator transcript: `bundle://proof/SB07/transcripts/prepared-validator-after-sb07.txt`

## Tests And Invariants

- Test name: `SemanticInvariant_CuratorCaptureNaturalProfessorQuestionAnswerAndShortCorrectionCreateAnchors`
- Test name: `CuratorCapture_NewKnowledgeAppliesTrustedMemoryWithoutReview`
- Test name: `CuratorCapture_NaturalProfessorGuidanceCreatesStructuredTemporaryAnchor`
- Test name: `CuratorCapture_ExplicitProfessorExamplesAndCounterexamplesCreateAnchor`
- Test name: `ProfessorAnchor_ActiveAnchorSourceMovesDreamCandidateToComparisonReview`
- Test name: `ProfessorAnchor_AssimilatesAndFadesOnlyAfterDerivedMemoryExists`
- Test name: `ProfessorAnchor_AssimilationRequiresMasteryEvidenceBeyondIndependentSupport`
- Test name: `ProfessorAnchor_DirectCaptureMemoryCannotAssimilateItsOwnAnchor`
- Test name: `ProfessorAnchor_RejectsDescendantOnlyAggregateSupport`
- Test name: `ProfessorAnchor_FadeDemotesDirectCaptureMemory`
- Test name: `ProfessorAnchor_ScanAssimilatesAndFadesIntegratedMasteryEvidence`
- Test name: `ReferenceResolver_ExpandsFadedProfessorAnchorLineage`
- Test name: `RecallAsync_ExcludesActiveProfessorAnchorMemoryByDefault`

Invariant IDs covered by transcripts:

- `SB07-NATURAL-PROFESSOR-01`
- `SB07-EXPLICIT-ANCHOR-02`
- `SB07-RECALL-ANCHOR-VISIBILITY-03`

## Source Assertions

`bundle://proof/SB07/transcripts/source-assertions.txt` proves natural professor extraction now uses previous Q&A teaching context, explicit capture kind hints, source-utterance resolution, claim extraction, target-scope extraction, misconception extraction, examples/counterexamples, and ordinary-capture `NotProfessorAnchor` state. It also proves recall data loading excludes active/comparing professor anchor memories by default and only includes them when `includeProfessorAnchors=true` is present.

## Red-Team Negative Proof

`bundle://proof/SB07/transcripts/passing-semantic-tests.txt` proves an ordinary trusted `NewKnowledge` capture applies without review but remains `NotProfessorAnchor`, while explicit example/counterexample professor teaching becomes `Active`. The same transcript proves default recall excludes an active professor direct quote memory even when a stable memory with the same task terms is present.

## Browser And Host Proof

Browser validation: N/A. SB07 changes backend professor-anchor extraction, persistence defaults, dream validation checks, recall filtering, and unit tests only; no UI routes, components, host startup behavior, or browser-visible behavior changed.

## Persistence And Migration Proof

`bundle://proof/SB07/transcripts/no-migration-proof.txt` proves no EF DbContext, entity configuration, model snapshot, SQLite migration, or PostgreSQL migration files changed. The new `NotProfessorAnchor` enum member and entity initializer use the existing persisted `AnchorState` column.

## Downstream Dependency Check

`bundle://proof/SB07/transcripts/regression-tests.txt` reruns professor-anchor lifecycle, assimilation, fading, dream-validation review routing, faded-lineage reference resolution, and default recall exclusion. `bundle://proof/SB07/transcripts/prepared-validator-after-sb07.txt` proves the bundle remains valid for prepared-stage progression after SB07 closure. SB08 can now depend on explicit active professor anchors and non-professor captures being separated before event-backed mastery logic is tightened.
