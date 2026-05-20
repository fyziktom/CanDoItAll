# SB07 Semantic Invariants

## Invariant SB07-NATURAL-PROFESSOR-01

- Invariant ID: `SB07-NATURAL-PROFESSOR-01`
- Source raw note: Professor capture must work for natural teaching conversations, including short corrections and Q&A.
- Expected behavior: A previous Q&A teaching turn plus a short correction creates a structured temporary professor anchor with target scope, corrected misconception, claims, capture type, confidence, and source utterances.
- Disallowed shallow implementation: Requiring explicit `remember` phrasing, requiring long user messages, or rejecting short corrections before checking conversation context.
- Failing-first test: `SemanticInvariant_CuratorCaptureNaturalProfessorQuestionAnswerAndShortCorrectionCreateAnchors` in `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`.
- Passing test: `bundle://proof/SB07/transcripts/passing-semantic-tests.txt` and `bundle://proof/SB07/transcripts/regression-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedEntities.cs`, and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs`.
- Production assertions: `bundle://proof/SB07/transcripts/source-assertions.txt` proves Q&A teaching context, source-utterance resolution, claim extraction, scope extraction, misconception extraction, explicit capture hints, and non-professor anchor state assignment are present.
- Red-team negative case: A short correction like `No: release-owner gate, not health.` must not be ignored when previous turns provide the teaching context.
- Downstream dependency check: SB08 lifecycle proof depends on correctly captured professor anchors.

## Invariant SB07-EXPLICIT-ANCHOR-02

- Invariant ID: `SB07-EXPLICIT-ANCHOR-02`
- Source raw note: Explicit professor captures must preserve examples, counterexamples, and scope corrections instead of treating every trusted curator capture as an active professor anchor.
- Expected behavior: Explicit `NewKnowledge` can produce an active professor anchor when it contains professor teaching semantics, examples, counterexamples, or scope rules; ordinary trusted captures remain `NotProfessorAnchor`.
- Disallowed shallow implementation: Treating every explicit capture as `Active`, or only matching exact fixture words while losing example/counterexample utterances.
- Failing-first test: `SemanticInvariant_CuratorCaptureNaturalProfessorQuestionAnswerAndShortCorrectionCreateAnchors` in `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt` provides the SB07 non-zero failing baseline; `CuratorCapture_NewKnowledgeAppliesTrustedMemoryWithoutReview` and `CuratorCapture_ExplicitProfessorExamplesAndCounterexamplesCreateAnchor` are the SB07 positive/negative closure tests.
- Passing test: `bundle://proof/SB07/transcripts/passing-semantic-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedEntities.cs`, and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs`.
- Production assertions: `bundle://proof/SB07/transcripts/source-assertions.txt` proves explicit capture kind is passed into extraction, professor intent checks examples/counterexamples and source-of-truth style teaching, and persisted ordinary captures use `NotProfessorAnchor`.
- Red-team negative case: An ordinary trusted `NewKnowledge` capture must apply without review but must not become an active professor anchor.
- Downstream dependency check: SB08 assimilation/fading must only scan real professor anchors.

## Invariant SB07-RECALL-ANCHOR-VISIBILITY-03

- Invariant ID: `SB07-RECALL-ANCHOR-VISIBILITY-03`
- Source raw note: Default recall must exclude active professor direct quote memories unless explicitly requested for references or review.
- Expected behavior: Recall data loading filters memories linked to active or comparing professor captures by default and includes them only when request metadata explicitly asks for professor anchors.
- Disallowed shallow implementation: Hiding only one recall path or leaving lexical/source-reference loading able to select active anchor memories by default.
- Failing-first test: SB02 established SB07 as a failing semantic area in `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`; `RecallAsync_ExcludesActiveProfessorAnchorMemoryByDefault` is the SB07 closure test.
- Passing test: `bundle://proof/SB07/transcripts/passing-semantic-tests.txt` and `bundle://proof/SB07/transcripts/regression-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallDataLoading.cs` and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryRecallOrchestratorTests.cs`.
- Production assertions: `bundle://proof/SB07/transcripts/source-assertions.txt` proves both candidate-loading paths call `ExcludeActiveProfessorAnchorRecords`, active/comparing captures are filtered, and `includeProfessorAnchors=true` is the explicit inclusion gate.
- Red-team negative case: A temporary active professor direct quote memory must not appear in normal recall when a stable memory with the same task terms exists.
- Downstream dependency check: SB09 recall brief lineage can depend on default task-facing recall staying free of temporary professor anchors.
