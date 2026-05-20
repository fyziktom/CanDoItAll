# CanDoItAll.Plugin.Office365

## Purpose

Bundled Office 365 plugin for workflow executors that download mail by category and mark messages processed through Microsoft Graph and OAuth-backed plugin connections.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/plugins/CanDoItAll.Plugin.Office365/CanDoItAll.Plugin.Office365.csproj
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

Office 365 executors must stay behind plugin installation, capability grants, OAuth grants, and connection status checks. Do not bypass `Office365GraphClient` from product modules.

Use the shared email payload contracts for workflow outputs so downstream workflow nodes do not depend on Microsoft Graph response shapes.

## Related Docs

- Plugin module: `src/CanDoItAll.Modules.Plugins/README.md`
- Shared email plugin contracts: `src/plugins/CanDoItAll.Plugin.Email/README.md`
