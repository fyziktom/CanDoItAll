# CanDoItAll.Plugin.Gmail

## Purpose

Bundled Gmail plugin for workflow executors that download messages by label and mark messages processed through Gmail API calls and OAuth-backed plugin connections.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/plugins/CanDoItAll.Plugin.Gmail/CanDoItAll.Plugin.Gmail.csproj
```

## References

Project references:

- `../../CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `../../CanDoItAll.Modules.Plugins/CanDoItAll.Modules.Plugins.csproj`
- `../../CanDoItAll.Plugins.Abstractions/CanDoItAll.Plugins.Abstractions.csproj`
- `../../CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`
- `../CanDoItAll.Plugin.Email/CanDoItAll.Plugin.Email.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.Extensions.DependencyInjection.Abstractions (10.0.7)`
- `Microsoft.Extensions.Http (10.0.7)`

## Architecture Notes

Gmail executors must stay behind plugin grants and OAuth connection checks. Do not call Gmail APIs from process or workflow modules directly; use plugin workflow executors so availability, simulation descriptors, settings schemas, and audit events remain consistent.

The client secret is resolved through the plugin OAuth descriptor environment variable path. Do not hard-code OAuth secrets in docs, settings, or tests.

## Related Docs

- Plugin module: `src/CanDoItAll.Modules.Plugins/README.md`
- Shared email plugin contracts: `src/plugins/CanDoItAll.Plugin.Email/README.md`
