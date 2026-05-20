# CanDoItAll.Plugin.Email

## Purpose

Shared email plugin support contracts for normalized email message batches and payload resolution used by concrete mail plugins.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/plugins/CanDoItAll.Plugin.Email/CanDoItAll.Plugin.Email.csproj
```

## References

Project references:

- `../../CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`

Framework references:

- None

Direct package references:

- None

## Architecture Notes

Keep this project provider-neutral. Gmail and Office 365 plugins should project provider-specific message data into these shared email payload contracts before workflow nodes consume it.

## Related Docs

- Gmail plugin: `src/plugins/CanDoItAll.Plugin.Gmail/README.md`
- Office 365 plugin: `src/plugins/CanDoItAll.Plugin.Office365/README.md`
