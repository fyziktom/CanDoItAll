# 02-process-only-development-db-reset-and-template-reload

## Status

- `Completed`

## Objective

- Clear process-owned data from the development PostgreSQL database and reload current process templates while preserving non-process settings and project data.

## Success Criteria

- SQL target list contains only tables whose names start with `Processes_`.
- Process tables are empty immediately after reset.
- Representative non-process table counts are unchanged.
- Current process templates are imported and published after reset.
- Project structure files and workspace managed files are not deleted.

## Covered Inputs

- N002, N003
- R005, R006, R007

## Prerequisites

- SB01 completed and passed closure gate.
- Development database connection string has been confirmed.
- SQL target list is reviewed before destructive execution.

## Exact Source References

- `repo://src/CanDoItAll.Web/appsettings.Development.json`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessCatalogWarmupService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Development/ProcessDevelopmentSeedService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessDefinitionEntityConfigurations.cs`
- `repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessRuntimeEntityConfigurations.cs`
- `repo://Templates/Processes`

## Deliverables

- Process-only truncate/reload transcript under `proof/SB02/transcripts`.
- Before/after table count proof.
- Reloaded process definitions from the current template pack.
- Preservation proof for agents/plugins/memory/projects/project structure.

## Dependency Impact

- This is the final operational state requested by the user. Weak proof would risk silent data loss or stale templates.

## Validation Depth

- Process-critical destructive-operation closure.
- Requires command transcripts, before/after counts, anti-stub audit, and raw-note literal closure.

## Implementation Steps

1. Capture before counts for all `Processes_%` tables and representative non-process tables.
2. Generate the process-table truncate list from database metadata and verify all names start with `Processes_`.
3. Execute the process-only truncate with `RESTART IDENTITY CASCADE`.
4. Confirm process tables are empty.
5. Reload current default process templates through application services/API.
6. Confirm reloaded process definitions are present and published.
7. Confirm representative non-process counts match before counts.
8. Update proof, execution report, and raw-note closure.

## Scope Exceptions

- None planned. If PostgreSQL is unavailable, mark SB02 blocked rather than simulating the database operation.

## Do Not Do

- Do not drop `candoitall_development`.
- Do not run broad `TRUNCATE ... CASCADE` against non-process tables.
- Do not delete workspace files, project structure files, agent settings, plugin settings, or cognitive memory rows.
- Do not reload old process templates from stale generated artifacts.

## Acceptance Checklist

- [x] Before counts captured.
- [x] SQL truncate target list contains only `Processes_` tables.
- [x] Process tables empty after reset.
- [x] Current default process templates imported/published after reset.
- [x] Representative non-process counts unchanged.
- [x] Final source/build/test validation remains green.

## Proof Required

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB02/semantic-invariants.md`
- `proof/SB02/transcripts/db-before-counts.txt`
- `proof/SB02/transcripts/db-process-table-targets.txt`
- `proof/SB02/transcripts/db-reset.txt`
- `proof/SB02/transcripts/db-after-counts.txt`
- `proof/SB02/transcripts/template-reload.txt`
- `proof/SB02/transcripts/non-process-preservation.txt`

## Browser Validation Logging

- N/A; this subbundle requires host/database proof rather than browser-visible proof.

## Progression Gate

- The bundle may close only when DB transcripts prove process-only deletion, template reload, and non-process preservation.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
