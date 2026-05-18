# Source Artifacts

## Primary Existing Artifacts

- `C:\repositories\CanDoItAll\cognitive-memory-testing-ingestion-settings`
- `C:\repositories\CanDoItAll\cognitive-memory-testing-ingestion-settings\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\cognitive-memory-testing-ingestion-settings\validation\evidence\20260517-115640\95-memory-quality-analysis.json`
- `C:\repositories\CanDoItAll\cognitive-memory-testing-ingestion-settings\sample-data\sample-projects.md`
- `C:\repositories\CanDoItAll\cognitive-memory-testing-ingestion-settings\sample-data\sample-projects.mmd`
- `C:\repositories\CanDoItAll\cognitive-memory-testing-ingestion-settings\sample-data\sample-projects.structure.json`
- `C:\Users\lucys\.codex\skills\candoitall-api-cognitive-memory\SKILL.md`

## New Bundle Artifacts

- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\sample-data\staged-sources`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\sample-data\source-manifest.json`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\sample-data\trackers\cognitive-memory-demo-source-tracker.xlsx`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\validation\scripts\build-demo-corpus.mjs`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\validation\scripts\verify-demo-tracker.mjs`

## Execution API Surface

- `GET /api/cognitive-memory/status`
- `GET /api/cognitive-memory/database/selection`
- `GET /api/cognitive-memory/settings`
- `POST /api/cognitive-memory/external-sources/files`
- `GET /api/cognitive-memory/external-sources/ingestions/{operationId}`
- `POST /api/cognitive-memory/ingestion/project-structure`
- `POST /api/cognitive-memory/ingestion/processes`
- `POST /api/cognitive-memory/consolidation/runs`
- `GET /api/cognitive-memory/snapshot`
- `POST /api/cognitive-memory/review-items/{reviewItemId}/decisions`
- `POST /api/cognitive-memory/recall`

## Chat Validation Surfaces

- Existing CanDoItAll agent/chat surfaces must be discovered during execution.
- If no stable chat API exists for source-backed project memory validation, execution must record that blocker and create a repair subbundle instead of replacing the test with a direct recall-only smoke.
