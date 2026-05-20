# 02-runtime-installation-and-script-docs

## Status

- `Completed`

## Objective

- Make public setup docs accurate for PostgreSQL, Qdrant, web app installation, MCP resetup, and Codex skill installation.

## Success Criteria

- Root README has a clear quick start for Docker Compose and native PostgreSQL.
- Root README documents Qdrant purpose, ports, and default collection.
- Root README documents `tools\Install-CanDoItAllWebApp.ps1`, `tools\Reinstall-CanDoItAllMcps.ps1`, and `codex\scripts\install-candoitall-skills.ps1`.
- `docs\development-runtime.md` remains consistent with config and root README.

## Covered Inputs

- `N002`: Main README must document PostgreSQL and Qdrant setup.
- `N003`: Main README must document installation, MCP, and skill scripts.
- `N005`: Remove old active setup guidance.

## Prerequisites

- `01-doc-inventory-and-target-structure` inventory source references are recorded.

## Exact Source References

- C:\repositories\CanDoItAll\README.md
- C:\repositories\CanDoItAll\docs\README.md
- C:\repositories\CanDoItAll\docs\development-runtime.md
- C:\repositories\CanDoItAll\docker-compose.yml
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\appsettings.json
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\appsettings.Development.json
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Properties\launchSettings.json
- C:\repositories\CanDoItAll\tools\dev\Ensure-DevelopmentPostgres.ps1
- C:\repositories\CanDoItAll\tools\Install-CanDoItAllWebApp.ps1
- C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1
- C:\repositories\CanDoItAll\codex\scripts\install-candoitall-skills.ps1

## Deliverables

- Updated root README runtime/setup/script sections.
- Updated docs index and development runtime details where needed.

## Dependency Impact

- Final closure depends on these docs being exact; public setup is the highest-risk documentation surface.

## Validation Depth

- Critical public setup documentation foundation.

## Implementation Steps

1. Update the root README quick start and runtime setup sections.
2. Add script purpose and command examples for web app install, MCP resetup, and skill installation.
3. Keep retired Processes/ProjectStructure MCP guidance in transition notes only.
4. Update `docs\development-runtime.md` and `docs\README.md` if root links need deeper detail.

## Scope Exceptions

- This phase does not install services or run the app; it documents current setup.

## Do Not Do

- Do not change script behavior.
- Do not add unverified environment variable names or ports.
- Do not describe SQLite as the default local development path.

## Acceptance Checklist

- PostgreSQL command examples match `docker-compose.yml` and `Ensure-DevelopmentPostgres.ps1`.
- Qdrant details match `appsettings.json`.
- Script command examples reference real files.
- Retired MCPs are not listed as active setup.

## Proof Required

- File existence/source review for each documented script and config file.
- Execution report row stating README/runtime docs were compared to source files.

## Browser Validation Logging

- N/A - documentation-only setup changes; no browser-visible behavior.

## Progression Gate

- Closure may proceed only after public setup commands and script descriptions match real repo files.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
