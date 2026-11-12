# Collaboration session tests

Focused xUnit tests of the production module's per-page workspace session. A scripted owner
port isolates UI lifetimes from persistence; PostgreSQL proof stays in the existing
Collaboration integration topic. These tests need no database or complete Web host.

```powershell
dotnet test tests/Unit/CanDoItAll.Collaboration.Tests/CanDoItAll.Collaboration.Tests.csproj --configuration Release --list-tests --filter "FullyQualifiedName~CanDoItAll.Tests.Unit.Collaboration." /m:1
dotnet test tests/Unit/CanDoItAll.Collaboration.Tests/CanDoItAll.Collaboration.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Unit.Collaboration." /m:1
```

Compare source and discovered case counts before execution. See [Testing](../../../docs/testing.md).
