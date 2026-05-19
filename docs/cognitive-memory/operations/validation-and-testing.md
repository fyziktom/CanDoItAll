# Validation And Testing

## Targeted Commands

Run from the repository root.

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
```

Use the Playwright project when UI behavior changes:

```powershell
dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
```

Run the solution build when persistence, API, or shared contracts change:

```powershell
dotnet build CanDoItAll.slnx --no-restore -m:1 --verbosity:minimal
```

## Current Coverage Shape

| Layer | Coverage today |
| --- | --- |
| Unit | Foundation guards, source ingestion, score geometry, signal ledger, taxonomy, recall, consolidation, review UI service, operational settings, procedure memory, temporal replay, workspace attention, advanced services, module registration, projection adapters. |
| Integration | SQLite/PostgreSQL persistence model coverage for foundation, source ingestion, score geometry, signals, taxonomy, recall, consolidation, temporal replay, workspace, procedural, neuro foundation, advanced records. |
| Component | Cognitive Memory page coverage. |
| Playwright | Review UI browser proof. |

## Evidence From Previous Closure

The latest completed Cognitive Memory repair bundles record:

- Unit Cognitive Memory tests: 117/117 passed.
- Integration Cognitive Memory tests: 25/25 passed.
- Component Cognitive Memory tests: 1/1 passed.
- Serial solution build passed with existing unrelated `Google.Protobuf` warnings.
- Live PostgreSQL validation exercised settings, staged source ingestion, consolidation, review approval/rejection, recall, probe sessions, Epistemic Drive scans, and local Ollama validation.

## Gaps To Add Before Beta

- Projection rebuild worker/API tests proving stale projection records become fresh provider points.
- Scheduled automation tests proving persisted schedule settings actually trigger ingestion/consolidation.
- MAF context fail/skip policy tests for process-critical runs.
- API DTO versioning/contract tests for agent-safe context output versus diagnostic traces.
- Browser coverage after splitting the large Cognitive Memory page.
- Load/performance tests for large source manifests, recall trace retention, and review queues.

