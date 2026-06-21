# Scope Inventory

The pre-removal workbook is the authoritative reference map:

- `bundle://inventories/unused-module-reference-map.xlsx`
- `bundle://inventories/unused-module-reference-map-preview.png`
- `bundle://inventories/unused-module-reference-map.xlsx.inspect.ndjson`

## Inventory Summary

| Area | Removal decision |
| --- | --- |
| Solution/project references | Remove references to Validation, Activity, and Automation module projects. |
| Composition/runtime registration | Remove old module service registration and module assembly discovery. |
| Web navigation/layout/home | Remove routes and cards for `/validation`, `/activity`, and `/automation`; prefer scheduler/workflow/process surfaces where relevant. |
| Workbench project structure | Remove ValidationRun projection, right-click creation action, scope bridge resolution, and direct command routing to `/validation`. |
| SchedulerPlanner | Replace direct Automation module contracts with SchedulerPlanner-owned scheduling/dispatch concepts. |
| Tests | Delete module-specific Automation/Validation tests; update or remove Activity assertions that exist only to prove the removed module. |
| Historical migrations | Keep unless compile/runtime validation requires change; they are not active module registration. |
