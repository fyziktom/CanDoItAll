# Advisory hotspots

These are not the main reason for the NO-GO, but they still matter:

- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs` remains very large.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` remains very large.
- legacy compatibility fallback from metadata still exists for markers and references.

These should not block phase12 closure, but they should not expand further.
