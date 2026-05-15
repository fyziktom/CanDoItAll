# 05 Recall Orchestrator

## Status

- Ready after taxonomy and projections.

## Objective

- Implement staged recall with explicit modes, budgets, traces, source loading, context-pack construction, and safe degradation.

## Covered Inputs

- Requirements FR-006, FR-010, FR-011, FR-020, FR-021, NFR-006, NFR-007, NFR-010, and NFR-011.
- Recall architecture and operational modes.

## Prerequisites

- `04-memory-taxonomy-and-projections` must provide canonical memory, relations, and projection state.
- `03-semantic-and-rag-adapters` must provide semantic and projection channel adapters.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\architecture\05-recall-orchestrator.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\architecture\13-operational-modes-and-scale.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\contracts\csharp\RecallContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Search\SearchIndexing.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Context.cs

## Deliverables

- Recall request/mode/budget contracts.
- Multi-channel recall orchestration.
- Recall trace persistence.
- Context-pack renderer with source refs and exclusions.

## Dependency Impact

- MAF and workflows consume recall output later.
- UI consumes traces and context-pack details.
- Search/RAG/source channels report availability rather than hiding failure.

## Validation Depth

- Unit tests for scoring, budget exclusion, and mode behavior.
- Integration tests for lexical, vector, graph, and source fallback paths.

## Implementation Steps

- Define recall modes and budgets.
- Implement staged candidate activation and focus selection.
- Add trace recording for inclusions, exclusions, failures, and budget limits.
- Render context packs without leaking restricted source content.

## Do Not Do

- Do not truncate silently.
- Do not inject raw secrets or restricted content into context packs.
- Do not call Qdrant directly from MAF.

## Acceptance Checklist

- Recall traces explain every channel used or skipped.
- Qdrant unavailability degrades predictably.
- Context packs cite source evidence and budget exclusions.

## Proof Required

- Recall unit and integration tests.
- Failure-path tests for unavailable projection and unavailable embedding provider.
- Trace inspection evidence.

## Browser Validation Logging

- Browser proof is deferred until the trace viewer exists.
- Capture route and viewport evidence in `08-human-review-ui`.

## Progression Gate

- Proceed to MAF integration only after recall output is stable and traceable.

## Suggested Agent Prompt

- Implement staged recall and trace persistence without coupling recall directly to MAF private context internals.
