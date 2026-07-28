# Generic Memory Validation

Run commands from the repository root. Restore and build the solution first by following the [repository test gate](../../testing.md).

## Focused Tests

Generic runtime and composition:

```powershell
dotnet test .\tests\Memory\CanDoItAll.Memory.Tests\CanDoItAll.Memory.Tests.csproj --configuration Release --no-build
```

MAF integration:

```powershell
dotnet test .\tests\MAF\CanDoItAll.AgentFramework.Memory.Tests\CanDoItAll.AgentFramework.Memory.Tests.csproj --configuration Release --no-build
```

Agent runtime compatibility:

```powershell
dotnet test .\tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --no-build --filter "FullyQualifiedName~MemoryAgentRuntimeToolProviderTests|FullyQualifiedName~MemoryWorkflowExecutorTests|FullyQualifiedName~MemoryAgentContextContributorTests|FullyQualifiedName~MemoryMafIntegrationCheckpointTests"
```

Component UI:

```powershell
dotnet test .\tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --configuration Release --no-build --filter "FullyQualifiedName~MemoryProvider|FullyQualifiedName~MemoryUiRefactoringCheckpoint"
```

Database switching:

```powershell
dotnet test .\tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-build --filter "FullyQualifiedName~DatabaseSwitchIntegrationTests"
```

Run the Playwright project when a change affects `/memory`, provider-profile rendering, provider surfaces, or browser-visible error states:

```powershell
dotnet test .\tests\Playwright\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --configuration Release --no-build --filter "FullyQualifiedName~MemoryProviderManagementPlaywrightTests"
```

## Required Assertions

A memory change is not ready unless tests show that:

- the base host starts without native Cognitive Memory, Qdrant, SemanticCompletion, or any enabled provider;
- disabled or missing providers return typed diagnostics without fallback;
- profile selection, agent binding order, and optional/required failure policy are deterministic;
- provider data crosses Source Gateway and driver contracts rather than module persistence boundaries;
- operation ownership prevents cross-agent, cross-session, workflow, or process status disclosure;
- worker leases prevent concurrent ownership when background workers are enabled;
- the main host has no `/api/cognitive-memory` route family or native implementation reference;
- the experimental `/api/memory-providers` surface routes only through provider-neutral application boundaries.

The separately owned native service has its own build, test, deployment, and provider validation. Its native implementation tests are not part of the base-host release gate.
