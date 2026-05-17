# Structured Input

## Core Objective

- Finish the operational closure of Cognitive Memory v2 for PostgreSQL-backed manual development and testing.

## Success Criteria

- Cognitive Memory has database setup APIs, automation settings APIs, source ingestion APIs, and matching UI controls.
- The user can configure sorting timing, manually ingest project/process data, upload a file, and ingest a website link.
- Visible status/progress is shown for ingestion actions.
- A PostgreSQL database is created, sample data is loaded through APIs, and the app is left running against it.

## Hard Constraints

- Use PostgreSQL as the primary development and validation database.
- Do not embed sample validation data in automated test code.
- Load sample data only through APIs.
- Set Visual Studio launch settings to the same database used by the live instance.

## Allowed Side Effects

- Update Cognitive Memory API, services, persistence, migrations, UI, tests, launch settings, bundle artifacts, and local skill documentation if needed.

## Source Artifacts

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Ingestion\CognitiveMemorySourceIngestionService.cs`

## Input Coverage Signals

- Database setup API must be added before UI/testing because the user explicitly called it missing.
- UI settings and external source tabs are both required; neither can be treated as optional polish.
- API-loaded sample data and the live process are closure requirements, not future work.

## Dependency And Sequencing Signals

- Database setup API unlocks PostgreSQL runtime alignment.
- Settings/source services unlock UI controls and API-loaded sample data.
- Browser proof and live instance proof depend on all prior implementation.

## Validation Expectations

- Focused .NET tests for new services/API behavior.
- Browser proof for the new tabs and visible progress/status.
- API smoke proof after loading sample data into PostgreSQL.

## Evidence Contract

- Test command output.
- PostgreSQL database name and connection string.
- Loader script output.
- Browser screenshot paths.
- Final live URL and process details.

## UI Validation Strategy

- Open the Cognitive Memory page on a large viewport and verify the Settings and Sources tabs.
- Capture screenshots for the new tabs.
- Repeat at a narrower viewport to catch layout overflow.

## Browser Validation Analytics

- Target route: Cognitive Memory page.
- Required viewport passes: desktop and narrow-width.
- Assertions: tabs visible, controls visible, progress/status area visible, no obvious overlap.

## Working Assumptions

- Existing database profile services should be reused rather than duplicating connection switching logic.
- External source ingestion should write standard source/evidence records.

## Primary Risks

- EF model/migration churn could affect multiple providers.
- UI proof requires a healthy app startup against the new PostgreSQL database.
