# CanDoItAll.AgentFramework.Llm.ProviderRuntime

## Purpose

Provider-backed runtime adapter for the lightweight LLM invocation contracts.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/CanDoItAll.AgentFramework.Llm.ProviderRuntime.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.AgentFramework.Llm.ProviderRuntime.csproj](CanDoItAll.AgentFramework.Llm.ProviderRuntime.csproj).

## Architecture Notes

This project adapts provider-neutral LLM requests to the configured provider runtime. Callers consume the abstractions project and do not depend on this adapter directly outside composition.

## Related Docs

- Repository overview: `README.md` at the repo root
- AgentFramework overview: `src/MAF/README.md`
