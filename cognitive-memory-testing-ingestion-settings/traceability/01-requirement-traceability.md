# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| R1 PostgreSQL-first runtime | `requirements/01-normalized-requirements.md` | `subbundles/01-database-source-setup-api-and-postgresql-runtime-alignment`, `subbundles/03-api-loaded-test-data-and-live-postgresql-instance` | launch settings diff, live app connection string, API status | SQLite is not acceptable for closure proof. |
| R2 Cognitive Memory database setup API | `requirements/01-normalized-requirements.md` | `subbundles/01-database-source-setup-api-and-postgresql-runtime-alignment` | API route proof and tests | Reuse existing database profile services. |
| R3 Memory automation settings | `requirements/01-normalized-requirements.md` | `subbundles/02-cognitive-memory-automation-settings-and-ingestion-ui` | service/API tests and UI browser proof | Store settings persistently. |
| R4 Manual source ingestion controls | `requirements/01-normalized-requirements.md` | `subbundles/02-cognitive-memory-automation-settings-and-ingestion-ui` | UI browser proof and service/API proof | Project and process ingestion required. |
| R5 External source ingestion | `requirements/01-normalized-requirements.md` | `subbundles/02-cognitive-memory-automation-settings-and-ingestion-ui`, `subbundles/03-api-loaded-test-data-and-live-postgresql-instance` | file/link API proof and live data load | Must show progress/status in UI. |
| R6 API-loaded test data | `requirements/01-normalized-requirements.md` | `subbundles/03-api-loaded-test-data-and-live-postgresql-instance` | bundle sample files and loader output | Sample data lives in bundle artifacts, not test code. |
| R7 Closure proof | `requirements/01-normalized-requirements.md` | all subbundles | execution report, screenshots, live URL | App must be left running. |
