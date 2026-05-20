# Scope Inventory

## In scope: skills and bundle process

- `/mnt/data/review_current/CanDoItAll-cognitive-memory/codex/skills/bundles/candoitall-bundle-execution/SKILL.md`
- `/mnt/data/review_current/CanDoItAll-cognitive-memory/codex/skills/bundles/candoitall-bundle-validator/SKILL.md`
- `/mnt/data/review_current/CanDoItAll-cognitive-memory/codex/skills/bundles/candoitall-subbundle-validator/SKILL.md`
- `/mnt/data/review_current/CanDoItAll-cognitive-memory/codex/skills/bundles/candoitall-bundle-preparation/SKILL.md`
- `/mnt/data/review_current/CanDoItAll-cognitive-memory/codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py`

## In scope: cognitive-memory quality services

- `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs`
- `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs`
- `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs`
- `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs`
- `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs`
- `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs`

## In scope: curator/professor services

- `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs`
- `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs`
- `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs`
- `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedEntities.cs`

## In scope: tests

- `/mnt/data/review_current/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`
- `/mnt/data/review_current/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs`
- `/mnt/data/review_current/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Components/CognitiveMemoryPageTests.cs`

## Out of scope

- Economic memory governance.
- Attention market/pricing models.
- Large live provider/LLM integration unless mocked and isolated behind existing providers.
- Major UI redesign beyond controls needed to expose proof/status safely.
