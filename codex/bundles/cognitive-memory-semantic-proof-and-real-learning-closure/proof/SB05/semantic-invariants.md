# SB05 Semantic Invariants

## Invariant SB05-ACCEPTED-USE-01

- Invariant ID: SB05-ACCEPTED-USE-01
- Source raw note: R05 production accepted-use outcome integration.
- Expected behavior: Accepted recall outcomes are handled through a production event handler, emit idempotent accepted-use signals from exact statement evidence, reject broad lineage, and feed scheduled assimilation.
- Disallowed shallow implementation: Counting recall source-map mentions or calling an emitter directly from a test would not prove the actual outcome path.
- Failing-first test: bundle://proof/SB05/transcripts/failing-first.txt.
- Passing test: bundle://proof/SB05/transcripts/passing.txt.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs` hash `A1FF6362E613FFF7D519FD21AF924AD86252F4AED4C756F411767EE95B004FE0`; `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAcceptedUseSignalEmitter.cs` hash `7D5880A0937EDEEC1916B4A5BC03804E276738D53694486951AA16793704091F`; `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryRecallOutcomeAcceptedEventHandler.cs` hash `F27C1A651F2077718FF4F4F8AE3EE3B7AD949C3018299C90C1DA8694C5B830AC`; `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAssimilationEvaluator.cs` hash `B9A7D8501E009F5D543276CA83A7F9F483EED8C5AB4CA7C34D355CE235BBD334`; `repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs` hash `2A6D38387C12BF5F3D606E229F8F46136DE862CC28E48546FAE92C01417288C3`; `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` hash `7CD57001F5EF077151B02AA81909E861FA43EBE3E9D008C883707D9D90A33F2E`.
- Production assertions: bundle://proof/SB05/transcripts/source-assertions.txt cites the production paths that implement the invariant.
- Red-team negative case: bundle://proof/SB05/transcripts/failing-first.txt records the failing shallow case for the invariant.
- Downstream dependency check: SB05 proof is included in bundle://reviews/01-execution-report.md, the cognitive-memory focused tests, and the completed-stage bundle validation.


