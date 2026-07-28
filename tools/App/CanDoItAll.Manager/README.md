# CanDoItAll.Manager

## Purpose

Local development manager that supervises `dotnet watch`, runtime readiness probes,
Tailwind rebuilds, capsule indexing, workspace-process cleanup, and tuning endpoints.

## Project Type

- SDK: `Microsoft.NET.Sdk.Web`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build tools/App/CanDoItAll.Manager/CanDoItAll.Manager.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Manager.csproj](CanDoItAll.Manager.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This is a local development or operations tool. Keep it explicit about ports, file paths, side effects, and runtime assumptions.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
