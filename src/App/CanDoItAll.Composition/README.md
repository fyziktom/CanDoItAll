# CanDoItAll.Composition

## Purpose

Composition root for runtime modules, shared services, infrastructure, provider setup, and component assembly discovery.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/App/CanDoItAll.Composition/CanDoItAll.Composition.csproj
```

## References

Project references:

- `../CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj`
- `../CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj`
- `../CanDoItAll.Modules.Factory/CanDoItAll.Modules.Factory.csproj`
- `../CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`
- `../CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `../CanDoItAll.Modules.Prompts/CanDoItAll.Modules.Prompts.csproj`
- `../CanDoItAll.Modules.Resources/CanDoItAll.Modules.Resources.csproj`
- `../CanDoItAll.Modules.Security/CanDoItAll.Modules.Security.csproj`
- `../CanDoItAll.Modules.TestLab/CanDoItAll.Modules.TestLab.csproj`
- `../CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj`
- `../CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj`
- `../CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj`

Framework references:

- None

Direct package references:

- None

## Architecture Notes

Composition is the boundary where modules, infrastructure, provider configuration, and shared components are wired together. Keep registrations explicit and avoid moving domain behavior into startup code.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
