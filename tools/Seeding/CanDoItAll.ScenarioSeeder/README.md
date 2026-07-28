# CanDoItAll.ScenarioSeeder

## Purpose

Tool for seeding representative CanDoItAll scenarios into local development databases.

The interactive Gantt sample is development-only and idempotent. It persists canonical task schedules,
finish-to-start dependencies, and person/agent assignments through the normal project services:

```powershell
dotnet run --project tools/Seeding/CanDoItAll.ScenarioSeeder/CanDoItAll.ScenarioSeeder.csproj -- `
  --scenario gantt-sample-project `
  --profile-root <database-profile-root>
```

The command returns the seeded project's `/projects/{id}/structure` route.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build tools/Seeding/CanDoItAll.ScenarioSeeder/CanDoItAll.ScenarioSeeder.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.ScenarioSeeder.csproj](CanDoItAll.ScenarioSeeder.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This is a local development or operations tool. Keep it explicit about ports, file paths, side effects, and runtime assumptions.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
