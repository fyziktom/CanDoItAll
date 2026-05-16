# 01a Common Drivers Helpers And EF Guardrails

## Status

- Ready after module foundation.

## Objective

- Establish the shared helpers, fake drivers, paging contracts, EF query rules, serialization policy, and performance guardrails that every later Cognitive Memory subbundle must consume.

## Covered Inputs

- Requirements FR-021, FR-022, NFR-001, NFR-003, NFR-005, NFR-009, NFR-010, NFR-011, NFR-012, and NFR-013.
- Architecture review findings about stringly typed seams, vector copying, JSON overuse, EF query shape, and high-volume source scans.

## Prerequisites

- `01-module-foundation` must define module registration, durable identity types, policy surfaces, and initial EF model conventions.
- The prerequisite boundary gate must remain closed and trusted.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\02-module-boundaries.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\03-memory-taxonomy-and-data-model.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\13-operational-modes-and-scale.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\test-and-quality-plan.md
- C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\AppDbContextModelRegistry.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Sources\MemorySourceSnapshotContracts.cs
- C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Abstractions\IRagDriver.cs
- C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Embeddings\IAgentTextEmbeddingGenerator.cs

## Deliverables

- Shared strongly typed value objects for operation ids, stage ids, section ids, evidence kinds, evaluator profiles, projection profiles, embedding profiles, and status/state fields.
- Common paging/cursor, batch budget, byte budget, timeout, and cancellation helper contracts.
- EF query policy document and test helpers for `AsNoTracking`, projection DTOs, index expectations, compiled-query candidates, and bulk update/delete candidates.
- Source-generated JSON serialization plan for durable payloads and trace/report artifacts.
- Deterministic fake embedding provider, fake vector store, fake source snapshot provider, and fake policy context for downstream tests.
- Vector ownership guidance so `float[]` is limited to adapter/serialization boundaries and hot paths can use memory/span-oriented APIs.

## Dependency Impact

- Source ingestion, projection, recall, consolidation, probing, and Epistemic Drive must use these shared helpers instead of inventing local paging, hashing, serialization, status strings, or fake drivers.
- EF model subbundles must use the same index and query-shape rules so later performance proof is comparable.
- Any later subbundle that needs a new string status, JSON payload, unbounded list, or vector array must justify it in its progression gate.

## Validation Depth

- Unit tests for typed value-object parsing, equality, and invalid input rejection.
- Unit tests for paging/budget helpers and deterministic fake providers.
- EF model-shape tests that verify required indexes and enum conversions for newly introduced state fields.
- Analyzer-style tests or grep checks for banned string-state fields in Cognitive Memory contracts and entities.
- Micro-level allocation review for vector and context-pack helpers when they sit on recall/projection hot paths.

## Implementation Steps

1. Define shared typed ids and state/profile/evidence contracts.
2. Define paging, batch, budget, and source cursor helper contracts.
3. Define fake providers and test builders used by downstream subbundles.
4. Define EF query/index rules and make them part of subbundle proof.
5. Define serialization contexts and durable payload versioning rules.
6. Update downstream subbundle prompts to consume these helpers.

## Do Not Do

- Do not implement source-specific ingestion behavior here.
- Do not implement Qdrant, SemanticCompletion, recall, consolidation, probing, or Epistemic Drive behavior here.
- Do not introduce fallback mechanisms that silently hide provider errors.
- Do not allow unbounded `IReadOnlyList<T>` result contracts on source scans, trace lists, review queues, relation lists, or evidence queries.

## Acceptance Checklist

- Later subbundles have reusable fake drivers and helper APIs before their implementation starts.
- Query-relevant state is strongly typed and indexed, not hidden in JSON.
- Read-only EF query guidance, paging guidance, and bulk-mutation guidance are explicit.
- Vector and JSON serialization boundaries are documented and testable.

## Proof Required

- Build/test proof for helper and fake-provider projects.
- EF model/index test output for any foundation entities introduced here.
- Grep or analyzer proof that Cognitive Memory contracts do not add new stringly typed state fields without a documented protocol exception.

## Browser Validation Logging

- No browser proof is required for helper-only work.
- Any UI-visible change fails this subbundle scope and must move to a UI subbundle.

## Progression Gate

- Proceed to source ingestion, adapters, taxonomy, and recall only when the common helper tests pass and the EF/performance rules are recorded in the execution report.
- If downstream phases need to bypass these helper contracts, reopen this subbundle instead of adding phase-local substitutes.

## Suggested Agent Prompt

- Implement only the Cognitive Memory shared helper, fake driver, serialization, EF query, and performance guardrail layer. Do not build ingestion, projection, recall, UI, probing, or learning behavior in this subbundle.
