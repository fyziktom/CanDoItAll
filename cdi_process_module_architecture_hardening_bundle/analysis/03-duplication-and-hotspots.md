# Duplication and hotspots

## Cross-module duplication hotspots

### Slug generation
Observed in:
- `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs`
- `src/CanDoItAll.Modules.Projects/ProjectModels.cs`

Risk:
- future slug rule changes must be mirrored manually,
- subtle drift can create inconsistent user-facing identifiers.

### JSON file loading
Observed in:
- `src/CanDoItAll.Modules.Processes/ProcessTemplatePackLoader.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs`
- `src/CanDoItAll.Modules.Factory/PromptLibraryPackLoader.cs`
- `src/CanDoItAll.Modules.Prompts/Pages/PromptGalleryPage.razor`

Risk:
- repeated serialization options,
- repeated file-system assumptions,
- inconsistent error behavior.

### Enum parsing helpers
Observed in:
- `src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateCatalogService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateLibraryService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessDevelopmentSeedService.RuntimeSeeds.Complex.cs`

Risk:
- drift in default values or permissiveness,
- repeated parsing edge-case handling.

### Role snapshot summary generation
Observed in:
- `src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateCatalogService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateLibraryService.cs`

Risk:
- the same semantics are recalculated in different ways,
- future display changes require repeated edits.

## Long-file hotspots

See `inventories/02-long-file-hotspots.md` for the full list. The most serious Process-related hotspots are:
- `Components/ProcessWorkspace.razor`
- `ProcessTemplateLibraryService.cs`
- `ProcessTemplateProjectionService.cs`
- `ProcessDefinitionModels.cs`
- `ProcessesService.Persistence.cs`
- `ProcessesService.Publication.cs`
- `ProcessesService.Runtime.cs`

## Hotspot conclusion

Some of these files are naturally substantial, but several are already beyond a reasonable maintainability threshold for the type of work they own. Future changes should reduce responsibility concentration, not merely move code into more partials.
