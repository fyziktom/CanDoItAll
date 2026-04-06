# Advisory follow-ups after phase11

These are not the primary bundle11 blockers, but they should stay visible so they do not re-grow during the plugin wave.

## Advisory 1: retire the remaining legacy compatibility fallbacks
- marker metadata fallback in `ProjectStructureAssemblyService.cs:77-82`
- reference metadata fallback in `ProjectNodeBindings.cs:391-395`

## Advisory 2: split large hotspot files
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`

## Advisory 3: unify read models over the new execution plane
Once phase11 exists, the automation workspace should eventually read from durable execution summaries instead of each module inventing one-off status surfaces.

## Advisory 4: keep Quartz and MQTT behind platform seams
Do not let library APIs leak into domain services and plugin handler code.
