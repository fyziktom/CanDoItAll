# 03 Semantic Embedding Profile

## Status

- `Completed`

## Objective

- Extend SemanticCompletion embedding results with stable profile metadata so Cognitive Memory projections can persist provider/model/profile identity, dimension, normalization, and rebuild signals without re-deriving them ad hoc.

## Success Criteria

- `AgentTextEmbedding` or equivalent result exposes stable profile metadata.
- Local hashing embeddings return deterministic provider/profile metadata.
- ONNX embeddings return deterministic provider/profile metadata when the local model path exists, or tests document environment-dependent skips.
- Existing classifier/ranker behavior remains compatible.

## Covered Inputs

- PR-006, PR-007.
- SemanticCompletion source audit from `analysis/01-current-state.md`.

## Prerequisites

- `01-00-current-state-and-gate` closure gate passed.
- This subbundle can run in parallel with RAG lifecycle work if assigned to a separate worker and write scopes stay in the SemanticCompletion repo.

## Exact Source References

- `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Embeddings\IAgentTextEmbeddingGenerator.cs`
- `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Embeddings\AgentTextEmbedding.cs`
- `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Embeddings\LocalHashingAgentTextEmbeddingGenerator.cs`
- `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Embeddings\LocalHashingAgentTextEmbeddingOptions.cs`
- `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Embeddings\OnnxAgentTextEmbeddingGenerator.cs`
- `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Embeddings\OnnxAgentTextEmbeddingOptions.cs`
- `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\tests\CanDoItAll.AgentFramework.SemanticCompletion.Tests`

## Deliverables

- Embedding profile metadata contract.
- Local hashing profile metadata.
- ONNX profile metadata.
- Tests proving stability and compatibility.

## Dependency Impact

- Cognitive Memory projection records depend on this to track embedding profile hash and rebuild conditions.
- Recall traces depend on this to explain which embedding profile produced a projection or query vector.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Add profile metadata to embedding result without breaking existing callers where practical.
2. Define profile fields that are stable across machines and sufficient for projection rebuild decisions.
3. Update local hashing embedding generator.
4. Update ONNX embedding generator.
5. Add or update tests for profile metadata stability, dimensions, normalization, and classifier compatibility.
6. Update execution report proof.

## Scope Exceptions

- Do not add Cognitive Memory projection entities to SemanticCompletion.
- Do not require downloading a model during normal tests.

## Do Not Do

- Do not make absolute local model directory paths the only profile identity.
- Do not silently omit profile metadata for one provider path.
- Do not change classifier decision semantics unless a test demonstrates the existing behavior is wrong.

## Acceptance Checklist

- Embedding result includes profile metadata.
- Local hashing profile metadata is deterministic.
- ONNX profile metadata is deterministic when model files exist.
- Existing SemanticCompletion tests still pass.

## Proof Required

- SemanticCompletion test command and passing result.
- Targeted tests for embedding profile metadata.
- Source review showing no Cognitive Memory-specific names added.

## Browser Validation Logging

- N/A. No browser-visible or host-visible behavior changes.

## Progression Gate

- Proceed to architecture sync only when embedding profile metadata can be persisted by Cognitive Memory without adapter-specific re-derivation.

## Suggested Agent Prompt

```text
Implement only the SemanticCompletion embedding profile subbundle. Add stable provider/model/profile metadata to embedding results while preserving existing classifier/ranker behavior. Add tests and do not add Cognitive Memory-specific concepts to SemanticCompletion.
```
