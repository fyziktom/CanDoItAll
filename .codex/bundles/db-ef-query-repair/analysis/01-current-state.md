# Current State

## Database Architecture

- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\AppDbContext.cs` is the shared EF Core context.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\SwitchableAppDbContextFactory.cs` selects SQLite, PostgreSQL, or in-memory profiles.
- Module entity configurations are registered through `AppDbContextModelRegistry`; migrations exist for SQLite and PostgreSQL.

## EF Query Scan Findings

- Several read paths already use `AsNoTracking()`, especially process runtime observation queries.
- No broad lazy-loading or `Include()`-inside-loop pattern was found in the first pass.
- Concrete trouble found: multiple services call `.ToListAsync()` and only then order, filter by date, take the first page, or pick the newest record. This loads avoidable rows and creates unnecessary tracked entities in read-only paths.
- Concrete trouble found: several read-only service methods omit `AsNoTracking()` even when their results are immediately mapped to DTOs or returned as read models.
- Validation finding during execution: SQLite cannot translate `DateTimeOffset` ordering. The repair uses explicit SQLite branches for affected `DateTimeOffset` order/filter paths and keeps server-side ordering for non-SQLite providers.

## High-Confidence Repair Targets

- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\BackgroundJobs\BackgroundJobs.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Activity\ActivityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\SchedulerPlannerService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAnalyticsService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureLeaseService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Persistence\PersistentWorkflowStores.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Models\WorkspaceModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Persistence\StorageCatalogService.cs`
