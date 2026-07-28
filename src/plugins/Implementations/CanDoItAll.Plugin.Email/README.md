# CanDoItAll.Plugin.Email

## Purpose

Shared email plugin support contracts for normalized email message batches and payload resolution used by concrete mail plugins.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/plugins/Implementations/CanDoItAll.Plugin.Email/CanDoItAll.Plugin.Email.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Plugin.Email.csproj](CanDoItAll.Plugin.Email.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Keep this project provider-neutral. Gmail and Office 365 plugins should project provider-specific message data into these shared email payload contracts before workflow nodes consume it.

## Related Docs

- Gmail plugin: `src/plugins/Implementations/CanDoItAll.Plugin.Gmail/README.md`
- Office 365 plugin: `src/plugins/Implementations/CanDoItAll.Plugin.Office365/README.md`
