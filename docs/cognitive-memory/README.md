# Cognitive Memory

This section is the current source-grounded documentation for the CanDoItAll Cognitive Memory module. Historical bundle folders explain how the feature was built; these pages describe what exists in the repo now.

## Current Stage

Cognitive Memory is at **P1 beta for the core memory and Qdrant-backed recall path** as of 2026-05-19.

The durable schema, module registration, versioned API aliases, contract/examples endpoint, operator UI, source ingestion, consolidation, review approval, recall traces, explicit projection rebuild, explicit automation execution, retention cleanup with durable run audit, operator audit signals, external-source safety policy, MAF context contribution policy, probing, self-regulation, and validation coverage exist. P1 is now beta-covered for the path from public source ingestion through durable memory, missing-record projection rebuild, Docker Qdrant projection, and public vector recall. The advanced control surfaces such as cross-project promotion, distributed compute, professor review, and broad workflow automation remain alpha/P2 hardening areas.

## Start Here

- [Stage assessment](current-state/stage-assessment.md): what is beta-covered, what is still alpha, and what remains for production hardening.
- [Implementation map](current-state/implementation-map.md): source folders, services, endpoints, persistence, tests, and integration owners.
- [System overview](architecture/system-overview.md): architecture-beta and flow diagrams for the current implementation.
- [Domain model](architecture/domain-model.md): class diagrams for the durable model and service shape.
- [Runtime flows](architecture/runtime-flows.md): sequence and flow diagrams for ingestion, consolidation, review, recall, and MAF context.
- [Integration boundaries](architecture/integration-boundaries.md): what owns truth, what is a projection, and what must not mutate canonical memory.
- [API](operations/api.md): current HTTP endpoints and operational notes.
- [Provider failure runbook](operations/provider-failure-runbook.md): deterministic failure proof and live Docker Qdrant validation for projection rebuild.
- [Retention cleanup](operations/retention-cleanup.md): explicit cleanup policy and API usage.
- [External source policy](operations/external-source-policy.md): limits, extraction errors, and sensitive-content behavior.
- [Performance baselines](operations/performance-baselines.md): baseline commands and thresholds for large source/recall work.
- [Validation and testing](operations/validation-and-testing.md): targeted test commands and remaining coverage gaps.
- [Roadmap](roadmap/roadmap.md): completed P0/P1 work and next P2/P3 hardening.

## Primary Source References

- `src/Modules/CanDoItAll.Modules.CognitiveMemory`
- `src/App/CanDoItAll.Web/Api/CognitiveMemoryApi*.cs`
- `src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `src/App/CanDoItAll.Composition/ModuleAssemblies.cs`
- `src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs`
- `tests/*/*CognitiveMemory*.cs`

## Architectural Summary

Cognitive Memory is a product module, not an MCP server and not a private MAF provider. `CanDoItAll.Web` hosts the Blazor route and Minimal API. `CanDoItAll.Composition` registers the module and Qdrant RAG driver when configured. `AppDbContext` stores durable memory state through PostgreSQL migrations. RAG/Qdrant and SemanticCompletion are adapter-backed projection and semantic utilities, not canonical truth.

The safe mental model is:

1. Source providers expose read-only snapshots from Workbench project structure, process runtime evidence, workflow runtime evidence, uploaded files, and web links.
2. Ingestion persists manifests, source items, provenance, layouts, links, context hints, tombstones, and evidence anchors.
3. Consolidation creates source-backed candidates and governed mutation commands.
4. Approved or machine-generated candidates materialize canonical memory records, claims, source links, and evidence links.
5. Recall combines lexical, optional vector projection, workspace, signal, graph, and source-detail channels into a persisted context pack and trace.
6. Projection rebuild updates rebuildable provider projection rows from durable memory and can project missing durable records when collection/profile/embedding settings are explicit; it does not create canonical truth.
7. Explicit automation and retention cleanup run only through operator/API commands; they are not hidden daemons.
8. Operator audit surfaces mutation commands, audit events, claim state, evidence anchors, projection failures, and retention cleanup runs without exposing raw mutation payloads.
9. UI, API, MAF, probes, answer gates, and self-regulation read the durable model and create reviewable signals, not uncontrolled truth mutations.

