# Memory UI component tests

These tests build the standalone sandbox and exercise the same renderer and presentation
controller as `/memory`, without production module services, providers or a database.
They cover raw input, draft/read/effect identity, admission and recovery, registered
component lifetime, and negative runtime/public-contract dependency boundaries.

Discover and run the owning topic from the repository root:

```powershell
dotnet test tests/Components/CanDoItAll.Memory.UI.Tests --configuration Release --list-tests --filter FullyQualifiedName~CanDoItAll.Tests.Components.Memory
dotnet test tests/Components/CanDoItAll.Memory.UI.Tests --configuration Release --no-build --no-restore --filter FullyQualifiedName~CanDoItAll.Tests.Components.Memory
```

Confirm current discovery before execution. The project belongs to Components, Stable and
the component CI shards. Production PostgreSQL and browser proof remain separate in
`MemoryWorkspaceOwnerTests` and `MemoryBrowserTests`; synthetic scenarios do not grant
unsupported provider capabilities. See the [boundary receipt](../../../docs/architecture/memory-ui-boundary.md).
