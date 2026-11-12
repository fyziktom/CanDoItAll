# TestLab session tests

Controlled owner-port tests cover route echoes, A/B/A reads and project changes,
reference lookups, retired render intents, single write admission, deep submissions,
row identity, field reconciliation, postcommit recovery, unknown outcomes and disposal.
These exercise the production module session with deterministic completion gates.

```powershell
dotnet test tests/Unit/CanDoItAll.TestLab.Tests/CanDoItAll.TestLab.Tests.csproj --configuration Release --filter FullyQualifiedName~TestLabSessionTests
```

The Unit and Stable solutions include this project. Real PostgreSQL owner proof remains
in the owning Integration and Components projects; this port fixture does not replace it.
