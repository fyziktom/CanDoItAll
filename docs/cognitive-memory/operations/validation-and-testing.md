# Validation And Testing

Run these commands from `C:\repositories\CanDoItAll` unless the command explicitly changes repository.

## Release Gate

Generic memory runtime:

```powershell
dotnet test .\tests\Memory\CanDoItAll.Memory.Tests\CanDoItAll.Memory.Tests.csproj --no-restore --logger "console;verbosity=normal"
```

MAF memory integration:

```powershell
dotnet test .\tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~MemoryAgentRuntimeToolProviderTests|FullyQualifiedName~MemoryWorkflowExecutorTests|FullyQualifiedName~MemoryAgentContextContributorTests|FullyQualifiedName~MemoryMafIntegrationCheckpointTests" --logger "console;verbosity=normal"
```

Generic memory component UI:

```powershell
dotnet test .\tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~MemoryProvider|FullyQualifiedName~MemoryUiRefactoringCheckpoint" --logger "console;verbosity=normal"
```

Generic memory browser UI:

```powershell
dotnet test .\tests\Playwright\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --no-restore --filter "FullyQualifiedName~MemoryProviderManagementPlaywrightTests" --logger "console;verbosity=normal"
```

Database runtime switching:

```powershell
dotnet test .\tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~DatabaseSwitchIntegrationTests" --logger "console;verbosity=normal"
```

Native Cognitive Memory service:

```powershell
dotnet build C:\repositories\CanDoItAll.CognitiveMemory\CanDoItAll.CognitiveMemory.slnx --no-restore --verbosity:minimal
dotnet test C:\repositories\CanDoItAll.CognitiveMemory\tests\CanDoItAll.CognitiveMemory.Tests\CanDoItAll.CognitiveMemory.Tests.csproj --no-restore --logger "console;verbosity=normal"
```

Main solution:

```powershell
dotnet build .\CanDoItAll.slnx --no-restore --verbosity:minimal
```

Bundle validation:

```powershell
python .\codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py --profile initiative --stage completed --repo-root . .\codex\bundles\candoitall-memory-provider-extraction-bundle
```

## Source Audits

Base host and generic memory paths must not depend on the retained native module, Qdrant RAG driver, or SemanticCompletion driver:

```powershell
rg -n "CanDoItAll.Modules.CognitiveMemory|AddCognitiveMemoryModule|CognitiveMemoryModuleAssemblyMarker|CanDoItAll.AgentFramework.Rag.Qdrant|CanDoItAll.AgentFramework.SemanticCompletion.Driver" .\src\App\CanDoItAll.Composition .\src\Memory .\src\Modules\CanDoItAll.Modules.Memory .\src\Modules\CanDoItAll.Modules.AgentFramework -g "*.cs" -g "*.csproj" -g "*.razor"
```

Retained native references must be classified as one of:

- native service repository code;
- retained legacy main-repo native module code;
- retained legacy/native regression tests;
- legacy main DB export/retirement artifacts;
- historical documentation.

Any direct reference from base composition, generic memory runtime, generic memory UI, or MAF memory integration to native Cognitive Memory implementation types is a release blocker.

## Current Coverage Shape

| Layer | Coverage |
| --- | --- |
| Generic memory | Provider profiles, registry selection, operation handler, runtime service, ledgers, workers, feedback, provider events, Source Gateway, manual ingestion, HTTP driver, MCP driver, native remote adapter, deterministic mock driver, retention, host-composition guards, end-to-end observability proof. |
| MAF | Runtime tool provider, workflow executor, context contributor, source snapshot contracts, and process/workflow/source adapter paths. |
| UI components | `/memory` provider list, profile editor, zero-provider state, query/feedback/ingestion/operations/event surfaces, provider UI surface projection. |
| Playwright | Browser-visible `/memory` provider management, zero-provider state, query/context pack, feedback, manual ingestion, operations ledger, RCL/iframe fallback, and mobile checkpoints. |
| Integration | Database runtime switching with generic memory persistence registered. |
| Native service | Native repo solution build and native service tests. |
| Legacy main DB | Export service and no-op retirement migration coverage. |

## Browser Validation Policy

Run Playwright when a change touches:

- `/memory` UI or CSS;
- provider profile rendering or selection UX;
- provider UI surface projection;
- browser-visible zero-provider, feedback, ingestion, operations, or event behavior.

SB34 is documentation and release-gate work, so it does not require new screenshots unless a browser-visible source file changes. The final proof can reference the SB33 browser screenshots for full `/memory` behavior.

## Historical Native Validation

Older documents in this folder describe P0/P1 native Cognitive Memory validation, including Qdrant-backed recall and the legacy `/api/cognitive-memory` route family. Treat those as native-provider history. Current base-host release proof is the generic Memory Provider release gate above.

## Failure Handling

Do not continue the release gate after a failed command unless the failure is classified and fixed or explicitly deferred in the execution report with owner, risk, and follow-up bundle. Existing NuGet vulnerability/source warnings may be recorded as non-blocking only when the command exits successfully.
