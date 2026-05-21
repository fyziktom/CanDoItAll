# SB09 Semantic Invariants

## Invariant SB09-BOUNDARIES-01

- Invariant ID: `SB09-BOUNDARIES-01`
- Source raw note: Large cognitive-memory responsibilities should be split into cohesive components where it reduces real complexity.
- Expected behavior: Production accepted-use emission and approximate cluster candidate generation have explicit interfaces and DI registration.
- Disallowed shallow implementation: Moving large methods unchanged into new files or hiding behavior behind static globals.
- Failing-first test: SB02 red baseline exposed missing producer/provider boundaries; SB09 builds on the SB03-SB08 fixes.
- Passing test: `CognitiveMemoryModule_RegistersQualityCollaboratorsAndVersionedOptions`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAcceptedUseSignalEmitter.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs`, and module registration.
- Production assertions: New collaborators own focused responsibilities and are registered through DI.
- Red-team negative case: No broad no-op file move was performed; `bundle://proof/SB09/responsibility-inventory.md` documents the boundary decision.
- Downstream dependency check: SB10 can cite production services, not inline helper seams.

## Invariant SB09-NO-BEHAVIOR-REGRESSION-02

- Invariant ID: `SB09-NO-BEHAVIOR-REGRESSION-02`
- Source raw note: Maintainability cleanup must not weaken behavior.
- Expected behavior: All SB03-SB08 focused tests and the affected Cognitive Memory suite pass after boundary cleanup.
- Disallowed shallow implementation: Refactor that drops lifecycle scans, proof lineage, or capture safeguards.
- Failing-first test: SB02 baseline plus SB03-SB08 red transcripts.
- Passing test: Focused 9-test run and affected 119-test run.
- Changed source files: See `bundle://proof/SB09/transcripts/changed-file-hashes.txt`.
- Production assertions: Accepted-use publication, comparison resolution, Czech capture, dream provenance, approximate provider, recall lineage, and DI registration stay green together.
- Red-team negative case: Anti-stub transcript confirms producer/lifecycle logic is in production source.
- Downstream dependency check: Completed-stage validation can evaluate a stable final state.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Production service boundaries | `bundle://proof/SB09/responsibility-inventory.md` | `repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs` | `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryModuleRegistrationTests.cs` | `bundle://proof/SB09/transcripts/anti-stub.txt` |
