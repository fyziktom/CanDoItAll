# CanDoItAll.AgentFramework.Models

## Purpose

Shared AgentFramework model types for catalogs, provider profiles, executions, artifacts, tools, and approvals.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.AgentFramework.Models.csproj](CanDoItAll.AgentFramework.Models.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Keep AgentFramework model contracts, persistence, provider-neutral orchestration, and provider/runtime adapters separated. Process automation should consume this layer through the AgentFramework module bridge instead of reaching into provider-specific code directly.

Project-structure access metadata retains the serialized AllowedProjectIds list and can
carry exact database profile/project/lifetime bindings alongside it. Typed grants and
revocation targets contain data only. Catalog mutation preserves unrelated JSON and
fails explicitly on malformed lifetime metadata; the product owner supplies admission
and reserved-creation policy. The metadata type alone does not validate project liveness.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
