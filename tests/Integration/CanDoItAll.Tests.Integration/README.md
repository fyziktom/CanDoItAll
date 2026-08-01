# CanDoItAll.Tests.Integration

Exercises host composition, APIs, PostgreSQL persistence, providers, plugins, workflows,
processes, AgentFramework execution, and module integration.

```powershell
dotnet test .\tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release
```

Tests that require external services must declare prerequisites and fail or skip
explicitly according to their test contract.
