# 03 Semantic And RAG Adapters

## Status

- Passed on 2026-05-16.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.
## Objective
- Wrap the existing SemanticCompletion and RAG repositories behind Cognitive Memory projection and semantic utility contracts.

## Covered Inputs

- Requirements FR-006, FR-008, FR-009, NFR-003, NFR-004, NFR-006, and NFR-009.
- Source audit of RAG and SemanticCompletion capabilities.

## Prerequisites

- `01-module-foundation` must define provider-neutral projection and embedding abstractions.
- `01a-common-drivers-helpers-and-ef-guardrails` must provide fake embedding/vector providers, profile value objects, serialization rules, and bounded batch helpers.
- `14-neuro-foundation-claim-evidence-ledger` must define typed projection payload requirements for claim ids, context frames, evidence anchors, and belief state.
- `02-workbench-and-source-ingestion` should supply source items for end-to-end projection tests.
- `codex/bundles/cognitive-memory-projection-boundary-hardening` is completed and must be consumed as the approved projection boundary for typed RAG filtering, payload-indexed search, delete-by-source cleanup, and embedding-profile based rebuild logic.

## Exact Source References

- C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Abstractions\IRagDriver.cs
- C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagSearchRequest.cs
- C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagFilter.cs
- C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagPayloadIndexRequest.cs
- C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagDeleteByFilterRequest.cs
- C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagKnowledgeEntry.cs
- C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Qdrant\QdrantRagDriver.cs
- C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Embeddings\IAgentTextEmbeddingGenerator.cs
- C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Embeddings\AgentTextEmbeddingProfile.cs
- C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Semantics\SemanticTextRanker.cs
- C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Semantics\SemanticClassifier.cs

## Deliverables

- Cognitive embedding adapter over SemanticCompletion.
- Recall intent classifier/ranker adapter.
- Projection adapter over `IRagDriver`.
- Consumption of the hardened RAG extension contracts for typed filters, payload indexes, delete-by-source cleanup, and projection metadata.

## Dependency Impact

- Cognitive Memory depends on generic driver contracts.
- Qdrant remains a replaceable projection backend.
- Filter, payload index, lifecycle cleanup, and embedding profile extensions are closed in the projection boundary hardening follow-up and must remain generic.

## Validation Depth

- Unit tests with deterministic fake embeddings.
- Integration tests with Qdrant where available.
- Failure tests for Qdrant and embedding provider unavailability.
- Allocation review for vector ownership so hot paths do not copy `float[]` unnecessarily outside adapter boundaries.

## Implementation Steps

- Add provider-neutral adapter contracts.
- Implement SemanticCompletion adapter.
- Implement RAG projection adapter.
- Consume typed filter, payload index, delete-by-source, and embedding-profile contracts from the completed projection boundary follow-up.

## Do Not Do

- Do not store canonical memory only in Qdrant.
- Do not put Cognitive Memory-specific semantics into the generic RAG driver.
- Do not silently fall back to a lower-quality embedding provider unless the active mode permits it and the trace records it.

## Acceptance Checklist

- Passed: projection entries require durable memory ids, source hashes, payload hashes, evidence anchors, embedding/projection profiles, and typed claim/context/belief metadata.
- Passed: search failures are visible at the adapter boundary when required payload metadata such as `memoryRecordId` or `payloadHash` is missing.
- Passed: adapter tests use deterministic in-memory fakes and do not require external API access.
- Passed: public Cognitive Memory projection uses `ProjectAsync`, not direct authoritative upsert semantics.

## Proof Required

- `src/CanDoItAll.Modules.CognitiveMemory/Projection/CognitiveMemoryProjectionAdapterContracts.cs`
- `src/CanDoItAll.Modules.CognitiveMemory/Projection/CognitiveMemoryProjectionAdapters.cs`
- `src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs`
- `tests/CanDoItAll.Tests.Unit/CognitiveMemoryProjectionAdapterTests.cs`
- `dotnet build .\src\CanDoItAll.Modules.CognitiveMemory\CanDoItAll.Modules.CognitiveMemory.csproj --no-restore`
- `dotnet build .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore`
- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter FullyQualifiedName~CognitiveMemoryProjectionAdapterTests` passed 10/10.
- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter CognitiveMemory` passed 43/43.
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter CognitiveMemory` passed 10/10.
- `dotnet build .\CanDoItAll.slnx --no-restore` passed with zero warnings.
- Optional Qdrant integration was not run; this phase proves provider-neutral adapter behavior without requiring a local Qdrant endpoint.

## Browser Validation Logging

- No browser proof is required for adapter-only work.
- Projection health browser proof belongs to UI subbundles.

## Progression Gate

- Passed: taxonomy/projection modeling may start. It must consume the projection adapter as rebuildable projection state and must not treat RAG/Qdrant as canonical memory truth.

## Suggested Agent Prompt

- Implement Cognitive Memory adapters over the existing SemanticCompletion and RAG contracts without making either repository the memory source of truth.
