# CanDoItAll.AgentFramework.Llm.Abstractions

## Purpose

Bounded provider-neutral conversation, attachment, invocation, and response contracts for lightweight LLM calls.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions/CanDoItAll.AgentFramework.Llm.Abstractions.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.AgentFramework.Llm.Abstractions.csproj](CanDoItAll.AgentFramework.Llm.Abstractions.csproj).

## Architecture Notes

This project defines LLM-facing ports and immutable contracts. Provider SDKs and provider selection do not belong here.

## Related Docs

- Repository overview: `README.md` at the repo root
- AgentFramework overview: `src/MAF/README.md`
