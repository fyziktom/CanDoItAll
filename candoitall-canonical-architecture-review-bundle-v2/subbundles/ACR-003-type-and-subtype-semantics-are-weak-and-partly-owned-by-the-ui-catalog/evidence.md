# Evidence

## Code evidence

- src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs:3-44 broad enums exist but no canonical kind registry
- src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs:6-28 and ProjectStructureCanvasCatalog.RichDefinitions.cs:132-144 UI create definitions encode semantic defaults and field sets
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:625-660 metadata validation only checks broad family/type alignment
- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs:466-474 participant role is inferred from subtype strings

## Root cause

Type semantics grew through UI creation and metadata family needs instead of a central domain registry.

## Architectural interpretation

This finding exists because the current code lets one concern occupy too many roles at once.
