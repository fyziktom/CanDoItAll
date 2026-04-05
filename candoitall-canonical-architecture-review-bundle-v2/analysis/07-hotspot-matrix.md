# Hotspot matrix

| Hotspot | Symptoms | Findings | Impact |
| --- | --- | --- | --- |
| ProjectWorkbenchModels.cs | 2931 lines; sync, read, write, projection, media, transfer, metadata, links | ACR-001, ACR-002, ACR-004, ACR-005, ACR-006, ACR-008, ACR-009, ACR-010, ACR-014 | Core hotspot |
| ProjectWorkbenchMetadata.cs | 869 lines; JSON family model, marker normalization, partial validation | ACR-003, ACR-008, ACR-012, ACR-014 | Semantic hotspot |
| ProjectStructurePage.PartyIntegration.cs | 505 lines; UI writes metadata and assignment rows | ACR-012, ACR-013, ACR-015 | Boundary hotspot |
| CrmHrServices.cs | 4704 lines overall; assignment save only checks project/party existence | ACR-013, ACR-015 | Cross-module integrity hotspot |
| ProjectStructureCanvasCatalog.RichDefinitions.cs | UI authoring catalog includes participant/work item semantics | ACR-003, ACR-014 | UI-owned semantics hotspot |
| Resource/Validation/TestLab responsibility fields | Module-local owner/responsible IDs fragment responsibility truth | ACR-012, ACR-015 | Cross-module drift hotspot |
