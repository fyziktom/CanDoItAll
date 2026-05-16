# 02 RAG Projection Lifecycle

## Status

- `Ready`

## Objective

- Add generic projection lifecycle operations to RAG so stale vector projections can be removed or rebuilt by filter/source-equivalent payload criteria without Cognitive Memory enumerating every point id or calling Qdrant directly.

## Success Criteria

- RAG exposes delete-by-filter or equivalent lifecycle cleanup.
- Capability discovery reports delete-by-filter support.
- Qdrant provider implements the operation or fails explicitly when unavailable.
- Tests prove cleanup can target generic metadata such as source id, projection version, or embedding profile without Cognitive Memory-specific model fields.

## Covered Inputs

- PR-004, PR-005, PR-007.
- RAG lifecycle target in `architecture/01-target-solution.md`.

## Prerequisites

- `02-01-rag-filter-and-payload-contracts` closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Abstractions\IRagDriver.cs`
- `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagDeleteRequest.cs`
- `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagDriverCapabilities.cs`
- `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Qdrant\QdrantRagDriver.cs`
- `C:\repositories\CanDoItAll.AgentFramework.Rag\tests\CanDoItAll.AgentFramework.Rag.Tests`

## Deliverables

- Generic delete-by-filter or projection cleanup contract.
- Qdrant implementation or explicit unsupported behavior.
- Tests for delete-by-id compatibility and delete-by-filter behavior.
- Documentation or sample note showing intended cleanup by metadata fields without Cognitive Memory names.

## Dependency Impact

- Cognitive Memory projection rebuilds depend on this for stale cleanup.
- Cross-project memory and distributed idle compute depend on this to avoid direct provider-specific deletion logic.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Reuse the typed filter contract from the previous subbundle.
2. Add a delete-by-filter request or extend delete requests without breaking delete-by-id callers.
3. Add capability discovery and explicit unsupported behavior.
4. Implement Qdrant deletion through provider APIs or isolate unsupported behavior.
5. Add tests that express cleanup by generic metadata filters.
6. Update execution report proof.

## Scope Exceptions

- Do not build Cognitive Memory projection records.
- Do not require a final collection partitioning decision for Cognitive Memory.

## Do Not Do

- Do not call Qdrant directly from CanDoItAll Cognitive Memory architecture or adapter code as part of this bundle.
- Do not introduce memory-specific cleanup APIs in the generic RAG repo.

## Acceptance Checklist

- Delete-by-id behavior still works.
- Delete-by-filter behavior is available or explicitly unsupported by capability.
- Stale projection cleanup can be described with generic payload filters.
- Tests cover unsupported provider behavior when applicable.

## Proof Required

- RAG tests for delete-by-filter/source-equivalent metadata cleanup.
- Qdrant mapper/driver tests or explicit environment-limited proof.
- Execution report command output.

## Browser Validation Logging

- N/A. No browser-visible or host-visible behavior changes.

## Progression Gate

- Proceed to final architecture sync only when stale projection cleanup no longer requires direct Qdrant access from Cognitive Memory.

## Suggested Agent Prompt

```text
Implement only the generic RAG projection lifecycle cleanup subbundle. Build on the typed filter contracts, preserve existing delete-by-id behavior, add tests, and do not add Cognitive Memory-specific model names.
```
