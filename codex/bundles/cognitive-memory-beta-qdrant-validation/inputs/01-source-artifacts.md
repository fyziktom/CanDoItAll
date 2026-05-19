# Source Artifacts

- `C:\repositories\CanDoItAll\docs\cognitive-memory\roadmap\roadmap.md`
- `C:\repositories\CanDoItAll\docs\cognitive-memory\current-state\stage-assessment.md`
- `C:\repositories\CanDoItAll\docs\cognitive-memory\operations\provider-failure-runbook.md`
- `C:\repositories\CanDoItAll\docs\cognitive-memory\operations\validation-and-testing.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\appsettings.json`
- `C:\repositories\CanDoItAll\docker-compose.yml`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Operations\CognitiveMemoryProjectionRebuildService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Projection\CognitiveMemoryProjectionAdapters.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi*.cs`
- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-p1-beta-hardening\reviews\01-execution-report.md`

Observed infrastructure before preparation:

- Docker container `candoitall-qdrant` is running image `qdrant/qdrant:v1.15.3`, publishes `6333` and `6334`, and reports healthy.
- Docker container `candoitall-postgres` is running image `postgres:16-alpine`, publishes `5432`, and reports healthy.
- `src/CanDoItAll.Web/appsettings.json` enables Qdrant RAG with host `localhost`, gRPC port `6334`, collection `candoitall-knowledge`, vector size `384`, and collection auto-create enabled.

