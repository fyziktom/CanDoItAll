# 01 RAG Filter And Payload Contracts

## Status

- `Completed`

## Objective

- Add provider-neutral typed filtering, payload index contracts, and capability discovery to the RAG driver so Cognitive Memory can request scoped projection searches without ad hoc string filters or unsafe post-filtering.

## Success Criteria

- Search requests can carry typed filters.
- Filter validation rejects unsupported or invalid shapes predictably.
- Qdrant filter translation is tested at mapper level.
- Payload index support is represented through generic contracts and capabilities.
- Existing basic search/upsert/delete behavior remains compatible.

## Covered Inputs

- PR-002, PR-003, PR-005, PR-007.
- `architecture/01-target-solution.md` RAG driver target.
- RAG source audit from `analysis/01-current-state.md`.

## Prerequisites

- `01-00-current-state-and-gate` closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Abstractions\IRagDriver.cs`
- `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagSearchRequest.cs`
- `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagKnowledgeEntry.cs`
- `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagDriverCapabilities.cs`
- `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Qdrant\Mapping\QdrantRagMapper.cs`
- `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Qdrant\QdrantRagDriver.cs`
- `C:\repositories\CanDoItAll.AgentFramework.Rag\tests\CanDoItAll.AgentFramework.Rag.Tests\Qdrant\QdrantRagMapperTests.cs`

## Deliverables

- Strongly typed RAG filter model.
- RAG search request support for filters.
- Payload index request/result contracts or equivalent provider-neutral index operation model.
- Capability discovery for filters and payload indexes.
- Qdrant mapper/driver support or explicit unsupported behavior.
- Unit tests for validation and translation.

## Dependency Impact

- Projection lifecycle cleanup depends on typed filters.
- Cognitive Memory RAG adapters depend on this to avoid direct Qdrant calls.
- Recall orchestrator depends on this to avoid global unscoped vector search followed by local post-filtering.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Design the smallest typed filter model that covers equality, membership, range, existence, and boolean composition.
2. Add validation so invalid fields, empty groups, incompatible values, and unsupported operations fail explicitly.
3. Extend search requests and capabilities additively where practical.
4. Add provider-neutral payload index contracts.
5. Implement Qdrant translation in mapper/driver code or explicit unsupported behavior where full support is not yet possible.
6. Add tests for model validation, mapper translation, capabilities, and compatibility.
7. Update the execution report with commands and results.

## Scope Exceptions

- Do not add Cognitive Memory payload field constants to the RAG repo.
- Do not require live Qdrant if mapper and contract tests fully cover translation.

## Do Not Do

- Do not introduce a string expression filter language.
- Do not silently ignore filters when a provider cannot apply them.
- Do not make Qdrant-specific types part of generic RAG driver contracts.

## Acceptance Checklist

- Filters are strongly typed.
- Search can be scoped before result cutoff when provider supports it.
- Capabilities expose filter and index support.
- Unsupported paths fail predictably.
- Tests cover valid and invalid filter shapes.

## Proof Required

- RAG test project command and passing result.
- Mapper tests proving Qdrant translation.
- Source review showing no Cognitive Memory-specific names added to RAG contracts.

## Browser Validation Logging

- N/A. No browser-visible or host-visible behavior changes.

## Progression Gate

- Proceed to projection lifecycle only when typed filters and payload index contracts have passing tests and no unsafe silent fallback behavior remains.

## Suggested Agent Prompt

```text
Implement only RAG typed filter, payload index, and capability contracts. Keep the driver generic and provider-neutral. Add validation and Qdrant mapper tests. Do not implement Cognitive Memory or add Cognitive Memory-specific payload names.
```
