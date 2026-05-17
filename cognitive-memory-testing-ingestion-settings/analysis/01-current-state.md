# Current State

## Existing Implementation

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs` already exposes status, snapshot, source ingestion, consolidation, recall, review, probe, self-regulation, answer gate, professor review, epistemic drive, cross-project, and distributed memory endpoints.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs` already has database profile endpoints under `/_dev/database`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Ingestion\CognitiveMemorySourceIngestionService.cs` supports project structure, process runtime, and workflow runtime ingestion.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor` has operational Cognitive Memory tabs, but no settings tab and no external source ingestion tab.
- The previous bundle already added advanced Cognitive Memory services and PostgreSQL smoke evidence, but the live manual-testing path is not fully closed.

## Gaps

- Cognitive Memory has no scoped database setup API even though database controls exist elsewhere.
- Automation timing/sorting settings are not persisted or exposed in the Cognitive Memory UI.
- External file/link ingestion is missing from the Cognitive Memory API and UI.
- Manual sample data needs to be loaded through APIs into a PostgreSQL database that the user can reuse from Visual Studio.
