# Memory Test Suite Rebalance

The provider extraction separates the experimental generic Memory runtime from the
standalone native Cognitive Memory service. Generic provider and external-driver tests
must not make native-service startup, Qdrant, or Semantic Completion dependencies part
of the base-host contract.

## Generic Memory Suite

The generic provider suite lives in `tests/Memory/CanDoItAll.Memory.Tests`.

| Area | Primary tests |
| --- | --- |
| Provider protocol and registry | `MemoryProtocolContractsTests.cs`, `MemoryProviderRegistryTests.cs`, `MemoryTestSuiteRebalanceCheckpointTests.cs` |
| Runtime and ledgers | `MemoryRuntimeCheckpointTests.cs`, `MemoryRuntimePersistenceTests.cs`, `MemoryOperationHandlerTests.cs`, `MemoryAsyncWorkerTests.cs` |
| Source gateway | `MemorySourceGatewayTests.cs`, `MemorySourceGatewayHardeningCheckpointTests.cs`, `ManualMemorySourceIngestionTests.cs` |
| MAF integration | `MemoryAgentContextContributorTests.cs`, `MemoryOperationHandlerTests.cs` |
| Host composition guards | `HostCompositionDependencyRemovalTests.cs` |
| Cognitive Memory external-driver contract | `NativeRemoteMemoryProviderDriverTests.cs` |

`GenericMockMemoryProviderFixture` is the test-only provider for generic Memory scenarios. It implements the real generic driver interfaces for context, accepted-operation polling, feedback, events, outbox delivery, health, and UI metadata. Tests register it explicitly; zero-provider tests must not enable a deterministic or fixture-backed provider.

## Component And Browser Suites

Generic Memory UI behavior is covered outside the Memory test project:

- `tests/Components/CanDoItAll.Tests.Components/MemoryProvidersPageTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/MemoryProviderOperationsPageTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/MemoryProviderUiSurfacePageTests.cs`
- `tests/Playwright/CanDoItAll.Tests.Playwright/MemoryProviderManagementPlaywrightTests.cs`

These tests own provider-list rendering, query and operation flows, feedback and event UI, and provider-surface projection. They use generic Memory services and must not import native Cognitive Memory implementation namespaces.

## Standalone Native Suite

Native domain, persistence, runtime, UI, and integration tests belong in the
[CanDoItAll.CognitiveMemory repository](https://github.com/fyziktom/CanDoItAll.CognitiveMemory).
This repository keeps only:

- generic provider runtime and UI tests;
- conformance tests for `src/Memory/Drivers/CanDoItAll.Memory.Drivers.CognitiveMemory`;
- negative boundary tests that prevent native implementation coupling;
- migration-only tests for the legacy PostgreSQL export bridge.

`HostCompositionDependencyRemovalTests` guards the base-host boundary: composition must not register native implementation services, Qdrant, or an implicit provider.

## Validation

Run the stable repository gate from the repository root:

```powershell
dotnet restore .\CanDoItAll.slnx
dotnet build .\CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet test .\CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1
```

Run the standalone repository's validation when changing the native service contract. See [Testing](../../testing.md) for category-specific commands and prerequisites.
