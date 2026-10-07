# TestLab contracts

Pure TestLab editor and summary models, the existing numeric status enum, and the minimal
child identity contract used by owner synchronization. Existing namespaces, defaults and
JSON shape are preserved. `ProjectLifetimeId` remains excluded from summary JSON.

The only project dependency is Projects.Contracts, which owns the immutable
profile/project/lifetime admission. No EF entities, context, owner services, registration
or Blazor types belong here.

```powershell
dotnet build src/Modules/CanDoItAll.Modules.TestLab.Contracts/CanDoItAll.Modules.TestLab.Contracts.csproj --configuration Release /m:1
```

See the [boundary record](../../../docs/architecture/testlab-ui-boundary.md).
