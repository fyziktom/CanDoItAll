# Plugins lightweight proof

The tests reference the sandbox and its actual renderer/presentation closure.
They need no database. They exercise raw input for all nine schema field kinds,
all six sections, persisted identities, pending reconciliation, explicit unknown
recovery, stale read fencing, grant/lifecycle admission, OAuth effects, upload
lifetime and dependency/public-type guards.

```powershell
dotnet test tests/Components/CanDoItAll.Plugins.UI.Tests/CanDoItAll.Plugins.UI.Tests.csproj --configuration PluginsUiProof --list-tests --filter FullyQualifiedName~CanDoItAll.Tests.Components.Plugins /m:1
dotnet test tests/Components/CanDoItAll.Plugins.UI.Tests/CanDoItAll.Plugins.UI.Tests.csproj --configuration PluginsUiProof --no-build --no-restore --filter FullyQualifiedName~CanDoItAll.Tests.Components.Plugins /m:1
```

Derive the expected count from current source before discovery. Real owner, API,
production page and browser proof is recorded in the
[boundary record](../../../docs/architecture/plugins-ui-boundary.md).
