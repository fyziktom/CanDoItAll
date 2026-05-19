# Scope Inventory

| Area | Files | Notes |
|---|---|---|
| Quality contracts | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityContracts.cs` | Preserve public DTOs/interfaces unless a defect requires a deliberate contract change. |
| Quality services | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityServices.cs` | Primary refactor and hardening target. Split by service responsibility. |
| Quality entities | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityEntities.cs` | Review durable fields for failure state, source item support, hashes, provenance, and concurrency. |
| EF mappings | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Quality\CognitiveMemoryQualityEntityConfigurations.cs` | Review indexes, unique constraints, delete behavior, and provider compatibility. |
| Migrations | `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\Migrations\20260519174514_AddCognitiveMemoryQualityFoundation.cs`; `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\Migrations\20260519174540_AddCognitiveMemoryQualityFoundation.cs` | Do not regenerate casually. Add follow-up migration only if schema changes are required. |
| Module registration | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\CognitiveMemoryModuleServiceCollectionExtensions.cs` | Keep DI registrations aligned after splitting services. |
| Recall focus | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallEvaluation.cs` | Preserve the `SideContext` fix. Add regression coverage if refactoring touches recall synthesis. |
| Unit tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryQualityFoundationTests.cs`; `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryRecallOrchestratorTests.cs` | Add defect-focused tests before changing behavior. |
| Integration tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CognitiveMemoryQualityPersistenceModelTests.cs`; `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CognitiveMemoryConsolidationPersistenceModelTests.cs` | Add provider/persistence and repeat-run proof. |
| Prior bundle | `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-quality-foundation-dreaming-synthesis` | Treat as source evidence and update/qualify closure if follow-up execution changes conclusions. |
