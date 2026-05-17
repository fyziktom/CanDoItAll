# API stage loader and cycle observation

## Status

- `Completed`

## Objective

- Create a fresh PostgreSQL validation database, load each staged source wave through APIs, force ingestion/consolidation after each stage, and capture cycle evidence before review decisions.

## Success Criteria

- A new PostgreSQL database is active and recorded.
- Each stage is loaded through APIs only.
- Each stage forces project/process ingestion and consolidation/dreaming cycle.
- Each stage captures status, ingestion operation, consolidation, snapshot, pending review, and candidate-preview evidence.

## Covered Inputs

- R1 PostgreSQL-isolated multi-cycle runtime.
- R4 API-only staged loading.
- R5 forced memory/dreaming cycles.

## Prerequisites

- Subbundle 01 closure gate passed.
- Previous Cognitive Memory API skill is available.

## Exact Source References

- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\sample-data\source-manifest.json`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\sample-data\staged-sources`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\sample-data\trackers\cognitive-memory-demo-source-tracker.xlsx`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs`
- `C:\Users\lucys\.codex\skills\candoitall-api-cognitive-memory\SKILL.md`

## Deliverables

- API loader script or documented command sequence.
- Per-stage evidence folders.
- Fresh PostgreSQL database details.
- Cycle snapshots before review.

## Dependency Impact

- Review quality and chat validation depend on these cycle snapshots. If a stage is not actually processed, later proof can only show stale memory state.

## Validation Depth

- Critical execution foundation.

## Implementation Steps

1. Start or restart the app against a fresh PostgreSQL database named for this bundle.
2. Verify `GET /api/cognitive-memory/database/selection` reports PostgreSQL.
3. For each stage from S01 to S04, upload each staged source file through external-source APIs.
4. Create or update Markdown project asset nodes for the staged source, especially S04 email/instruction packets.
5. Force project-structure ingestion and process ingestion where appropriate.
6. Force consolidation/dreaming cycle for each affected project.
7. Capture snapshots before review decisions.
8. Record per-stage evidence paths in the execution report.

## Scope Exceptions

- This phase observes candidates before approval. It does not make final review decisions; that belongs to Subbundle 03.

## Do Not Do

- Do not write directly to Cognitive Memory tables.
- Do not reuse the prior `_12` database for closure proof.
- Do not skip forced consolidation because automatic scheduling exists.
- Do not bulk-load all stages before observing stage-by-stage behavior.

## Acceptance Checklist

- Completed: Fresh PostgreSQL database is active.
- Completed: Stage 1 load and forced cycle evidence exists.
- Completed: Stage 2 load and forced cycle evidence exists.
- Completed: Stage 3 load and forced cycle evidence exists.
- Completed: Stage 4 load and forced cycle evidence exists.
- Completed: Candidate previews are captured before review decisions.

## Proof Required

- API status and database selection JSON.
- Per-stage external ingestion operation JSON.
- Per-stage project/process ingestion JSON.
- Per-stage consolidation run JSON.
- Per-stage snapshot JSON.
- Execution report rows listing evidence paths.

## Browser Validation Logging

- Target route: `/cognitive-memory`.
- Required viewport: desktop large enough to inspect Review queue after each stage.
- Actions: open Cognitive Memory, open Review queue, verify candidate previews exist for stage-loaded records.
- Screenshots: one review queue screenshot after at least S03 and one after S04.
- Review question: can a human see proposed memory text, source excerpt, and stage-specific source locator before making a decision?

## Progression Gate

- Subbundle 03 may start only after all four stages have forced-cycle snapshots and candidate previews captured.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Use a fresh PostgreSQL database. Load one stage at a time through APIs, force ingestion/consolidation after that stage, capture snapshots and candidate previews, then stop before review decisions. Record evidence paths in reviews/01-execution-report.md. If an API is missing or stage processing cannot be forced, create a repair subbundle before proceeding.
```
