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

## P0 Validation Evidence

The P0 maintainability and operations pass was validated with:

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory|FullyQualifiedName~AgentContextContributionTests" --logger "console;verbosity=minimal" -m:1
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -m:1 --verbosity:minimal
```

Results:

- Unit Cognitive Memory and agent-context tests: 136/136 passed.
- Integration Cognitive Memory tests: 25/25 passed.
- Component Cognitive Memory tests: 1/1 passed.
- Web project build: passed with 0 warnings and 0 errors.

Browser proof was run because this pass changed rendered Blazor structure:

- Route: `http://127.0.0.1:5289/cognitive-memory`.
- Startup action: accepted the active database profile dialog.
- Desktop viewport: 1440x1000, settings tab rendered operational controls.
- Narrow viewport: 390x900, settings tab rendered operational controls with no horizontal overflow.
- Assertions: `cognitive-memory-settings`, `cognitive-memory-operational-actions`, `cognitive-memory-run-automation`, `cognitive-memory-rebuild-projections`, `cognitive-memory-automation-run-progress`, and `cognitive-memory-projection-rebuild-progress` were present.
- Console: only normal Blazor connection messages.
- Screenshots: `codex/bundles/cognitive-memory-p0-maintainability/reviews/browser-proof/cognitive-memory-settings-desktop-p0.png` and `codex/bundles/cognitive-memory-p0-maintainability/reviews/browser-proof/cognitive-memory-settings-mobile-p0.png`.

## Evidence From Previous Closure

The latest completed Cognitive Memory repair bundles record:

- Unit Cognitive Memory tests: 117/117 passed.
- Integration Cognitive Memory tests: 25/25 passed.
- Component Cognitive Memory tests: 1/1 passed.
- Serial solution build passed with existing unrelated `Google.Protobuf` warnings.
- Live PostgreSQL validation exercised settings, staged source ingestion, consolidation, review approval/rejection, recall, probe sessions, Epistemic Drive scans, and local Ollama validation.

## Gaps To Add Before Beta

- Live Qdrant/provider failure integration tests for projection rebuild.
- Hosted scheduler tests only if an autonomous scoped worker is introduced.
- API DTO versioning/contract tests for agent-safe context output versus diagnostic traces.
- Broader browser coverage for review, probe, ingestion, and projection operation flows.
- Load/performance tests for large source manifests, recall trace retention, and review queues.

