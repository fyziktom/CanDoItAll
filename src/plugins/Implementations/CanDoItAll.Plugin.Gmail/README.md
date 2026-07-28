# CanDoItAll.Plugin.Gmail

## Purpose

Bundled Gmail plugin for workflow executors that download messages by label and mark messages processed through Gmail API calls and OAuth-backed plugin connections.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/plugins/Implementations/CanDoItAll.Plugin.Gmail/CanDoItAll.Plugin.Gmail.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Plugin.Gmail.csproj](CanDoItAll.Plugin.Gmail.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Gmail executors must stay behind plugin grants and OAuth connection checks. Do not call Gmail APIs from process or workflow modules directly; use plugin workflow executors so availability, simulation descriptors, settings schemas, and audit events remain consistent.

The client secret is resolved through the plugin OAuth descriptor environment variable path. Do not hard-code OAuth secrets in docs, settings, or tests.

## Related Docs

- Plugin module: `src/Modules/CanDoItAll.Modules.Plugins/README.md`
- Shared email plugin contracts: `src/plugins/Implementations/CanDoItAll.Plugin.Email/README.md`
