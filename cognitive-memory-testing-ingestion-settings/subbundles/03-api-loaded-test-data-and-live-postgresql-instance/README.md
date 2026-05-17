# API-loaded test data and live PostgreSQL instance

## Status

- `Completed`

## Objective

- Create rich Cognitive Memory sample source documents, load them through APIs into a new PostgreSQL database, and leave the app running for user testing.

## Success Criteria

- Sample documents and mermaid mindmaps exist under the bundle.
- Loader script uses HTTP APIs only.
- PostgreSQL database contains loaded source data.
- App is left running against the same database configured in Visual Studio launch settings.
- Final API smoke, review approval, recall, and browser proof are recorded.

## Covered Inputs

- R1 PostgreSQL-first runtime.
- R5 External source ingestion.
- R6 API-loaded test data.
- R7 Closure proof.

## Prerequisites

- Subbundle 01 database setup API complete.
- Subbundle 02 external source ingestion API and UI complete.

## Exact Source References

- `C:\repositories\CanDoItAll\cognitive-memory-testing-ingestion-settings\sample-data`
- `C:\repositories\CanDoItAll\cognitive-memory-testing-ingestion-settings\validation`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Properties\launchSettings.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`

## Deliverables

- Sample development, SaaS, Docker, economy, and non-programming documents/mindmaps.
- API loader script.
- PostgreSQL database creation/loading proof.
- Running app URL and process details.

## Dependency Impact

- This is the closure phase. Weak proof means the user cannot confidently start manual testing from either the live instance or Visual Studio.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Create sample-data markdown and mermaid artifacts.
2. Create loader script that calls Cognitive Memory APIs.
3. Create or configure the PostgreSQL validation database.
4. Start the app against the validation database.
5. Run loader script through APIs.
6. Run API smoke checks and browser validation.
7. Leave the app process running and record URL/database details.

## Scope Exceptions

- none

## Do Not Do

- Do not insert sample data directly into the database.
- Do not put sample data into automated tests.
- Do not stop the final app instance.

## Acceptance Checklist

- Completed: Loader output lists successful API calls.
- Completed: API status/snapshot shows ingested data and approved FieldOps memory records.
- Completed: Browser can open the Cognitive Memory page.
- Completed: Final response records URL and PostgreSQL connection string.

## Proof Required

- Loader evidence: `validation/evidence/20260517-115640/99-summary.json`.
- API smoke evidence: `validation/evidence/20260517-115640/92-final-status.json`.
- Review approval evidence: `validation/evidence/20260517-115640/93-fieldops-review-approvals.json`.
- Recall smoke evidence: `validation/evidence/20260517-115640/94-fieldops-recall-after-approval.json`.
- Memory quality evidence: `validation/evidence/20260517-115640/95-memory-quality-analysis.json`.
- Browser screenshots: `validation/evidence/20260517-085609/cognitive-memory-settings-desktop.png`, `validation/evidence/20260517-085609/cognitive-memory-sources-desktop.png`, and `validation/evidence/20260517-085609/cognitive-memory-sources-mobile.png`.
- Review queue screenshot: `validation/evidence/20260517-115640/96-cognitive-memory-review-preview-postgresql.png`.
- Live process id: `validation/live-app.pid`; URL: `http://localhost:5032/cognitive-memory`.

## Browser Validation Logging

- Target route: Cognitive Memory page on the live instance.
- Viewports: desktop and narrow.
- Assertions: loaded status visible, Settings and Sources tabs still usable, no layout overlap.
- Screenshot artifacts must be recorded in `reviews/01-execution-report.md`.

## Progression Gate

- Final closure may proceed only after the app is running against PostgreSQL and sample data has been loaded through APIs.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
