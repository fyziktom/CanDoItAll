# SB03 Test Transcript

## Targeted Unit Tests

Command:

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~AgentContextContributionTests|FullyQualifiedName~CognitiveMemoryOperationalSettingsTests|FullyQualifiedName~CognitiveMemoryOperationalServicesTests"
```

Result:

```text
Passed: 38
Failed: 0
```

## Component Tests

Command:

```powershell
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~CognitiveMemoryPageTests"
```

Result:

```text
Passed: 2
Failed: 0
```

## Build

Command:

```powershell
dotnet build CanDoItAll.slnx --no-restore
```

Result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```
