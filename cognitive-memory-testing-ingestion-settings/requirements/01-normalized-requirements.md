# Normalized Requirements

## R1 PostgreSQL-First Runtime

All implementation and validation work for this follow-up must run against PostgreSQL. Visual Studio launch settings and the live app instance must reference the same PostgreSQL database.

Success criteria:
- The final validation database name and connection string are recorded.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Properties\launchSettings.json` is updated for the same database.
- No follow-up proof depends on SQLite.

## R2 Cognitive Memory Database Setup API

The Cognitive Memory developer API must expose database source/profile setup operations equivalent to the existing development database controls.

Success criteria:
- API routes expose current selection, PostgreSQL profile setup, and profile switching.
- Route inventory/status includes the new database setup surface.
- Tests or API proof confirm the routes are reachable.

## R3 Memory Automation Settings

Cognitive Memory must persist settings for automatic sorting/consolidation timing.

Success criteria:
- Supported modes include nightly, idle-based, exact scheduled moments, and manual-only.
- Settings can be read and saved through API/service boundaries.
- The UI renders and saves those settings.

## R4 Manual Source Ingestion Controls

The user must be able to start ingestion from projects and processes from the Cognitive Memory UI.

Success criteria:
- Buttons initiate project structure and process runtime ingestion using the selected scope/options.
- The UI shows status and progress feedback.
- The implementation reuses existing ingestion services.

## R5 External Source Ingestion

The user must be able to ingest an uploaded file or website link from the Cognitive Memory UI and API.

Success criteria:
- File upload and URL ingestion APIs create source records/evidence that Cognitive Memory can consolidate.
- The UI exposes file and URL input controls.
- The UI shows progress/status during and after ingestion.

## R6 API-Loaded Test Data

Representative development, SaaS planning, Docker analysis, economy, and non-programming source data must be stored as bundle documents/mermaid mindmaps and loaded via APIs only.

Success criteria:
- Bundle artifacts contain detailed source documents/mindmaps.
- A loader script calls local HTTP APIs to create/load data.
- The live database contains the loaded sample sources.

## R7 Closure Proof

The bundle cannot close until automated tests, API smoke proof, browser proof, and live instance proof are captured.

Success criteria:
- Focused tests pass or any failure is documented with a clear blocker.
- Browser screenshots/checks cover the new Cognitive Memory tabs.
- The final app process is left running with the local URL recorded.
