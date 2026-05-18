# Cognitive Memory Ingestion And Consolidation Validation

## Status

- Status: `Ready`

## Objective

- Ingest the staged project structures and source chunks into Cognitive Memory, run consolidation, and make source-truth based review decisions.

## Success Criteria

- Each stage uploads a markdown source chunk as an external source.
- Each stage triggers project-structure ingestion.
- Each stage triggers consolidation with a developer validation profile.
- Snapshots are saved before and after review decisions.
- Review items are approved, rejected, or deferred according to source-truth rules.

## Covered Inputs

- Stage source chunks generated from source-truth markdown.
- Project-structure nodes and links from subbundle 02.
- Cognitive Memory status, settings, ingestion, consolidation, snapshot, and review APIs.

## Prerequisites

- Subbundle 02 readback evidence exists.
- PostgreSQL-backed Cognitive Memory is active unless the run is explicitly marked as non-PostgreSQL.
- External-source upload endpoint is available.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\validation\load-realistic-project-memory-validation.ps1
- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\source-truth\source-manifest.json
- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\source-truth\ai-tap-time-sliced.md
- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\source-truth\curacao-glass-time-sliced.md

## Deliverables

- External source upload evidence.
- Ingestion run evidence.
- Consolidation run evidence.
- Snapshot evidence before and after review decisions.
- Review-decision evidence and summary counts.

## Dependency Impact

- Recall validation depends on valid source-backed memories being approved or otherwise available.
- If consolidation creates low-quality duplicate candidates, subbundle 04 must identify whether review policy or implementation behavior is responsible.

## Validation Depth

- End-to-end memory pipeline validation.

## Implementation Steps

1. Verify Cognitive Memory status and settings.
2. Upload stage source chunks.
3. Ingest project structure for the active project and stage.
4. Run consolidation.
5. Snapshot pending review items.
6. Apply source-truth based decisions.
7. Snapshot again and save cycle evidence.

## Scope Exceptions

- No C# repair happens in this subbundle unless ingestion or consolidation cannot run at all and the failure is already proven as an app bug.

## Do Not Do

- Do not approve candidates blindly.
- Do not suppress consolidation or review failures.
- Do not treat empty snapshots as success without checking preceding API responses.

## Acceptance Checklist

- Run summary includes settings, ingestion, consolidation, snapshot, and review-decision data.
- Review decision counts are present.
- No unhandled API error stops before evidence is saved.

## Proof Required

- `validation/evidence/<runId>/*-external-file.json`.
- `validation/evidence/<runId>/*-ingest-project-structure.json`.
- `validation/evidence/<runId>/*-consolidation.json`.
- `validation/evidence/<runId>/*-snapshot-before-review.json`.
- `validation/evidence/<runId>/*-snapshot-after-review.json`.

## Browser Validation Logging

- N/A. API evidence replaces browser validation.

## Progression Gate

- Recall probing may start only after every stage has ingestion, consolidation, snapshot, and review-decision evidence or a clearly recorded blocking failure.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Run staged ingestion and consolidation, make explicit source-truth review decisions, capture before/after snapshots, and stop if the memory provider or API state invalidates the run.
```
