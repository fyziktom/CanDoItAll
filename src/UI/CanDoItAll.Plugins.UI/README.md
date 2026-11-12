# Plugins rendering library

`PluginsWorkspaceSurface` renders the catalog tree, lifecycle header, Main info,
Executors, Settings, Connections, Logs, Grants and the package dialog. All children
use the shipped BaseLib controls. The renderer receives `IPluginsWorkspace`; it
injects no catalog, database, vault, installer, runtime or navigation service.

`PluginConnectionEditorState` retains raw input, schema validation, the immutable
submission and accepted identity across unmounts. It performs no I/O. Host-owned
presentation coordinates the reads, admission and effects. Section indices are
mapped explicitly to `PluginSection`; `/plugins` has no new query protocol.

The dependency boundary consists of Plugins.Contracts, descriptive Plugins and
framework abstractions, SharedKernel, BaseLib/Common and Blazor. There is no feature
CSS or JavaScript. The normal Tailwind source scan includes this RCL; the sandbox
links the authoritative Web stylesheet without a Web project reference.

```powershell
dotnet build src/UI/CanDoItAll.Plugins.UI/CanDoItAll.Plugins.UI.csproj --configuration Release /m:1
```

See the [sandbox](../../Sandboxes/CanDoItAll.Plugins.UiSandbox/README.md) and
[boundary and proof record](../../../docs/architecture/plugins-ui-boundary.md).
