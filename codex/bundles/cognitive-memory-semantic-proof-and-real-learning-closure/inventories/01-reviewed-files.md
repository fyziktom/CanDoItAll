# Reviewed File Inventory

| Area | File | Finding |
|---|---|---|
| Bundle proof | `repo://codex/bundles/cognitive-memory-production-signal-and-deep-synthesis-followup/proof/SB01/manifest.md` | Contains machine-specific Windows active skill path that breaks moved-checkout validation. |
| Bundle validator | `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` | Stronger than before but still validates proof shape, not literal capability claims. |
| Professor capture | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs` | English-only teaching signals and no diacritic-insensitive matching. |
| Accepted use | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAcceptedUseSignalEmitter.cs` | Real emitter exists, but app-level outcome event integration still needs proof. |
| Assimilation | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAssimilationEvaluator.cs` | Improved but still count/link based; exact claim entailment is weak. |
| Scheduling | `repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs` | Assimilation scan exists but only after successful consolidation cycles. |
| Clustering | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs` | Provider named embedding-backed is lexical/alias based. |
| Dream synthesis | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs` | Still emits source-claim meta text. |
| Dream provenance | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs` | Claim units inherit all source maps for the record. |
| Support loader | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualitySupport.cs` | Does not load `CognitiveMemoryClaimEvidenceLinkRecord`. |
| Recall | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs` | Better but still fragment-based. |
| Tests | `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` | Many tests exist, but Czech/diacritic and outcome-event integration need stronger failing-first coverage. |
