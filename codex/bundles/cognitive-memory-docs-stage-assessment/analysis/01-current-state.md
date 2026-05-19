# Current State

## Source Audit Summary

- `CanDoItAll.slnx` includes `src/CanDoItAll.Modules.CognitiveMemory/CanDoItAll.Modules.CognitiveMemory.csproj`.
- `RuntimeHostServiceCollectionExtensions.AddCanDoItAllRuntimeModules()` registers Cognitive Memory through `AddCognitiveMemoryModule()`.
- `ModuleAssemblies` includes `CognitiveMemoryModuleAssemblyMarker`, so EF model configuration discovery includes the module assembly.
- `AppDbContext` applies module configurations through `AppDbContextModelRegistry.ConfigureAssemblies(moduleAssemblies)`.
- `CognitiveMemoryApi.cs` maps 31 endpoints under `/api/cognitive-memory`.
- Cognitive Memory has provider-specific migrations for SQLite and PostgreSQL.
- Tests exist across unit, integration, component, Playwright, and support projects.

## Current Implementation Shape

- Durable memory state lives in `AppDbContext`, not Qdrant.
- Qdrant/RAG is an optional projection target behind `ICognitiveMemoryProjectionAdapter` and `IRagDriver`.
- SemanticCompletion is used as an optional embedding, ranking, and classification utility, not as canonical memory.
- Source ingestion covers Workbench project structure, process runtime evidence, workflow runtime evidence, uploaded files, and web links.
- Consolidation creates source-backed candidates, review items, mutation commands, and canonical memory records when approved/applied.
- Recall combines lexical, optional vector, workspace, signal, graph, and source-detail channels, then persists traces and context packs.
- MAF context contribution is project-scoped and provider-policy guarded.
- Probe, answer-gate, professor-review, self-regulation, cross-project, and distributed records exist as alpha control surfaces.

## Stage Decision

- The honest stage is validation-grade alpha.
- The module is beyond prototype because durable schema, API, UI, ingestion, consolidation, recall, review, and tests exist.
- The module is not beta because projection rebuild orchestration, scheduled automation execution, service decomposition, API contract stabilization, and production observability still need hardening.

## Important Gaps Found

- `CognitiveMemoryAutomationSettings` stores schedule flags, but no dedicated Cognitive Memory scheduler/worker was found.
- Consolidation invalidates projection records, but a normal product path for projection rebuild was not obvious from source search.
- Current consolidation fact extraction is deterministic/rule-based despite model execution profile settings existing.
- Several files are oversized and should be split before more behavior is added: recall services, advanced services, the Blazor page/code-behind, review UI, consolidation, settings, workspace, signals, and ingestion.
- API DTOs are co-located in a large Minimal API file, which is acceptable for alpha but not a stable external API contract.

## Prior Validation Evidence

- Earlier Cognitive Memory repair and validation bundles record 117/117 unit tests passed, 25/25 integration tests passed, 1/1 component test passed, and a serial solution build passed with unrelated `Google.Protobuf` warnings.
- The current task is markdown-only, so code tests are not the closure gate for this bundle.
