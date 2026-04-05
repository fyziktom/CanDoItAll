# Evidence

## Code evidence

- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs:250-257, 293-305, 330-351, 370-405 save both metadata and ProjectPartyAssignment rows
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:296-299 and 331-334 store LinkedPartyId / AssigneePartyId inside node metadata
- src/CanDoItAll.Modules.Resources/ResourceModels.cs:84-92 stores OwnerPartyId / MaintainerPartyId
- src/CanDoItAll.Modules.Validation/ValidationModels.cs:50-58 stores ResponsiblePartyId
- src/CanDoItAll.Modules.TestLab/TestLabModels.cs:18-25 stores ResponsiblePartyId

## Root cause

CRM/HR integration added reusable party semantics without first establishing one canonical actor-assignment owner across node-scoped and module-scoped responsibility.

## Architectural interpretation

This finding exists because the current code lets one concern occupy too many roles at once.
