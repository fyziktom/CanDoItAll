# CanDoItAll.AgentFramework.Persistence

## Purpose

Persistence contracts and file-backed storage support for AgentFramework catalogs, workspaces, executions, receipts, and artifacts.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Persistence/CanDoItAll.AgentFramework.Persistence.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.AgentFramework.Persistence.csproj](CanDoItAll.AgentFramework.Persistence.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Keep AgentFramework model contracts, persistence, provider-neutral orchestration, and provider/runtime adapters separated. Process automation should consume this layer through the AgentFramework module bridge instead of reaching into provider-specific code directly.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
