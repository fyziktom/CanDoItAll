# SB04 Semantic Invariants

## Invariant SB04-CZECH-DIACRITIC-01

- Invariant ID: SB04-CZECH-DIACRITIC-01
- Source raw note: R04 Czech professor teaching capture with diacritics.
- Expected behavior: Natural Czech professor teaching is captured without English keywords, keeps diacritics, records language, and preserves examples and counterexamples.
- Disallowed shallow implementation: Adding Czech words to a test title or stripping diacritics from stored text would still leave the production extractor English-only.
- Failing-first test: bundle://proof/SB04/transcripts/failing-first.txt.
- Passing test: bundle://proof/SB04/transcripts/passing.txt.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs` hash `6565034DE52E3FB8D6DDC41D1B76428417C4F6B3385B61AACD284D842F0FCE46`; `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` hash `1257A829C07154C670023628C1F908D399CD1E0F193672A54136F2803B61F6D6`; `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` hash `7CD57001F5EF077151B02AA81909E861FA43EBE3E9D008C883707D9D90A33F2E`.
- Production assertions: bundle://proof/SB04/transcripts/source-assertions.txt cites the production paths that implement the invariant.
- Red-team negative case: bundle://proof/SB04/transcripts/failing-first.txt records the failing shallow case for the invariant.
- Downstream dependency check: SB04 proof is included in bundle://reviews/01-execution-report.md, the cognitive-memory focused tests, and the completed-stage bundle validation.


