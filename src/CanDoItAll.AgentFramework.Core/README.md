# CanDoItAll.AgentFramework.Core

## Purpose

Provider-neutral AgentFramework application services, execution contracts, workspace orchestration, and runtime abstractions.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj
```

## References

Project references:

- `../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`

Framework references:

- None

Direct package references:

- `OpenTelemetry.Api (1.15.3)`

## Architecture Notes

Keep AgentFramework model contracts, persistence, provider-neutral orchestration, and provider/runtime adapters separated. MAF-specific workflow adapters and checkpoint helpers belong in `CanDoItAll.AgentFramework.Maf`. Process automation should consume this layer through the AgentFramework module bridge instead of reaching into provider-specific code directly.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
