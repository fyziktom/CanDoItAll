# CanDoItAll.Modules.AgentFramework

## Purpose

Product module that exposes AgentFramework catalog, provider, execution, and technical-agent bridge capabilities to the app runtime.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj
```

## References

Project references:

- `../CanDoItAll.AgentFramework.Components/CanDoItAll.AgentFramework.Components.csproj`
- `../CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj`
- `../CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `../CanDoItAll.AgentFramework.Persistence/CanDoItAll.AgentFramework.Persistence.csproj`
- `../CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj`
- `../CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj`
- `../CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `../CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`
- `../CanDoItAll.Modules.Security/CanDoItAll.Modules.Security.csproj`
- `../CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj`
- `../CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.AspNetCore.Components.Web (10.0.5)`

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. MCP projects should call into these services instead of duplicating module logic.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
