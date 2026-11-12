# Plugins contracts

Backend-free catalog, settings, connection, grant, log, OAuth and package API models.
Their `CanDoItAll.Modules.Plugins` namespaces, JSON shapes/defaults and enum values
are preserved. The implementation assembly forwards the moved types. Existing XML
documentation moves with the models for the OpenAPI source generator.

Plugin SDK identifiers, descriptors and loader identity stay in their original
assemblies. The runtime registrar, package path options, token envelope and stores
remain in the implementation module. Small typed progress receipts describe known
package and OAuth commit stages without exposing tokens or authorization URLs.

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Plugins.Contracts/CanDoItAll.Modules.Plugins.Contracts.csproj --configuration Release /m:1
```

See the [boundary record](../../../docs/architecture/plugins-ui-boundary.md).
