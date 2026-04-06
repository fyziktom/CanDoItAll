# Closure evidence checklist

Phase10 is not closed without all of the following:

- touched file list,
- explanation of where stale projection cleanup now lives,
- explanation of why the new seam is not reachable from structure reads,
- exact test names and results,
- output from `scripts/gate_check_phase10.py`,
- runtime build/test commands from a real .NET environment,
- proof that the current false-green scenario now fails before the fix and passes after the fix.

## Required test names
Use these exact test names so closure is unambiguous:

1. `GetStructureAsync_does_not_delete_stale_system_managed_projection_rows`
2. `GetStructureAsync_does_not_delete_stale_projection_layout_rows`
3. `GetStructureAsync_does_not_write_when_legacy_marker_and_reference_fallback_is_used`
4. `Explicit_projection_repair_removes_stale_system_managed_rows_and_orphan_layouts_idempotently`
5. `Settings_page_renders_unknown_provider_manifest_fields_through_shared_field_editor`
6. `Resources_page_renders_unknown_resource_manifest_fields_through_shared_field_editor`
7. `Unknown_connector_manifest_fields_round_trip_without_page_specific_code`
