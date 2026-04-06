# Phase10 closure re-check

## Verdict
The phase10 blocker is now closed.

## Direct evidence
### 1. The structure read path is now zero-write
`LoadAsync(...)` in `ProjectStructureAssemblyService` now:

- loads canonical rows,
- normalizes marker payloads in memory,
- loads node bindings,
- marks loaded entities as unchanged,
- loads persisted user links,
- loads projection layout overrides,
- assembles the final snapshot.

There is no stale projection retirement and no save call inside the hot read method.

Relevant code:
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:130-176`

### 2. Cleanup was moved to an explicit repair boundary
`ProjectStructureProjectionMaintenanceService.RepairAsync(...)` is now the explicit delete/save boundary for stale system-managed nodes, stale system-managed links, and orphan layouts.

Relevant code:
- `src/CanDoItAll.Modules.Workbench/ProjectStructureProjectionMaintenanceService.cs:15-68`

### 3. Required behavioral proof exists
The repo now contains the tests that phase10 explicitly required:

- `GetStructureAsync_does_not_delete_stale_system_managed_projection_rows`
- `GetStructureAsync_does_not_delete_stale_projection_layout_rows`
- `GetStructureAsync_does_not_write_when_legacy_marker_and_reference_fallback_is_used`
- `Explicit_projection_repair_removes_stale_system_managed_rows_and_orphan_layouts_idempotently`

Relevant file:
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchProjectionMaintenanceIntegrationTests.cs:15-198`

### 4. Unknown-manifest shared-editor proof exists
The repo now contains an unknown manifest round-trip test that exercises shared provider/resource connector field handling without page-specific UI code.

Relevant file:
- `tests/CanDoItAll.Tests.Integration/UnknownConnectorManifestIntegrationTests.cs:18-99`

## Current conclusion
Bundle10 should now be considered closed. The remaining work is no longer about the old write-on-read blocker. The next real platform risk is the missing execution/runtime plane needed for plugin orchestration.
