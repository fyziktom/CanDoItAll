# Cognitive memory automation settings and ingestion UI

## Status

- `Completed`

## Objective

- Add persisted Cognitive Memory automation settings plus UI/API controls for project, process, file, and website-link ingestion with visible progress/status.

## Success Criteria

- Automation settings support manual-only, nightly, idle-based, and scheduled-moments modes.
- Settings can be read and saved through API/service boundaries.
- Cognitive Memory UI has Settings and Sources tabs.
- Settings tab can trigger project/process ingestion and shows progress/status.
- Sources tab can ingest uploaded files and website links and shows progress/status.

## Covered Inputs

- R3 Memory automation settings.
- R4 Manual source ingestion controls.
- R5 External source ingestion.

## Prerequisites

- Subbundle 01 complete enough that new API handlers can be validated against PostgreSQL.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Ingestion\CognitiveMemorySourceIngestionService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Foundation\CognitiveMemoryEntities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\CognitiveMemoryModuleServiceCollectionExtensions.cs`

## Deliverables

- Persisted automation settings entity/service/contracts.
- External source ingestion service and API endpoints.
- Cognitive Memory UI Settings tab.
- Cognitive Memory UI Sources tab.
- Focused tests for service/API behavior.

## Dependency Impact

- Subbundle 03 depends on the external source ingestion API to load sample documents and mindmaps. Weak UI proof would leave the user unable to manually exercise the new behavior.

## Validation Depth

- UI, component-test, and browser-proof.

## Implementation Steps

1. Add settings and external ingestion contracts/entities/services.
2. Register services and update persistence configuration/migrations.
3. Add API endpoints for settings, manual source ingestion triggers, file ingestion, and URL ingestion.
4. Extend the Cognitive Memory page with Settings and Sources tabs.
5. Add focused tests.
6. Run browser proof for the new tabs.

## Scope Exceptions

- Full unattended background scheduling is not required if the persisted settings contract and manual trigger controls are complete; a later scheduler can consume the same settings.

## Do Not Do

- Do not store validation sample data inside automated test code.
- Do not bypass Cognitive Memory source/evidence records for external ingestion.
- Do not add broad UI framework changes unrelated to Cognitive Memory.

## Acceptance Checklist

- Completed: API returns and saves settings.
- Completed: API ingests file/link content.
- Completed: UI tabs render with controls and progress/status.
- Completed: Manual project/process actions call existing ingestion services.

## Proof Required

- Focused .NET tests passed.
- Large-screen browser screenshot for Settings tab: `validation/evidence/20260517-085609/cognitive-memory-settings-desktop.png`.
- Large-screen browser screenshot for Sources tab: `validation/evidence/20260517-085609/cognitive-memory-sources-desktop.png`.
- Narrow viewport screenshot/layout proof: `validation/evidence/20260517-085609/cognitive-memory-sources-mobile.png`.

## Browser Validation Logging

- Target route: Cognitive Memory page.
- Viewports: desktop and narrow.
- Assertions: Settings tab visible, Sources tab visible, progress/status elements visible, no obvious overlap.
- Screenshot artifacts must be recorded in `reviews/01-execution-report.md`.

## Progression Gate

- Downstream sample-data loading may continue only after the external source ingestion API is usable and the UI tabs pass browser proof.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
