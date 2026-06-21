# CanDoItAll.Modules.Factory

## Purpose

Product module for factory/work generation concepts that connect templates, projects, and runtime work.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Modules.Factory/CanDoItAll.Modules.Factory.csproj
```

## References

Project references:

- `../CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj`
- `../CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`
- `../CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`
- `../CanDoItAll.Modules.Resources/CanDoItAll.Modules.Resources.csproj`
- `../CanDoItAll.Modules.Prompts/CanDoItAll.Modules.Prompts.csproj`
- `../CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.AspNetCore.Components.Web (10.0.4)`

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. MCP projects should call into these services instead of duplicating module logic.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
