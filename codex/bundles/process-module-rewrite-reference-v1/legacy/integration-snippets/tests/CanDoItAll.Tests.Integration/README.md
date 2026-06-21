# CanDoItAll.Tests.Integration

## Purpose

Test project for the corresponding CanDoItAll runtime, module, component, MCP, or integration behavior.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj
```

## References

Project references:

- `../CanDoItAll.Tests.Support/CanDoItAll.Tests.Support.csproj`
- `../../src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `../../src/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../../src/CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`
- `../../src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `../../src/CanDoItAll.Modules.Resources/CanDoItAll.Modules.Resources.csproj`
- `../../src/CanDoItAll.Modules.Prompts/CanDoItAll.Modules.Prompts.csproj`
- `../../src/CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj`
- `../../tools/CanDoItAll.Manager/CanDoItAll.Manager.csproj`
- `../../src/CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj`

Framework references:

- None

Direct package references:

- `coverlet.collector (6.0.4)`
- `Microsoft.NET.Test.Sdk (17.14.1)`
- `xunit (2.9.3)`
- `xunit.runner.visualstudio (3.1.4)`

## Architecture Notes

Keep tests focused on observable behavior and use shared fixtures from CanDoItAll.Tests.Support where cross-project setup is needed.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
