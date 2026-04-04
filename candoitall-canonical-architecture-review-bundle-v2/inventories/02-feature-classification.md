# Feature classification

| File / area | Classification | Notes |
| --- | --- | --- |
| src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs | Domain-core contract | Owns top-level object/link enumerations but not the full semantic policy. |
| src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs | Mixed-role application + persistence hotspot | Currently mixes canonical-like data, persistence schema, mutation workflows, and projection read assembly. |
| src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs | Typed facet payload helper / validation helper | Currently acts as JSON family serializer and partial validator; should shrink once typed facets exist. |
| src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs | UI workflow + boundary hotspot | Writes both node metadata and CRM/HR assignment rows, creating duplicated truth. |
| src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs | Cross-module contract | Good seam, but NodeKey scope is too soft and unconstrained. |
| src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs | Canonical identity / assignment persistence | Party directory is strong; node-scoped assignment storage needs stronger scope semantics. |
| src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs | Application service hotspot | Validates project + party existence but not node existence/project/kind compatibility. |
| src/CanDoItAll.Modules.Resources/ResourceModels.cs | Module-native canonical aggregate | Owns resource-specific responsibility fields today. |
| src/CanDoItAll.Modules.Validation/ValidationModels.cs | Module-native canonical aggregate | Owns validation-run responsibility today. |
| src/CanDoItAll.Modules.TestLab/TestLabModels.cs | Module-native canonical aggregate | Owns test-plan responsibility today. |
| src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.RichDefinitions.cs | UI projection / authoring catalog | Currently owns too much semantic meaning for participant and work-item subtypes. |
| src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeMutations.cs | UI workflow | Only supports note→block or block→block; does not cover the intended note→task/decision lifecycle. |
