# CanDoItAll.ScenarioSeeder

## Purpose

Tool for seeding representative CanDoItAll scenarios into local development databases.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build tools/Seeding/CanDoItAll.ScenarioSeeder/CanDoItAll.ScenarioSeeder.csproj
```

## References

Project references:

- `../../src/App/CanDoItAll.Composition/CanDoItAll.Composition.csproj`
- `../../src/Foundation/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../../src/Foundation/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj`
- `../../src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj`
- `../../src/Modules/CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj`
- `../../src/Modules/CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj`
- `../../src/Modules/CanDoItAll.Modules.Factory/CanDoItAll.Modules.Factory.csproj`
- `../../src/Modules/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `../../src/Modules/CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`
- `../../src/Modules/CanDoItAll.Modules.Prompts/CanDoItAll.Modules.Prompts.csproj`
- `../../src/Modules/CanDoItAll.Modules.Resources/CanDoItAll.Modules.Resources.csproj`
- `../../src/Modules/CanDoItAll.Modules.Security/CanDoItAll.Modules.Security.csproj`
- `../../src/Modules/CanDoItAll.Modules.TestLab/CanDoItAll.Modules.TestLab.csproj`
- `../../src/Modules/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj`
- `../../src/Modules/CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj`
- `../../src/Foundation/CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`
- `../../src/App/CanDoItAll.Web/CanDoItAll.Web.csproj`

Framework references:

- `Microsoft.AspNetCore.App`

Direct package references:

- None

## Architecture Notes

This is a local development or operations tool. Keep it explicit about ports, file paths, side effects, and runtime assumptions.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
