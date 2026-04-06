# Acceptance

- Remove all direct and transitive persistence mutations from `ProjectStructureAssemblyService.LoadAsync(...)`.
- Restore `ProjectStructureProjectionMaintenanceService` (or equivalent explicit repair boundary) with `RepairAsync(...)`.
- Register the repair service in DI.
- Keep cleanup of stale projection rows/layouts outside the hot read path.
- Preserve the previous phase10 compatibility behavior without writing on read.
