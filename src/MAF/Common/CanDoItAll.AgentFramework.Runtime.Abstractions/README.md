# CanDoItAll.AgentFramework.Runtime.Abstractions

## Purpose

Provider-neutral execution, continuation, diagnostics, administration, response, and runtime-state contracts for AgentFramework hosts.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Runtime.Abstractions/CanDoItAll.AgentFramework.Runtime.Abstractions.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.AgentFramework.Runtime.Abstractions.csproj](CanDoItAll.AgentFramework.Runtime.Abstractions.csproj).

## Architecture Notes

This project owns runtime ports and transport-neutral contracts. Provider adapters, persistence, host composition, and UI orchestration remain in their owning projects.

## Related Docs

- Repository overview: `README.md` at the repo root
- AgentFramework overview: `src/MAF/README.md`
