# CanDoItAll.Tests.Unit

## Purpose

Test project for the corresponding CanDoItAll runtime, module, component, MCP, or integration behavior.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj
```

## References

Project references:

- `../CanDoItAll.Tests.Support/CanDoItAll.Tests.Support.csproj`
- `../../src/CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../../src/CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`
- `../../src/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../../src/CanDoItAll.Modules.Security/CanDoItAll.Modules.Security.csproj`
- `../../src/CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`
- `../../src/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj`
- `../../src/CanDoItAll.Modules.Prompts/CanDoItAll.Modules.Prompts.csproj`
- `../../src/CanDoItAll.Modules.Factory/CanDoItAll.Modules.Factory.csproj`
- `../../src/CanDoItAll.Mcp.SshOps/CanDoItAll.Mcp.SshOps.csproj`
- `../../tools/CanDoItAll.Manager/CanDoItAll.Manager.csproj`

Framework references:

- None

Direct package references:

- `coverlet.collector (6.0.4)`
- `Microsoft.NET.Test.Sdk (17.14.1)`
- `xunit (2.9.3)`
- `xunit.runner.visualstudio (3.1.4)`
- `CanDoItAll.Components.WebGlLib (0.1.0)`

## Architecture Notes

Keep tests focused on observable behavior and use shared fixtures from CanDoItAll.Tests.Support where cross-project setup is needed.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
