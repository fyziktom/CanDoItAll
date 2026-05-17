# 05 Recall Orchestrator

## Status

- Completed
- Completion detail: Passed on 2026-05-16. `06-consolidation-engine` may start.
- Backend-only phase; no UI files changed and browser proof remains deferred until trace/review UI work.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.
## Objective
- Implement staged recall with explicit modes, budgets, traces, source loading, context-pack construction, and safe degradation.

## Implementation Result

- Added typed recall modes, intent kinds, budgets, trace stages, channel decisions, candidate ids, context-pack ids, context sections, source refs, and the `ICognitiveMemoryRecallOrchestrator` service contract.
- Implemented bounded staged recall across lexical durable-memory reads, typed-filter projection search, workspace focus, signal activation, and graph expansion.
- Integrated `RecallCandidate` score geometry vectors/shapes for ranking, inhibition, context-boundary decisions, and persisted score trace/component rows.
- Persisted recall trace stages, recall candidates, context packs, context sections, source refs, answer-gate/context-pack hooks, and provider-specific migrations for SQLite and PostgreSQL.
- Rendered context packs with source/evidence refs, explicit selected/inhibited/excluded candidates, budget exclusions, and restricted-source redaction behavior.
- Recorded unavailable projection/filter/embedding channels as trace stages instead of falling back to unscoped vector search plus local post-filtering.

## Covered Inputs

- Requirements FR-006, FR-010, FR-011, FR-020, FR-021, NFR-006, NFR-007, NFR-010, and NFR-011.
- Recall architecture and operational modes.

## Prerequisites

- `04-memory-taxonomy-and-projections` must provide canonical memory, relations, and projection state.
- `03-semantic-and-rag-adapters` must provide semantic and projection channel adapters.
- `01a-common-drivers-helpers-and-ef-guardrails` must provide recall budgets, typed trace stage/section ids, fake providers, and EF query policy.
- `01b-score-geometry-driver` must provide recall candidate score spaces, vector/shape evaluation, scalar projection policy, and deterministic score fixtures.
- `14-neuro-foundation-claim-evidence-ledger` must provide claim/evidence/context data.
- `15-cognitive-workspace-attention-router` must provide workspace frame and attention decision contracts.
- `16-prediction-error-salience-signals` must provide signal inputs for activation and trace evidence.
- Projection-backed recall modes must consume the completed `codex/bundles/cognitive-memory-projection-boundary-hardening` contracts so vector search is scoped through typed RAG filters instead of global search plus post-filtering.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\05-recall-orchestrator.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\13-operational-modes-and-scale.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\RecallContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Search\SearchIndexing.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Context.cs

## Deliverables

- Recall request/mode/budget contracts.
- Multi-channel recall orchestration.
- Recall trace persistence.
- Score evaluation traces for ranking, focus, inhibition, and context-boundary decisions.
- Context-pack renderer with source refs and exclusions.

## Dependency Impact

- MAF and workflows consume recall output later.
- UI consumes traces and context-pack details.
- Search/RAG/source channels report availability rather than hiding failure.
- Vector channels must use scoped provider filters or explicit unavailable-channel traces; unscoped projection search plus local post-filtering is not acceptable for strict modes.

## Validation Depth

- Unit tests for score geometry consumption, budget exclusion, and mode behavior.
- Integration tests for lexical, vector, graph, and source fallback paths.
- EF/performance tests proving candidate queries are paged, no-tracking for reads, projected to DTOs, and bounded by recall budgets.

## Implementation Steps

- Define recall modes and budgets.
- Implement staged candidate activation and focus selection.
- Evaluate recall candidates through score geometry and persist vector/shape traces.
- Add trace recording for inclusions, exclusions, failures, and budget limits.
- Render context packs without leaking restricted source content.

## Do Not Do

- Do not truncate silently.
- Do not inject raw secrets or restricted content into context packs.
- Do not call Qdrant directly from MAF.
- Do not rely on unscoped vector search followed by local post-filtering for project/user/security scoped recall.
- Do not implement a local weighted-sum final score.

## Acceptance Checklist

- Recall traces explain every channel used or skipped.
- Recall traces include score vector snapshots, matched shapes, scalar projections, and missing dimensions.
- Qdrant unavailability degrades predictably.
- Context packs cite source evidence and budget exclusions.

## Closure Proof

- `dotnet build .\src\CanDoItAll.Modules.CognitiveMemory\CanDoItAll.Modules.CognitiveMemory.csproj --no-restore` passed with zero warnings.
- `dotnet build .\src\CanDoItAll.Migrations.Sqlite\CanDoItAll.Migrations.Sqlite.csproj --no-restore` passed with zero warnings.
- `dotnet build .\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --no-restore` passed with zero warnings.
- `dotnet ef migrations has-pending-model-changes` passed for SQLite and PostgreSQL; both reported no model changes after the recall migration.
- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~CognitiveMemoryRecallOrchestratorTests"` passed 4/4.
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~CognitiveMemoryRecallPersistenceModelTests"` passed 2/2.
- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~CognitiveMemory"` passed 63/63.
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~CognitiveMemory"` passed 18/18.
- `dotnet build .\CanDoItAll.slnx --no-restore` passed with zero warnings.
- Static grep proof found no local final-score, `Dictionary<string,double>`, authoritative upsert, direct truth mutation, unscoped vector, or local post-filtering surfaces under recall.
- .NET hot-path performance scan found no critical issues after removing the avoidable token-estimation `params` allocation and metadata dictionary allocations.

## Deviations

- Browser proof remains not applicable for this backend-only phase and is still owned by `08-human-review-ui`.
- Answer-gate, MAF integration, probing, consolidation, Epistemic Drive, cross-project promotion, and distributed compute behavior remain downstream work.

## Proof Required

- Recall unit and integration tests.
- Failure-path tests for unavailable projection and unavailable embedding provider.
- Trace inspection evidence.

## Browser Validation Logging

- Browser proof is deferred until the trace viewer exists.
- Capture route and viewport evidence in `08-human-review-ui`.

## Progression Gate

- Proceed to consolidation next. Proceed to MAF integration only after recall output is stable, traceable, and downstream review/UI/procedure/replay gates permit it.
- Projection-backed channels consume the completed projection boundary hardening gate through typed filters; unscoped search plus local post-filtering remains forbidden.
- Reopen `01b-score-geometry-driver` if recall requires score dimensions or shapes not covered by the registered score space.

## Suggested Agent Prompt

- Implement staged recall and trace persistence without coupling recall directly to MAF private context internals.
