# Evidence Notes From Current Review

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAssimilationEvaluator.cs` counts `ProfessorAnchorAcceptedUse` signals, but production grep shows no emitter outside tests and source assertions.
- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` manually seeds `ProfessorAnchorAcceptedUse`, so the current tests can pass without production accepted-use emission.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs` does not call `ScanAssimilationAsync`, so automatic assimilation lifecycle is not wired into scheduled automation.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs` builds final text using `Conclusion`, `Support`, `Condition`, and `Caveat` labels and a conclusion that says the subject is supported by source-backed observations.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs` creates source maps once per record and assigns them to every extracted claim unit from that record.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs` has mostly English teaching signals and question lead-ins.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` has limited ASCII Czech phrases and no diacritic-folded matching in the capture-kind heuristic.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs` calls the brief composer with context pack title/summary rather than the real query/intent.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs` persists statement source maps by combining source refs with every statement aggregate claim id.
