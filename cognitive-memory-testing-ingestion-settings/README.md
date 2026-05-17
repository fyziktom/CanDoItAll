# Cognitive memory testing ingestion settings

This bundle is a coordination and execution package for `cognitive-memory-testing-ingestion-settings`.

## Profile

- `initiative`

## Mission

- Close the remaining Cognitive Memory v2 operational gaps by adding PostgreSQL-first database setup APIs, source ingestion controls, automation settings, a user-visible ingestion UI, API-loaded sample data, and a live PostgreSQL-backed instance for manual testing.

## Outcome Contract

- Requested outcome: Cognitive Memory can be configured, populated, and exercised through developer APIs and the UI without falling back to SQLite.
- Hard constraints: use PostgreSQL for development and validation, load sample data via APIs only, keep test data out of automated test code, and leave the app running against the same database configured for Visual Studio.
- Evidence required before closure: focused automated tests, API proof, browser/UI proof, sample-data load proof, launch settings update, and a running local URL with the database connection details.
- Known blockers or explicit scope exceptions: no known blockers at preparation time.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-database-source-setup-api-and-postgresql-runtime-alignment`
2. `subbundles/02-cognitive-memory-automation-settings-and-ingestion-ui`
3. `subbundles/03-api-loaded-test-data-and-live-postgresql-instance`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed`

## Closure Evidence

- PostgreSQL database: `candoitall_cognitive_memory_followup_20260517_12`
- Live app URL: `http://localhost:5032/cognitive-memory`
- Loader evidence: `validation/evidence/20260517-115640/99-summary.json`
- Memory quality analysis: `validation/evidence/20260517-115640/95-memory-quality-analysis.json`
- Post-approval recall evidence: `validation/evidence/20260517-115640/94-fieldops-recall-after-approval.json`
- Final PostgreSQL status evidence: `validation/evidence/20260517-115640/92-final-status.json`
- Browser evidence:
  - `validation/evidence/20260517-085609/cognitive-memory-settings-desktop.png`
  - `validation/evidence/20260517-085609/cognitive-memory-sources-desktop.png`
  - `validation/evidence/20260517-085609/cognitive-memory-sources-mobile.png`
  - `validation/evidence/20260517-115640/96-cognitive-memory-review-preview-postgresql.png`
