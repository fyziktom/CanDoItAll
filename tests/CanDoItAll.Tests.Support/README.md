# CanDoItAll.Tests.Support

## Purpose

Shared test support library for integration hosts, database setup, fixtures, and common assertions.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build tests/CanDoItAll.Tests.Support/CanDoItAll.Tests.Support.csproj
```

## References

Project references:

- `../../src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj`
- `../../src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj`
- `../../src/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../../src/CanDoItAll.Modules.Activity/CanDoItAll.Modules.Activity.csproj`
- `../../src/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj`
- `../../src/CanDoItAll.Modules.Automation/CanDoItAll.Modules.Automation.csproj`
- `../../src/CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj`
- `../../src/CanDoItAll.Modules.Factory/CanDoItAll.Modules.Factory.csproj`
- `../../src/CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`
- `../../src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `../../src/CanDoItAll.Modules.Prompts/CanDoItAll.Modules.Prompts.csproj`
- `../../src/CanDoItAll.Modules.Resources/CanDoItAll.Modules.Resources.csproj`
- `../../src/CanDoItAll.Modules.Security/CanDoItAll.Modules.Security.csproj`
- `../../src/CanDoItAll.Modules.TestLab/CanDoItAll.Modules.TestLab.csproj`
- `../../src/CanDoItAll.Modules.Validation/CanDoItAll.Modules.Validation.csproj`
- `../../src/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj`
- `../../src/CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj`
- `../../src/CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`
- `../../src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `../../src/CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj`

Framework references:

- `Microsoft.AspNetCore.App`

Direct package references:

- `Microsoft.Data.Sqlite (10.0.0)`
- `Npgsql (10.0.0)`

## Architecture Notes

Keep tests focused on observable behavior and use shared fixtures from CanDoItAll.Tests.Support where cross-project setup is needed.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
