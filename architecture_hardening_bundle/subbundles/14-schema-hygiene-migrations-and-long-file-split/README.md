# Schema hygiene, migrations, and long-file split

## Status

- `Ready`

## Objective

- Finish the core hardening by improving entity/configuration auditability, synchronizing provider snapshots or migrations, and reducing remaining long-file concentration in the touched process core.

## Covered Inputs

- `U003` Long-file and DB concerns.
- `BRQ-014` Schema and model hygiene.
- `F010` Schema/configuration concentration and long-file sprawl.

## Prerequisites

- `13-workspace-and-canvas-decomposition` passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeEntityConfigurations.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesModuleServiceCollectionExtensions.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\Migrations\AppDbContextModelSnapshot.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\Migrations\AppDbContextModelSnapshot.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\DatabaseMigrationIntegrationTests.cs

## Deliverables

- Smaller, easier-to-audit entity/configuration files for the process aggregates touched by this initiative.
- Explicit relationship/delete-behavior hygiene where needed.
- Synchronized SQLite and PostgreSQL snapshots/migrations if the model changed.
- Updated build/migration proof.

## Dependency Impact

- Gate D and final closure depend on model/configuration hygiene being coherent, not only functionally correct.
- This phase is the last structural cleanup before final closure.

## Validation Depth

- `High`

## Implementation Steps

1. Split large model/configuration files into clearer aggregate- or concern-based files where that improves auditability.
2. Review relationship and delete-behavior configuration in the touched process aggregates and make it explicit where needed.
3. Generate or synchronize migrations/snapshots for both providers if the model changed during earlier phases.
4. Run build and migration-related proof and record the result.

## Scope Exceptions

- Do not churn unrelated modules just to reduce line counts.
- Project-module cleanup is only in scope where it directly supports shared-helper extraction or schema auditability touched by this initiative.

## Do Not Do

- Do not hand-edit snapshots carelessly.
- Do not split files purely by arbitrary line count with no ownership improvement.
- Do not leave provider snapshots out of sync.

## Acceptance Checklist

- Touched model/configuration files are easier to audit.
- Relationship/delete behavior is explicit where the initiative changed it.
- SQLite and PostgreSQL snapshots or migrations are coherent.
- The build remains healthy after the split.

## Proof Required

- Build proof for the full solution or impacted projects.
- Migration or snapshot proof for both providers when applicable.
- Execution-report note summarizing the resulting model/configuration layout.

## Browser Validation Logging

- N/A.

## Progression Gate

- The touched model/configuration files are materially easier to audit, both providers are coherent, and the codebase is ready for the final architecture review and closure pass.

## Suggested Agent Prompt

```text
Implement only subbundle 14. Improve schema/configuration auditability, synchronize both provider snapshots or migrations, reduce remaining long-file concentration in the touched process core, and stop before the final architecture review and closure.
```
