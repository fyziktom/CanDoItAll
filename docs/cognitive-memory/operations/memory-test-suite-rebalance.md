# Memory Test Suite Rebalance

The provider extraction splits test ownership between the active generic Memory runtime and the retained legacy native Cognitive Memory module. Generic provider tests must not make native-module startup, Qdrant, or Semantic Completion dependencies part of the base-host contract.

## Generic Memory Suite

The generic provider suite lives in `tests/Memory/CanDoItAll.Memory.Tests`.

| Area | Primary tests |
| --- | --- |
| Provider protocol and registry | `MemoryProtocolContractsTests.cs`, `MemoryProviderRegistryTests.cs`, `MemoryTestSuiteRebalanceCheckpointTests.cs` |
| Runtime and ledgers | `MemoryRuntimeCheckpointTests.cs`, `MemoryRuntimePersistenceTests.cs`, `MemoryOperationHandlerTests.cs`, `MemoryAsyncWorkerTests.cs` |
| Source gateway | `MemorySourceGatewayTests.cs`, `MemorySourceGatewayHardeningCheckpointTests.cs`, `ManualMemorySourceIngestionTests.cs` |
| MAF integration | `MemoryAgentContextContributorTests.cs`, `MemoryOperationHandlerTests.cs` |
| Host composition guards | `HostCompositionDependencyRemovalTests.cs` |
| Native remote driver contract | `NativeRemoteMemoryProviderDriverTests.cs` |

`GenericMockMemoryProviderFixture` is the test-only provider for generic Memory scenarios. It implements the real generic driver interfaces for context, accepted-operation polling, feedback, events, outbox delivery, health, and UI metadata. Tests register it explicitly; zero-provider tests must not enable a deterministic or fixture-backed provider.

## Component And Browser Suites

Generic Memory UI behavior is covered outside the Memory test project:

- `tests/Components/CanDoItAll.Tests.Components/MemoryProvidersPageTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/MemoryProviderOperationsPageTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/MemoryProviderUiSurfacePageTests.cs`
- `tests/Playwright/CanDoItAll.Tests.Playwright/MemoryProviderManagementPlaywrightTests.cs`

These tests own provider-list rendering, query and operation flows, feedback and event UI, and provider-surface projection. They use generic Memory services and must not import `CanDoItAll.Modules.CognitiveMemory`.

## Retained Legacy Native Suite

Native-engine tests remain in legacy test projects until the native service extraction is complete. They are retained compatibility regressions, not generic Memory startup requirements.

| Legacy area | Retained examples | Eventual owner |
| --- | --- | --- |
| Native engine unit tests | `tests/Unit/CanDoItAll.Tests.Unit/*CognitiveMemory*.cs` | Native service test suite |
| Native module registration | `tests/Unit/CanDoItAll.Tests.Unit/CognitiveMemoryModuleRegistrationTests.cs` | Native service registration tests |
| Native persistence model | `tests/Integration/CanDoItAll.Tests.Integration/*CognitiveMemory*.cs` | Legacy Cognitive Memory entities exercised through the shared `AppDbContext` |
| Native review UI | `tests/Components/CanDoItAll.Tests.Components/CognitiveMemoryPageTests.cs`, `tests/Playwright/CanDoItAll.Tests.Playwright/CognitiveMemoryReviewUiPlaywrightTests.cs` | Native provider UI-surface tests |
| Native fakes | `tests/Support/CanDoItAll.Tests.Support/CognitiveMemory/CognitiveMemoryFakes.cs` | Native service fakes |

`CognitiveMemoryModuleRegistrationTests.cs` does not prove base-host startup coupling. `HostCompositionDependencyRemovalTests` guards the current boundary: base composition must not register the legacy module, Qdrant, or an implicit provider.

## Validation

Run the stable repository gate from the repository root:

```powershell
dotnet restore .\CanDoItAll.slnx
dotnet build .\CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet test .\CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1
```

Use the Playwright and legacy Cognitive Memory filters only when changing their retained compatibility surfaces. See [Testing](../../testing.md) for category-specific commands and prerequisites.
