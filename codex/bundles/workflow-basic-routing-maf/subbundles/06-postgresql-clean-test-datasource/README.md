# PostgreSQL Clean Test Datasource

## Status

- `Completed`

## Objective

- Provide a clean PostgreSQL workflow-routing test database and a Visual Studio launch profile that points the web app at that datasource.
- Make reset behavior explicit and bounded to the named workflow-routing database.

## Covered Inputs

- RQ-021: clean PostgreSQL datasource and Visual Studio profile.

## Prerequisites

- Subbundles 01-05 completed.
- Local PostgreSQL or Docker PostgreSQL must be available for live reset proof; if unavailable, record the blocker and validate scripts/config statically.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Properties\launchSettings.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\appsettings.Development.json`
- `C:\repositories\CanDoItAll\tools\dev`

## Deliverables

- Safe reset/setup script for the workflow-routing PostgreSQL database.
- Visual Studio launch profile or appsettings datasource entries.
- Evidence that the configured datasource can be created or that the local PostgreSQL prerequisite is missing.

## Validation Depth

- Configuration/script proof plus app startup/build proof.

## Dependency Impact

- Downstream subbundles 08 and 09 depend on this profile for persistent workflow example seeding and PostgreSQL verification.
- The reset script is bounded to `candoitall_workflow_routing_dev` and does not affect production or unrelated local databases.

## Implementation Steps

1. Inspect the current persistence configuration and environment-variable contract.
2. Add a bounded reset/setup script for `candoitall_workflow_routing_dev`.
3. Add Visual Studio launch profile environment variables for PostgreSQL.
4. Run the reset/setup script when PostgreSQL is available.
5. Build or start the app with the new profile configuration.

## Do Not Do

- Do not drop or mutate any database except the explicitly named workflow-routing test database.
- Do not embed secrets beyond local-development defaults.

## Acceptance Checklist

- The datasource is discoverable from Visual Studio launch profiles.
- Reset script refuses unsafe or empty database names.
- Proof records whether live PostgreSQL reset succeeded.

## Proof Required

- Script/config inspection.
- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -m:1` or targeted web build.

## Closure Proof

- Added `C:\repositories\CanDoItAll\tools\dev\Reset-WorkflowRoutingPostgres.ps1`.
- Added Visual Studio launch profile `PostgreSQL workflow routing` in `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Properties\launchSettings.json`.
- Live PostgreSQL app startup against `candoitall_workflow_routing_dev` succeeded; evidence: `reviews/evidence/subbundle-06/postgres-app.log`.
- Seed-count query returned 15 definitions, 15 components, and 1 settings row; evidence: `reviews/evidence/subbundle-06/postgres-seed-counts.txt`.

## Browser Validation Logging

- N/A unless app startup/browser smoke uses this datasource.

## Progression Gate

- Subbundle 08 may rely on persistent workflow examples only after datasource config is present or a documented local PostgreSQL blocker is recorded.

## Suggested Agent Prompt

```text
Implement this subbundle only: add a safe PostgreSQL workflow-routing reset/setup script and Visual Studio launch profile/configuration. Do not mutate unrelated databases.
```
