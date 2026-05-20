# SB06 Proof Manifest - Natural professor capture and structured anchors

## Subbundle

- Subbundle: `06-06-natural-professor-capture-and-structured-anchors`
- Status: `Completed`
- Owned requirements: `R-11`, `R-12`
- Owned raw note: `Curator/professor mode must capture natural teaching, keep anchors temporary, and avoid treating every professor phrase as stable truth`
- Browser/host proof: `N/A - backend curator, professor-anchor, and recall tests only`
- Test name: `CuratorCapture_NaturalProfessorGuidanceCreatesStructuredTemporaryAnchor`
- Test name: `ProfessorAnchor_AssimilationRequiresMasteryEvidenceBeyondIndependentSupport`
- Test name: `ProfessorAnchor_ActiveAnchorSourceMovesDreamCandidateToComparisonReview`
- Test name: `RecallAsync_ExcludesActiveProfessorAnchorMemoryByDefault`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryProfessorAnchorExtraction.cs` | `912A1D402B3DBF08B0FED2957FCD6517E0A63CCEA67EE3098072E76BBCBDF533` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryCuratorConversationService.cs` | `C8F7DA087F0948C5F86642D73AAE77DF6148C89BCB87EFED6AB19C69B5CA3957` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryProfessorAnchorService.cs` | `31168DC96134E052BC2E50A5F8C9AB8F189D1684EF53B932C5192678CA6030F2` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallDataLoading.cs` | `D9ED4BD792E9C6B6EB7424E569C993D017C94362EC564554BCBCEBF196DFFF5E` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\CognitiveMemoryModuleServiceCollectionExtensions.cs` | `C32F3208770E306CA6E5086741D9568A19EC9A569D937FDFED1C6A7F5A4D0548` |
| `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryRecallOrchestratorTests.cs` | `86511CDE5457652179BCB05E063A3B01B7910D00FD1A5FECB6D9199A814B56BC` |

## Proof Artifacts

- Failing-first transcript: `proof/SB03/transcripts/failing-first-targeted-tests.txt`
- Passing transcript: `proof/SB06/transcripts/passing-targeted-professor-anchor-tests.txt`
- Regression transcript: `proof/SB06/transcripts/passing-professor-regression-tests.txt`
- Source assertion transcript: `proof/SB06/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `proof/SB06/transcripts/anti-stub-audit.txt`
- Bundle prepared-stage validator transcript: `proof/SB06/transcripts/prepared-validator-after-sb06.txt`

## Source Assertions

- `CognitiveMemoryProfessorAnchorExtraction.cs` defines structured professor anchor claim extraction with subtypes for teaching answers, confirmations, misconception corrections, scope corrections, and new knowledge.
- `CognitiveMemoryCuratorConversationService.cs` invokes the extractor only when explicit/keyword capture did not already resolve, persists structured summaries with target, claims, misconception, source utterances, lifecycle, and confidence, and stores natural anchors as `NeedsHumanReview`/`Experimental` temporary memory.
- `CognitiveMemoryRecallDataLoading.cs` excludes active/comparing professor anchor applied memory records from ordinary recall unless `includeProfessorAnchors=true` is explicitly supplied.
- `CognitiveMemoryProfessorAnchorService.cs` blocks assimilation when derived evidence explicitly says mastery is missing.
- `CognitiveMemoryModuleServiceCollectionExtensions.cs` registers the extractor as an injectable service.

## Semantic Adequacy

- Raw note owned: professor mode must allow natural teaching without command words and without approving a flood of permanent memories.
- Shipped behavior: natural non-keyword professor guidance is captured into a structured temporary anchor, with target scope and source utterance lineage, while default recall filters active anchors out of stable context.
- Shallow-pass trap: adding another keyword such as `remember` or storing the raw user text as an approved active memory would pass old capture tests but still fail natural professor teaching and recall safety.
- Adversarial negative proof: SB03 failing-first transcript shows natural professor capture and mastery-gated assimilation failed before this implementation.
- Semantic positive proof: SB06 targeted transcript shows natural capture, mastery rejection, dream comparison review, and default recall exclusion pass.
- Anti-stub audit: `anti-stub-audit.txt` finds no TODO, NotImplemented, or fixture/test-name-specific production branches in SB06 production files.

## Progression Decision

SB06 closure passes. SB07 may rely on structured active anchors, recall exclusion, and the mastery gate to implement safe assimilation and fading lifecycle behavior.
