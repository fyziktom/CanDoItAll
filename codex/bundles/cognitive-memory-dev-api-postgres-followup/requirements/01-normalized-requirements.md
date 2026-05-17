# Normalized Requirements

## R1 PostgreSQL-First Development Gate

New cognitive-memory behavior testing must verify the active database profile is PostgreSQL before loading source data or running memory behavior smoke checks.

## R2 Previous-Bundle State Assessment

Document which `cognitive-memory-architecture-v2` phases are implemented, which remain unstarted, and which implementation areas need maintainability refactoring.

## R3 Developer API

Expose Cognitive Memory through `/api/cognitive-memory` using the same HTTP control-plane style as process and project-structure APIs.

Required routes:

- `GET /api/cognitive-memory/status`
- `GET /api/cognitive-memory/snapshot`
- `POST /api/cognitive-memory/sources/ingest`
- `POST /api/cognitive-memory/consolidation/runs`
- `POST /api/cognitive-memory/recall`
- `POST /api/cognitive-memory/review-items/{reviewItemId}/decisions`

## R4 Codex Skill

Install a local skill that instructs Codex to use the API and verify PostgreSQL before testing.

## R5 Sample Source Data

Create detailed markdown documents and mermaid mindmaps in the bundle, plus an API loader. The sample behavior data must not be embedded in automated test code.

## R6 Behavior Smoke

Create a fresh PostgreSQL database, activate it through the dev database endpoint, load sample project structures through project-structure APIs, ingest them into Cognitive Memory, run consolidation, read snapshots, and attempt recall.

## R7 Explicit Limitations

Record semantic/RAG provider absence as an explicit limitation if recall or vector projection is unavailable.
