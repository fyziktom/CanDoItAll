# CanDoItAll.Tests.Playwright

## Purpose

Test project for the corresponding CanDoItAll runtime, module, component, MCP, or integration behavior.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj
```

## References

Project references:

- `../CanDoItAll.Tests.Support/CanDoItAll.Tests.Support.csproj`
- `../../src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `../../src/CanDoItAll.Components.WebGlSandbox/CanDoItAll.Components.WebGlSandbox.csproj`
- `../../tools/CanDoItAll.Manager/CanDoItAll.Manager.csproj`

Framework references:

- None

Direct package references:

- `coverlet.collector (6.0.4)`
- `Microsoft.Playwright (1.55.0)`
- `Microsoft.NET.Test.Sdk (17.14.1)`
- `xunit (2.9.3)`
- `xunit.runner.visualstudio (3.1.4)`

## Architecture Notes

Keep tests focused on observable behavior and use shared fixtures from CanDoItAll.Tests.Support where cross-project setup is needed.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
