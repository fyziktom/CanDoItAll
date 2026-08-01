# CanDoItAll.Plugin.Office365

## Purpose

Bundled Office 365 plugin for workflow executors that download mail by category and mark messages processed through Microsoft Graph and OAuth-backed plugin connections.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/plugins/Implementations/CanDoItAll.Plugin.Office365/CanDoItAll.Plugin.Office365.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Plugin.Office365.csproj](CanDoItAll.Plugin.Office365.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Office 365 executors must stay behind plugin installation, capability grants, OAuth grants, and connection status checks. Do not bypass `Office365GraphClient` from product modules.

Use the shared email payload contracts for workflow outputs so downstream workflow nodes do not depend on Microsoft Graph response shapes.

## Related Docs

- Plugin module: `src/Modules/CanDoItAll.Modules.Plugins/README.md`
- Shared email plugin contracts: `src/plugins/Implementations/CanDoItAll.Plugin.Email/README.md`
