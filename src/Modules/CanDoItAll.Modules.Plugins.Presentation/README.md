# Plugins presentation

`PluginsWorkspace` owns one page lifetime over `IPluginWorkspaceOwner`. It composes
seven independent request lanes, a bounded draft registry and typed mutation
admission. The production module's `PluginWorkspaceSession` binds real services;
the sandbox binds in-memory scenario storage. No production owner is referenced.

Connection targets include their draft generation; grant targets include the full
scope and recipe. Lifecycle decisions share a plugin target. Saves and OAuth share
an editor target. Accepted identities are reconciled before secondary reads, and
unknown results require explicit review. Refresh performs reads only.

Upload owns the bounded browser stream until disposal. The visible input remains
mounted while reading; a second selection or close is refused. Retiring the page
cancels owned reads and suppresses stale effects without undoing admitted writes.

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Plugins.Presentation/CanDoItAll.Modules.Plugins.Presentation.csproj --configuration Release /m:1
```

See the [boundary record](../../../docs/architecture/plugins-ui-boundary.md).
