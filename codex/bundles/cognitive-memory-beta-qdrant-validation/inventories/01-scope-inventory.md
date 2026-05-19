# Scope Inventory

| Area | Files or tools | Purpose |
| --- | --- | --- |
| Docker infrastructure | `docker-compose.yml`, `docker ps`, Qdrant REST `:6333`, Qdrant gRPC `:6334` | Prove provider availability. |
| App configuration | `src/CanDoItAll.Web/appsettings.json`, `src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` | Confirm Qdrant driver registration and default collection. |
| API validation | `src/CanDoItAll.Web/Api/CognitiveMemoryApi*.cs` | Use v1 status, settings, ingestion, consolidation, projection rebuild, recall, and snapshot endpoints. |
| Projection rebuild | `src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryProjectionRebuildService.cs` | Validate durable-to-Qdrant projection. |
| Projection adapter | `src/CanDoItAll.Modules.CognitiveMemory/Projection/CognitiveMemoryProjectionAdapters.cs` | Validate RAG/Qdrant mapping and typed filters. |
| Recall | `src/CanDoItAll.Modules.CognitiveMemory/Recall/*` | Validate vector channel use in recall traces. |
| Docs | `docs/cognitive-memory/**` | Update stage, roadmap, runbooks, and validation evidence. |
| Tests | `tests/*/*CognitiveMemory*.cs` | Keep P0/P1 regression coverage passing. |

