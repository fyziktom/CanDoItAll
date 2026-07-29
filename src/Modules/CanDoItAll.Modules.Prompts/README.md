# CanDoItAll.Modules.Prompts

## Purpose

Product module for prompt library management and prompt assets used by users and agents.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Prompts/CanDoItAll.Modules.Prompts.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.Prompts.csproj](CanDoItAll.Modules.Prompts.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. UI and transport adapters should call into these services instead of duplicating module logic.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
