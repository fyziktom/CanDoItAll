# Advisory hotspots

These remain visible maintenance debt, but they are not the primary reason for phase11.

- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs` — approximately 4969 lines
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` — approximately 1147 lines

Recommendation:
- split orchestration/services from DTO/view-models,
- isolate plugin/runtime additions from these hotspots,
- do not use the plugin wave to grow either hotspot further.
