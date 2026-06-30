# CanDoItAll.Manager

## Purpose

Local development manager that supervises dotnet watch, readiness checks, browser sessions, capsules, and tuning endpoints.

## Project Type

- SDK: `Microsoft.NET.Sdk.Web`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build tools/App/CanDoItAll.Manager/CanDoItAll.Manager.csproj
```

## References

Project references:

- `../../src/Foundation/CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`
- `../../src/Foundation/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.AspNetCore.OpenApi (10.0.4)`
- `System.Management (10.0.0)`

## Architecture Notes

This is a local development or operations tool. Keep it explicit about ports, file paths, side effects, and runtime assumptions.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
