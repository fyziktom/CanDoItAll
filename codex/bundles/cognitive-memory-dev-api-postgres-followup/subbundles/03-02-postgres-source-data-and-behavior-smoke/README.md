# 02-postgres-source-data-and-behavior-smoke

## Status

- `Completed`

## Objective

Create realistic source data, load it through APIs into a fresh PostgreSQL profile, and smoke Cognitive Memory ingestion/consolidation/snapshot/recall readiness.

## Success Criteria

- Active profile is PostgreSQL.
- Sample projects are created through project-structure APIs.
- Markdown and mermaid source assets are attached to projects.
- Cognitive Memory ingestion and consolidation run for each project.
- Snapshot summary is captured.
- Recall succeeds or returns an explicit provider-unavailable response.

## Covered Inputs

- R1 PostgreSQL-first gate.
- R5 Sample source data.
- R6 Behavior smoke.
- R7 Explicit limitations.

## Prerequisites

- Developer API and skill are installed.
- Web app is running in `Development`.
- PostgreSQL is available.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-dev-api-postgres-followup\sample-source-data`
- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-dev-api-postgres-followup\sample-source-data\Load-CognitiveMemorySamples.ps1`

## Deliverables

- Six sample projects with detailed structures and mindmaps.
- API loader script.
- Evidence JSON under `evidence/`.

## Dependency Impact

- This phase proves whether the API can operate the implemented memory stack against realistic PostgreSQL-backed source data.

## Validation Depth

- End-to-end smoke with explicit provider limitation handling.

## Implementation Steps

1. Create a dedicated PostgreSQL database.
2. Start the web app in Development.
3. Activate the PostgreSQL profile through the dev endpoint.
4. Read the installed skill before testing.
5. Run the sample loader.
6. Attempt recall and capture response.
7. Save evidence.

## Scope Exceptions

- Vector projection rebuild may be disabled for relational smoke if RAG providers are not configured.

## Do Not Do

- Do not insert data directly into database tables.
- Do not put source data into test code.
- Do not accept SQLite behavior-smoke evidence.

## Acceptance Checklist

- PostgreSQL status evidence exists.
- Project ids and node/link counts are captured.
- Ingestion and consolidation responses are captured.
- Snapshot summary is captured.
- Recall readiness is captured.

## Proof Required

- API response evidence JSON.
- Command output in execution report.

## Browser Validation Logging

- N/A.

## Progression Gate

- Final closure requires PostgreSQL smoke evidence or a concrete blocker with exact failing command and response.

## Suggested Agent Prompt

```text
Implement this subbundle only. Verify PostgreSQL through the skill, load bundle sample data through APIs, capture smoke evidence, and record explicit provider limitations.
```
