# Memory Test Suite Rebalance

The memory provider extraction work splits test ownership between the generic memory runtime and the retained legacy native Cognitive Memory module. Generic provider tests must not enforce native module startup, Qdrant, or SemanticCompletion dependencies.

## Generic Memory Suite

The generic provider suite lives in `tests/Memory/CanDoItAll.Memory.Tests`.

| Area | Primary tests |
| --- | --- |
| Provider protocol and registry | `MemoryProtocolContractsTests.cs`, `MemoryProviderRegistryTests.cs`, `MemoryTestSuiteRebalanceCheckpointTests.cs` |
| Runtime and ledgers | `MemoryRuntimeCheckpointTests.cs`, `MemoryRuntimePersistenceTests.cs`, `MemoryOperationHandlerTests.cs`, `MemoryAsyncWorkerTests.cs` |
| Source Gateway | `MemorySourceGatewayTests.cs`, `MemorySourceGatewayHardeningCheckpointTests.cs`, `ManualMemorySourceIngestionTests.cs` |
| MAF integration | `MemoryAgentContextContributorTests.cs`, `MemoryOperationHandlerTests.cs` |
| Host composition guards | `HostCompositionDependencyRemovalTests.cs` |
| Native remote driver contract | `NativeRemoteMemoryProviderDriverTests.cs` |

`GenericMockMemoryProviderFixture` is the test-only mock provider package for generic memory tests. It implements the real generic driver interfaces for immediate context, accepted async operation polling, feedback delivery, event polling, outbox delivery, health, and UI surface metadata. Tests must register it explicitly per scenario; zero-provider tests must not enable deterministic or fixture-backed providers.

## Component And Browser Suite

Generic memory UI behavior is covered outside the memory test project:

- `tests/Components/CanDoItAll.Tests.Components/MemoryProvidersPageTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/MemoryProviderOperationsPageTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/MemoryProviderUiSurfacePageTests.cs`
- `tests/Playwright/CanDoItAll.Tests.Playwright/MemoryProviderManagementPlaywrightTests.cs`

These tests own provider list rendering, query and operations flows, feedback/event UI, and provider surface projection. They must use generic memory services and must not import `CanDoItAll.Modules.CognitiveMemory`.

## Retained Legacy Native Suite

Native engine tests remain in the legacy test projects until the native service extraction is completed. They are retained regressions, not generic memory startup requirements.

| Legacy area | Retained examples | Owner after extraction |
| --- | --- | --- |
| Native engine unit tests | `tests/Unit/CanDoItAll.Tests.Unit/*CognitiveMemory*.cs` | Native service test suite |
| Native module registration | `tests/Unit/CanDoItAll.Tests.Unit/CognitiveMemoryModuleRegistrationTests.cs` | Native service registration tests |
| Native persistence model | `tests/Integration/CanDoItAll.Tests.Integration/*CognitiveMemory*.cs` | Native `CognitiveMemoryDbContext` tests |
| Native review UI | `tests/Components/CanDoItAll.Tests.Components/CognitiveMemoryPageTests.cs`, `tests/Playwright/CanDoItAll.Tests.Playwright/CognitiveMemoryReviewUiPlaywrightTests.cs` | Native provider UI surface tests |
| Native fakes | `tests/Support/CanDoItAll.Tests.Support/CognitiveMemory/CognitiveMemoryFakes.cs` | Native service fakes |

`CognitiveMemoryModuleRegistrationTests.cs` does not prove base-host startup coupling. Base-host decoupling is guarded by `HostCompositionDependencyRemovalTests`, which asserts `CanDoItAll.Composition` no longer registers the native module, Qdrant, or implicit providers for zero-provider startup.

## Validation Commands

Run these from the repository root after changing generic memory provider behavior:

```powershell
dotnet test tests\Memory\CanDoItAll.Memory.Tests\CanDoItAll.Memory.Tests.csproj --logger "console;verbosity=minimal"
dotnet test tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~MemoryProvider" --logger "console;verbosity=minimal"
dotnet build CanDoItAll.slnx --no-restore -m:1 --verbosity:minimal
```

Run legacy native filters only when changing retained native Cognitive Memory code:

```powershell
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1
```
