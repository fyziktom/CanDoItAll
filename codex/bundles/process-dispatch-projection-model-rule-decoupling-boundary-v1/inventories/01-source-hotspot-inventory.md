# Source Hotspot Inventory

| File | Current role | Risk | Target action |
| --- | --- | --- | --- |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacetImplementations.cs` | Focused facet implementation file, still forwarding to dispatcher static helpers and nested models. | Transitional coupling remains. | Split model conversion and rule forwarding into top-level module-local helpers. |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacets.cs` | Defines facet interfaces and `ProcessArtifactProjectionFacetSet`. | Facets still use nested dispatcher aliases. | Introduce projection-specific model types and adjust facets gradually. |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionContext.cs` | Carries candidate/detail/response/workspace/write coordinators. | Uses nested dispatcher aliases. | Create projection context over projection snapshots/state. |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionOrchestrator.cs` | Owns source-family order. | Must not reorder. | Preserve and test exact order. |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/Process*ArtifactProjectionCoordinator.cs` | Source-specific projection execution. | May still consume dispatcher aliases via facets/context. | Migrate one family at a time to projection models. |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` | Dispatcher projection facade + residual helpers. | Still acts as compatibility adapter. | Keep adapter; reduce helper surface only after snapshot migration. |
