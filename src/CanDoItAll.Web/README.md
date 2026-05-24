# CanDoItAll.Web

## Purpose

Blazor Web App host that composes the runtime, maps development endpoints, loads module assemblies, and serves the local-first UI.

## Project Type

- SDK: `Microsoft.NET.Sdk.Web`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Web/CanDoItAll.Web.csproj
```

## References

Project references:

- `../CanDoItAll.Components/CanDoItAll.Components.csproj`
- `../CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj`
- `../CanDoItAll.Composition/CanDoItAll.Composition.csproj`
- `../CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`
- `../CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj`
- `../CanDoItAll.Modules.Activity/CanDoItAll.Modules.Activity.csproj`
- `../CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj`
- `../CanDoItAll.Modules.Automation/CanDoItAll.Modules.Automation.csproj`
- `../CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj`
- `../CanDoItAll.Modules.Factory/CanDoItAll.Modules.Factory.csproj`
- `../CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`
- `../CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `../CanDoItAll.Modules.Prompts/CanDoItAll.Modules.Prompts.csproj`
- `../CanDoItAll.Modules.Resources/CanDoItAll.Modules.Resources.csproj`
- `../CanDoItAll.Modules.Security/CanDoItAll.Modules.Security.csproj`
- `../CanDoItAll.Modules.TestLab/CanDoItAll.Modules.TestLab.csproj`
- `../CanDoItAll.Modules.Validation/CanDoItAll.Modules.Validation.csproj`
- `../CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj`
- `../CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj`
- `../CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.EntityFrameworkCore.Design (10.0.4)`
- `Microsoft.EntityFrameworkCore.Relational (10.0.4)`

## Architecture Notes

The web host should orchestrate startup, endpoint mapping, and Blazor rendering. Keep non-trivial product behavior in modules or application services.

Development and Visual Studio `http`/`https` launch profiles are PostgreSQL-first. They target `127.0.0.1:5432/candoitall_development` with `candoitall/candoitall` credentials and keep development workspace/control-plane files under `%LOCALAPPDATA%\CanDoItAll`. Use `tools/dev/Ensure-DevelopmentPostgres.ps1` to prepare native PostgreSQL, or `docker compose up -d postgres qdrant` for the repo-managed containers.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
- Development runtime: `docs/development-runtime.md`
