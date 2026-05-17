# Target Solution

## API Surface

- Extend `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs` with Cognitive Memory database setup routes that reuse existing database profile services.
- Add settings and external ingestion routes to the same API group so the skill/developer API has one memory-focused control plane.

## Application Services

- Add a small Cognitive Memory settings service for persisted automatic sorting/consolidation preferences.
- Add an external source ingestion service that writes source manifest/item/evidence records and records status/progress.
- Reuse `ICognitiveMemorySourceIngestionService` for project/process ingestion actions.

## UI

- Extend `CognitiveMemoryPage` with:
  - Settings tab for automatic sorting mode and ingestion buttons.
  - Sources tab for file and URL ingestion with visible progress/status.
- Keep logic in the code-behind and service layer; the Razor file should remain primarily rendering/orchestration.

## Validation Data

- Store sample data as markdown/mermaid files under this bundle.
- Use a PowerShell loader that calls the running app APIs to load data into PostgreSQL.
