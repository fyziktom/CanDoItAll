# SB03 Semantic Invariants

## Invariant SB03-ACCEPTED-USE-PRODUCER-01

- Invariant ID: `SB03-ACCEPTED-USE-PRODUCER-01`
- Source raw note: Accepted-use evidence must be emitted by real recall/workflow acceptance paths.
- Expected behavior: `ICognitiveMemoryProfessorAcceptedUseSignalEmitter.EmitAsync` validates project, actor, recall trace, synthesis id, statement id, derived memory id, accepted outcome id, and source evidence before publishing.
- Disallowed shallow implementation: Manual test seeding or counting selected recall context as accepted use.
- Failing-first test: `SemanticInvariant_AcceptedUseSignalHasProductionEmitterAndScheduledAssimilation` failed in the SB02 baseline.
- Passing test: `AcceptedUseEmitter_PublishesRecallTraceSignalAndRejectsDirectCaptureMemory`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAcceptedUseSignalEmitter.cs`.
- Production assertions: Signal publication is tied to a synthesized statement source map for the derived memory and includes recall trace, synthesis, statement, derived memory, and accepted outcome metadata.
- Red-team negative case: Direct professor capture memory throws and is not counted as derived accepted use.
- Downstream dependency check: Assimilation evaluator can now consume production-published accepted-use signals.

## Invariant SB03-SCHEDULED-ASSIMILATION-02

- Invariant ID: `SB03-SCHEDULED-ASSIMILATION-02`
- Source raw note: Professor assimilation scanning must not remain manual/test-only.
- Expected behavior: Scheduled automation invokes `ICognitiveMemoryProfessorAnchorService.ScanAssimilationAsync` after successful consolidation cycles.
- Disallowed shallow implementation: Calling assimilation only from unit test helpers.
- Failing-first test: SB02 baseline proved scheduled runner lacked professor scan wiring.
- Passing test: `SemanticInvariant_AcceptedUseSignalHasProductionEmitterAndScheduledAssimilation`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs`.
- Production assertions: The scheduled runner receives `ICognitiveMemoryProfessorAnchorService` through DI and reports resolved anchors as warnings.
- Red-team negative case: If no project id or no successful consolidation cycle exists, the scan is not falsely reported.
- Downstream dependency check: SB10 lifecycle proof can rely on scheduled assimilation after consolidation.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ProfessorAnchorAcceptedUse` signal | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAcceptedUseSignalEmitter.cs` | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAssimilationEvaluator.cs` | `repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs` | `bundle://proof/SB03/transcripts/anti-stub.txt` |
