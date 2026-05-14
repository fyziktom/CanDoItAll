# 01 EF Query Hotspots And Repair

## Status

- `Completed`

## Objective

Repair concrete EF Core query-shape problems found in current database-backed services while preserving existing behavior and architecture.

## Covered Inputs

- N001
- N002
- N003
- N004

## Prerequisites

- Prepared bundle exists at `C:\repositories\CanDoItAll\.codex\bundles\db-ef-query-repair`.
- Source scan identifies concrete `.ToListAsync()` before order/filter/take or safe read-only no-tracking gaps.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\BackgroundJobs\BackgroundJobs.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Persistence\StorageCatalogService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Activity\ActivityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Persistence\PersistentWorkflowStores.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\SchedulerPlannerService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAnalyticsService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureLeaseService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Models\WorkspaceModels.cs`

## Deliverables

- Server-side order, date filtering, and take are applied before materialization where safe.
- Read-only queries use `AsNoTracking()` where safe.
- Bundle execution report records exact proof.

## Dependency Impact

- This is the only implementation subbundle. Weak proof here invalidates final closure because the request is specifically about DB correctness and repair.

## Validation Depth

- Critical foundation.
- Targeted integration/unit tests for touched services plus build proof.

## Implementation Steps

1. Patch read paths that materialize before order/filter/take.
2. Add no-tracking to safe read-only query paths.
3. Avoid changing tracked write flows.
4. Run targeted tests for scheduler, workspace/provider, storage catalog, project structure agent API, workflow/process integration where feasible.
5. Run build proof.
6. Update execution report and closure rows.

## Scope Exceptions

- No database schema or index changes.
- No provider-specific SQL tuning.
- No global no-tracking default.

## Do Not Do

- Do not change `AppDbContext` lifetime, profile switching, migrations, or service contracts.
- Do not rewrite services into repositories.
- Do not change UI components for this bundle.

## Acceptance Checklist

- [x] High-confidence EF query-shape problems are repaired.
- [x] Read-only no-tracking gaps are repaired where safe.
- [x] Targeted tests/build pass or blockers are documented.
- [x] Execution report and raw-note closure are updated.

## Proof Required

- `python codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py .codex\bundles\db-ef-query-repair --profile feedback --stage prepared`
- Targeted `dotnet test` commands for touched test coverage.
- `dotnet build CanDoItAll.slnx`
- `python codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py .codex\bundles\db-ef-query-repair --profile feedback --stage completed`

## Browser Validation Logging

- N/A. This subbundle changes non-UI EF query shape only.

## Progression Gate

- Passed. Targeted tests/build passed and the execution report contains non-pending gate and raw-note rows.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Repair concrete EF query-shape issues in the listed files. Push ordering/filtering/take into SQL where safe, add AsNoTracking to read-only queries, preserve tracked writes, run targeted tests/build, update the execution report, and stop if provider translation breaks.
```
