# 06 Consolidation Engine

## Status

- Ready after taxonomy, projections, and initial recall.

## Objective

- Add resumable consolidation jobs that create/refine episodic, procedural, decision, reflection, contradiction, and review candidates.

## Covered Inputs

- Requirements FR-012, FR-013, FR-014, FR-015, FR-016, FR-020, FR-021, FR-022, and NFR-012.
- Consolidation architecture and process/workflow source audit.

## Prerequisites

- `04-memory-taxonomy-and-projections` must provide canonical memory and projection state.
- `01a-common-drivers-helpers-and-ef-guardrails` must provide lease, idempotency, batch, source-generated JSON, and EF bulk-operation guidance.
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

## Proof Required

- Consolidation unit and integration tests.
- Failure/retry evidence.
- Review handoff evidence.

## Browser Validation Logging

- Browser proof starts when review/consolidation UI is added.
- Record consolidation queue and review queue screenshots in `08-human-review-ui`.

## Progression Gate

- Proceed to distributed compute only after consolidation jobs are deterministic and idempotent.

## Suggested Agent Prompt

- Implement the consolidation engine with explicit modes, source evidence, idempotency, and review gates.
