# CanDoItAll.Tests.Components

## Purpose

Test project for the corresponding CanDoItAll runtime, module, component, MCP, or integration behavior.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj
```

## References

Project references:

- `../CanDoItAll.Tests.Support/CanDoItAll.Tests.Support.csproj`
- `../../src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `../../src/CanDoItAll.Components/CanDoItAll.Components.csproj`
- `../../src/CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../../src/CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj`
- `../../src/CanDoItAll.Components.Sandbox/CanDoItAll.Components.Sandbox.csproj`
- `../../src/CanDoItAll.Components.WebGlLib/CanDoItAll.Components.WebGlLib.csproj`
- `../../src/CanDoItAll.Components.WebGlSandbox/CanDoItAll.Components.WebGlSandbox.csproj`
- `../../src/CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj`
- `../../src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `../../src/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj`
- `../../src/CanDoItAll.Modules.Factory/CanDoItAll.Modules.Factory.csproj`
- `../../src/CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`
- `../../tools/CanDoItAll.Manager/CanDoItAll.Manager.csproj`

Framework references:

- None

Direct package references:

- `bunit.web (1.40.0)`
- `coverlet.collector (6.0.4)`
- `Microsoft.Data.Sqlite (10.0.0)`
- `Microsoft.NET.Test.Sdk (17.14.1)`
- `xunit (2.9.3)`
- `xunit.runner.visualstudio (3.1.4)`

## Architecture Notes

Keep tests focused on observable behavior and use shared fixtures from CanDoItAll.Tests.Support where cross-project setup is needed.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
