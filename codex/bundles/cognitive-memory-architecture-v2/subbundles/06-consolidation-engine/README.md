# 06 Consolidation Engine

## Status

- Completed
- Completion detail: Passed on 2026-05-16. `17-temporal-replay-scheduler` may start.
- Backend-only phase; no UI files changed and browser proof remains deferred until review/consolidation UI work.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.
## Objective
- Add resumable consolidation jobs that create/refine episodic, procedural, decision, reflection, contradiction, and review candidates.

## Implementation Result

- Added typed consolidation modes, trigger kinds, candidate kinds/statuses, budgets, profiles, run requests/results, candidate/report payload contracts, run ids, candidate ids, and the `ICognitiveMemoryConsolidationEngine` service contract.
- Persisted consolidation run, candidate, cursor, and report records with provider migrations for SQLite and PostgreSQL plus EF guardrail index expectations.
- Implemented idempotent run replay, active lease blocking, duplicate source/candidate suppression, bounded source item processing, failure recording, and cursor advancement only after durable accepted writes.
- Routed generated consolidation candidates through mutation authority `RecordEvidence` commands with source evidence anchors and mandatory human-review policy.
- Created review items for accepted review-required candidates, and blocked no-evidence rejected candidates without advancing the cursor or creating review rows.
- Evaluated consolidation candidates through the `ConsolidationCandidate` score space and persisted score trace/component rows for source sufficiency, evidence strength, source quality, risk, redaction, contradiction, recency, and procedure maturity.
- Marked linked projections rebuild-required with source-hash stale reason only after candidate/review/report state is durable.

## Covered Inputs

- Requirements FR-012, FR-013, FR-014, FR-015, FR-016, FR-020, FR-021, FR-022, and NFR-012.
- Consolidation architecture and process/workflow source audit.

## Prerequisites

- `04-memory-taxonomy-and-projections` must provide canonical memory and projection state.
- `01a-common-drivers-helpers-and-ef-guardrails` must provide lease, idempotency, batch, source-generated JSON, and EF bulk-operation guidance.
- `01b-score-geometry-driver` must provide activation, contradiction, and consolidation candidate score evaluation contracts.
- `14-neuro-foundation-claim-evidence-ledger` must provide mutation authority, evidence anchors, claims, and context frames.
- `16-prediction-error-salience-signals` must provide durable signal/error evidence that consolidation can consume.
- Source snapshot contracts for Process and Workflow data must be available or explicitly staged.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Persistence\Entities\ProcessRuntimeModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\06-consolidation-engine.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\ConsolidationContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\13-operational-modes-and-scale.md

## Deliverables

- Consolidation run model and scheduler contract.
- Episode/procedure/decision/reflection extraction rules.
- Review candidate creation for ambiguous, contradictory, stale, or high-risk memory.
- Idempotent cursor and lease behavior.

## Dependency Impact

- Process and workflow sources feed consolidation through snapshots/events.
- Review UI consumes consolidation candidates.
- Distributed workers later consume deterministic job packages.

## Validation Depth

- Unit tests for idempotent extraction and duplicate suppression.
- Integration tests for run resume, failure recording, and review handoff.
- EF/performance tests for bounded batches, cursor advancement, stale projection marking, and safe bulk state transitions.
- Score geometry tests proving consolidation updates vectors/traces rather than direct scalar boosts/penalties.

## Implementation Steps

- Define consolidation modes and run state.
- Extract process/workflow episodes from source snapshots.
- Detect contradictions and stale records.
- Emit review items and projection updates through authoritative services.

## Do Not Do

- Do not overwrite human-validated memory automatically.
- Do not advance cursors before accepted writes are durable.
- Do not allow generated procedures to become active without risk policy.

## Acceptance Checklist

- Consolidation can retry safely.
- Review-required output is not active until accepted.
- Every generated record keeps source evidence, algorithm version, and run id.
- Activation, contradiction, and review priority changes are backed by score evaluation traces.

## Closure Proof

- `dotnet build .\src\CanDoItAll.Modules.CognitiveMemory\CanDoItAll.Modules.CognitiveMemory.csproj --no-restore` passed with zero warnings.
- `dotnet build .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore` passed with zero warnings.
- `dotnet build .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore` passed with zero warnings after rerunning serially; the first parallel run hit an output-file lock in a shared dependency, not a code error.
- `dotnet build .\src\CanDoItAll.Migrations.Sqlite\CanDoItAll.Migrations.Sqlite.csproj --no-restore` passed with zero warnings.
- `dotnet build .\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --no-restore` passed with zero warnings.
- `dotnet ef migrations has-pending-model-changes` passed for SQLite and PostgreSQL; both reported no model changes after the consolidation migration once EF rebuilt the migration assemblies.
- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~CognitiveMemoryConsolidationEngineTests"` passed 3/3.
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~CognitiveMemoryConsolidationPersistenceModelTests"` passed 2/2.
- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~CognitiveMemory"` passed 66/66.
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~CognitiveMemory"` passed 20/20.
- `dotnet build .\CanDoItAll.slnx --no-restore` passed with zero warnings.
- Static grep proof found no local final-score, `Dictionary<string,double>`, direct write/upsert, direct truth mutation, canonical approval, or active-memory overwrite surfaces under consolidation/scoring.
- .NET hot-path performance scan found no critical issues after tightening bounded list capacities, score evidence ref construction, score shape reuse, and deterministic hash construction.

## Deviations

- Browser proof remains not applicable for this backend-only phase and is still owned by `08-human-review-ui`.
- Temporal replay, procedural skill memory/simulation, review UI, MAF integration, probing, answer gate, Epistemic Drive, cross-project promotion, and distributed compute behavior remain downstream work.

## Proof Required

- Consolidation unit and integration tests.
- Failure/retry evidence.
- Review handoff evidence.

## Browser Validation Logging

- Browser proof starts when review/consolidation UI is added.
- Record consolidation queue and review queue screenshots in `08-human-review-ui`.

## Progression Gate

- Proceed to temporal replay next. Proceed to procedure mining, review UI, MAF integration, probing, Epistemic Drive, cross-project memory, or distributed compute only after their prerequisite gates close.
- Reopen consolidation if replay/procedure phases require generated outputs to become active without mutation authority, review policy, source evidence, or score traces.

## Suggested Agent Prompt

- Implement the consolidation engine with explicit modes, source evidence, idempotency, and review gates.
