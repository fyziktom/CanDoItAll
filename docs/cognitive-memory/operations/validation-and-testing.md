# Validation And Testing

## Targeted Commands

Run from the repository root.

```powershell
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
dotnet test tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
```

Use the Playwright project when UI behavior changes:

```powershell
dotnet test tests\Playwright\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
```

Run the solution build when persistence, API, or shared contracts change:

```powershell
dotnet build CanDoItAll.slnx --no-restore -m:1 --verbosity:minimal
```

## Current Coverage Shape

| Layer | Coverage today |
| --- | --- |
| Unit | Foundation guards, source ingestion, score geometry, signal ledger, taxonomy, recall, consolidation, review UI service, operator audit including retention cleanup run audit, operational settings, retention cleanup, procedure memory, temporal replay, workspace attention, advanced services, module registration, projection adapters, provider failure paths. |
| Integration | PostgreSQL persistence model coverage for foundation, source ingestion, score geometry, signals, taxonomy, recall, consolidation, temporal replay, workspace, procedural, neuro foundation, advanced records. |
| Component | Cognitive Memory page coverage. |
| Playwright | Review UI browser proof. |

## P1 Beta Qdrant Validation Evidence

The P1 beta closure pass was validated with Docker PostgreSQL/Qdrant and the public API path:

```powershell
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryOperationalServicesTests|FullyQualifiedName~CognitiveMemoryConsolidationEngineTests|FullyQualifiedName~CognitiveMemoryTaxonomyTests" --logger "console;verbosity=minimal" -m:1
dotnet build src\App\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -m:1 --verbosity:minimal
```

Results:

- Focused unit tests: 26/26 passed.
- Web project build: passed with 0 warnings and 0 errors.
- Docker status: `candoitall-qdrant` healthy on `6333/6334`, `candoitall-postgres` healthy on `5432`.
- Public API proof: uploaded an external Markdown source, consolidated 2 source items into 2 memory records, projected 2/2 missing durable records through `/api/cognitive-memory/v1/projections/rebuild`.
- Qdrant proof: collection `candoitall-knowledge` green, 384-dimensional cosine vectors, filtered points contain durable projection row ids and embedding profile `local-hashing-v1:dimension=384`.
- Recall proof: `/api/cognitive-memory/v1/recall` returned 2 selected candidates and vector stage `providerTrace = rag:qdrant:search:2`.
- Evidence file: `codex/bundles/cognitive-memory-beta-qdrant-validation/reviews/runtime-proof/qdrant-beta-live-proof.json`.

Browser proof:

- Route: `http://127.0.0.1:5289/cognitive-memory`.
- Startup profile dialog: explicit PostgreSQL startup override was shown and continued.
- Dashboard and health tab loaded at 1440x1000 and 390x900.
- Console: only normal Blazor startup/WebSocket info entries.
- Screenshots: `codex/bundles/cognitive-memory-beta-qdrant-validation/reviews/browser-proof/cognitive-memory-beta-desktop-loaded.png`, `cognitive-memory-beta-mobile-loaded.png`, `cognitive-memory-beta-health-desktop.png`, and `cognitive-memory-beta-health-mobile.png`.

## P1 Hardening Validation Evidence

The P1 beta-hardening pass was validated with:

```powershell
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryOperationalSettingsTests" --logger "console;verbosity=minimal" -m:1
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory|FullyQualifiedName~AgentContextContributionTests" --logger "console;verbosity=minimal" -m:1
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
dotnet test tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
dotnet build src\App\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -m:1 --verbosity:minimal
```

Results:

- External-source/settings unit focus: 10/10 passed.
- Unit Cognitive Memory and agent-context tests: 142/142 passed.
- Integration Cognitive Memory tests: 25/25 passed.
- Component Cognitive Memory tests: 1/1 passed.
- Web project build: passed with 0 warnings and 0 errors.
- V1 API contract smoke: `GET /api/cognitive-memory/v1/contract` returned version `v1`, base path `/api/cognitive-memory/v1`, 35 routes, 7 examples, and the retention cleanup route.

Browser proof was run because this pass changed the health tab:

- Route: `http://127.0.0.1:5289/cognitive-memory`.
- Desktop viewport: 1440x1000, health tab rendered `Operator audit` and `Mutation, evidence, and projection signals`.
- Narrow viewport: 390x900, health tab rendered the operator audit section without horizontal overflow in the captured snapshot.
- Console: only normal Blazor startup/WebSocket info entries in the fresh proof log.
- Screenshots: `codex/bundles/cognitive-memory-p1-beta-hardening/reviews/browser-proof/cognitive-memory-health-desktop-p1.png` and `codex/bundles/cognitive-memory-p1-beta-hardening/reviews/browser-proof/cognitive-memory-health-mobile-p1.png`.

## P0 Validation Evidence

The P0 maintainability and operations pass was validated with:

```powershell
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory|FullyQualifiedName~AgentContextContributionTests" --logger "console;verbosity=minimal" -m:1
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
dotnet test tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
dotnet build src\App\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -m:1 --verbosity:minimal
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

## Gaps To Add After P1 Beta

- Containerized CI or operator-owned scripted repeatability for the live Docker Qdrant proof.
- Hosted scheduler tests only if an autonomous scoped worker is introduced.
- External-client API contract compatibility tests for the v1 route surface.
- Broader browser coverage for review, probe, ingestion, and projection operation flows.
- Load/performance tests for large source manifests, recall trace retention, and review queues.

