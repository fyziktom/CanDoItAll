# SB09 Semantic Invariants

## Invariant SB09-ARCHITECTURE-OPTIONS-01

- Invariant ID: SB09-ARCHITECTURE-OPTIONS-01
- Source raw note: R09 maintainability boundaries and injected options.
- Expected behavior: Cognitive-memory quality services use focused internal boundaries and DI-provided options instead of production-path direct options construction.
- Disallowed shallow implementation: Rename-only refactoring or leaving direct options construction in DI-built services would keep future behavior changes brittle.
- Failing-first test: bundle://proof/SB09/transcripts/failing-first.txt.
- Passing test: bundle://proof/SB09/transcripts/passing.txt.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs` hash `2A6D38387C12BF5F3D606E229F8F46136DE862CC28E48546FAE92C01417288C3`; `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs` hash `0010D63663A3A47DF9837D21C54964BE0071501EC05E1FD9AB051D85735472BB`; `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs` hash `717255C67971B4D1503E34D7A523A17567DBF3A55A5C190C0C3CA5F48CD488C6`; `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs` hash `E185545B52F1920632A85CA52F4D4BD38A44EB216DEFFF890E042837BB0FADB5`; `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs` hash `0588FFEDA4292BD26B0443AAE32C7C090AC49460ABFDCD5D74D886582646C62D`; `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs` hash `6565034DE52E3FB8D6DDC41D1B76428417C4F6B3385B61AACD284D842F0FCE46`; `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryRecallOutcomeAcceptedEventHandler.cs` hash `F27C1A651F2077718FF4F4F8AE3EE3B7AD949C3018299C90C1DA8694C5B830AC`; `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` hash `27856051661381AA5D341D51D3D8E4C7C1D2C810AE0C1829B2B3B6EF0BC16954`.
- Production assertions: bundle://proof/SB09/transcripts/source-assertions.txt cites the production paths that implement the invariant.
- Red-team negative case: bundle://proof/SB09/transcripts/failing-first.txt records the failing shallow case for the invariant.
- Downstream dependency check: SB09 proof is included in bundle://reviews/01-execution-report.md, the cognitive-memory focused tests, and the completed-stage bundle validation.


