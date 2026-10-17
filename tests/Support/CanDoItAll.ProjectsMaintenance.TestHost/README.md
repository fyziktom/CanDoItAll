# CanDoItAll.ProjectsMaintenance.TestHost

Runs the registered Projects operation-maintenance worker in an independent process
for the opt-in sustained-capacity integration scenarios. The parent supplies the
owned temporary root, exact profile/run identity, finite lifetime and explicit test
database through environment configuration. The shared bounded JSON-lines protocol
reports readiness, cycles and terminal backend identities for cleanup verification.

This executable is test infrastructure only. It is a build-only Integration
dependency, has no test SDK, and is not a production host or separate Stable test
assembly. It never provisions a PostgreSQL server. Use the existing isolated fixture
and the parent-owned scenario/resource contract in the
[maintenance validation plan](../../../codex/bundles/concurrent-mutations/plan/P02b-sustained-maintenance-validation.md).

```powershell
dotnet build ./tests/Support/CanDoItAll.ProjectsMaintenance.TestHost/CanDoItAll.ProjectsMaintenance.TestHost.csproj --configuration Release /m:1
```
