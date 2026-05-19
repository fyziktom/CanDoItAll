# Cognitive Memory

This section is the current source-grounded documentation for the CanDoItAll Cognitive Memory module. Historical bundle folders explain how the feature was built; these pages describe what exists in the repo now.

## Current Stage

Cognitive Memory is at **P0-complete validation-grade alpha** as of 2026-05-19.

That is deliberately not beta. The durable schema, module registration, endpoint-grouped API surface, operator UI, source ingestion, consolidation, review approval, recall traces, explicit projection rebuild, explicit automation execution, MAF context contribution policy, probing, self-regulation, and validation coverage exist. P0 also closed the Blazor child-tab split, explicit scheduler decision, and adapter-backed projection proof. The remaining gaps are beta hardening: API versioning/examples, live provider and failure integration, production observability/runbooks, retention policy, and continued decomposition of older broad service files.

## Start Here

- [Stage assessment](current-state/stage-assessment.md): what is done, what is alpha, and what must happen before beta.
- [Implementation map](current-state/implementation-map.md): source folders, services, endpoints, persistence, tests, and integration owners.
- [System overview](architecture/system-overview.md): architecture-beta and flow diagrams for the current implementation.
- [Domain model](architecture/domain-model.md): class diagrams for the durable model and service shape.
- [Runtime flows](architecture/runtime-flows.md): sequence and flow diagrams for ingestion, consolidation, review, recall, and MAF context.
- [Integration boundaries](architecture/integration-boundaries.md): what owns truth, what is a projection, and what must not mutate canonical memory.
- [API](operations/api.md): current HTTP endpoints and operational notes.
- [Validation and testing](operations/validation-and-testing.md): targeted test commands and remaining coverage gaps.
- [Roadmap](roadmap/roadmap.md): completed work and next steps toward beta.

## Primary Source References

- `src/CanDoItAll.Modules.CognitiveMemory`
- `src/CanDoItAll.Web/Api/CognitiveMemoryApi*.cs`
- `src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `src/CanDoItAll.Composition/ModuleAssemblies.cs`
- `src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `src/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs`
- `tests/*/*CognitiveMemory*.cs`

## Architectural Summary

Cognitive Memory is a product module, not an MCP server and not a private MAF provider. `CanDoItAll.Web` hosts the Blazor route and Minimal API. `CanDoItAll.Composition` registers the module and Qdrant RAG driver when configured. `AppDbContext` stores durable memory state through provider-specific SQLite and PostgreSQL migrations. RAG/Qdrant and SemanticCompletion are adapter-backed projection and semantic utilities, not canonical truth.

The safe mental model is:

1. Source providers expose read-only snapshots from Workbench project structure, process runtime evidence, workflow runtime evidence, uploaded files, and web links.
2. Ingestion persists manifests, source items, provenance, layouts, links, context hints, tombstones, and evidence anchors.
3. Consolidation creates source-backed candidates and governed mutation commands.
4. Approved or machine-generated candidates materialize canonical memory records, claims, source links, and evidence links.
5. Recall combines lexical, optional vector projection, workspace, signal, graph, and source-detail channels into a persisted context pack and trace.
6. Projection rebuild updates rebuildable provider projection rows from durable memory; it does not create canonical truth.
7. Explicit automation runs configured ingestion/consolidation through existing services; it is not a hidden daemon.
8. UI, API, MAF, probes, answer gates, and self-regulation read the durable model and create reviewable signals, not uncontrolled truth mutations.

