# Subbundle 03: Focused Regression Validation

## Goal

Prove current behavior still works after the package update.

## Required focused tests

```powershell
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~MafAgentRuntime|FullyQualifiedName~ProviderDispatchLaneGate|FullyQualifiedName~ProviderRuntimeLifecycle|FullyQualifiedName~Finalizer|FullyQualifiedName~ToolProviderComposition|FullyQualifiedName~Workflow"
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~AgentFramework|FullyQualifiedName~Process|FullyQualifiedName~ProjectStructureAgent"
```

If the exact tests do not exist, discover nearest tests with:

```powershell
rg "MafAgentRuntime|ProviderDispatchLaneGate|ProviderRuntimeLifecycle|Finalizer|ToolProviderComposition|Workflow|AgentFramework|Process|ProjectStructureAgent" tests -g "*.cs"
```

Then run the closest available tests and document replacements.

## Optional broader tests

```powershell
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release
dotnet test tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --configuration Release
```

## Exit criteria

- Focused tests pass or failures are clearly unrelated pre-existing failures.
- Replacement tests are documented.
- No governance behavior weakened to pass tests.
