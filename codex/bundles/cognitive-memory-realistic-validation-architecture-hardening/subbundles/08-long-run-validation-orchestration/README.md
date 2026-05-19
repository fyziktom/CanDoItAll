# 08-long-run-validation-orchestration

## Status

- `Ready`

## Objective

Support longer Cognitive Memory validation runs with resumable cycles and controlled approval checkpoints.

## Required Edits

- Add validation cycle IDs and resumable cursors.
- Record per-cycle ingestion, consolidation, dreaming, approval, probe, recall, and projection metrics.
- Add stop criteria and follow-up trouble generation.

## Closure Proof

- Multi-cycle validation proof shows progress across cycles.
- Workbook includes accepted/rejected memories, recall traces, probe feedback, and unresolved architecture findings.

## Covered Inputs

- Long-running validation needs repeatable cycles, approval checkpoints, operation IDs, metrics, and trouble records without overloading context.

## Prerequisites

- Source truth, policy preservation, dream quality, probe recall, and projection diagnostics must be stable enough for repeated cycles.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Operations\CognitiveMemoryOperationalContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Operations\CognitiveMemoryScheduledAutomationRunner.cs`

## Deliverables

- Resumable validation cycles with cursoring, bounded budgets, per-cycle metrics, approval gates, and follow-up trouble generation.

## Dependency Impact

- This subbundle consumes outputs from all earlier subbundles and produces final validation confidence or follow-up architecture work.

## Validation Depth

- Unit tests should cover cycle bounds and cursor continuation; realistic proof requires repeated PostgreSQL/Qdrant runs and workbook capture.

## Implementation Steps

- Add cycle IDs and cursors, bound each operation, persist metrics, enforce approval checkpoints, and record unresolved findings.

## Do Not Do

- Do not create unbounded background loops or auto-approve generated memories.

## Acceptance Checklist

- Each cycle has a durable operation ID and cursor.
- Metrics distinguish ingestion, consolidation, dreaming, approval, probe, recall, and projection work.

## Proof Required

- Focused orchestration tests and a completed validation workbook from the long-run environment.

## Browser Validation Logging

- Record large-screen cycle dashboard or operations UI proof when the UI surface is changed.

## Progression Gate

- Close only after repeated validation cycles produce auditable metrics, approval decisions, recall traces, and unresolved finding rows.

## Suggested Agent Prompt

- Implement Cognitive Memory long-run validation orchestration with bounded resumable cycles, approval checkpoints, metrics, and follow-up trouble capture.
