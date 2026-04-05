# Evidence map

## P7-001 - Workbench still persists synchronized cross-module projection nodes and links as a second truth
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:350-388
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:398-425
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1962-2239

## P7-002 - The universal node carrier is still overloaded instead of being a stable carrier plus typed facets and bindings
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:26-59
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:143-177
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:165-244

## P7-003 - Node-kind semantics and node-scoped capability rules are still fragmented and hardcoded
- src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs:3-44
- src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs:45-120
- src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.RichDefinitions.cs:1-529
- src/CanDoItAll.Modules.Workbench/ProjectStructureCreateRequestComposer.cs:1-197
- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs:217-243
- src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs:4888-4933

## P7-004 - Node reclassification still mutates in place without transition history or facet supersession
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:944-975
- tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:949-1002

## P7-005 - Hierarchy is still dual-represented through ParentNodeKey and generic link rows
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:447-499
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:626-650
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1059-1068
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:2286-2289
- src/CanDoItAll.Modules.Workbench/ProjectStructureInvariantService.cs:56-74

## P7-006 - Workbench metadata still carries foreign identifiers and keeps dual marker truth
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:219-247
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:287-331
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:388-477
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:545-585

## P7-007 - Provider/resource/connector architecture is still a closed enum-and-switch seam
- src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs:10-63
- src/CanDoItAll.Modules.Workspace/ProviderExecution.cs:26-48
- src/CanDoItAll.Modules.Resources/ResourceModels.cs:10-81

## P7-008 - Cross-module mutation boundaries are still compensation-based and not ready for outbound connector side effects
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:662-748
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1038-1133
- tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:1262-1425

## P7-009 - Workbench and CRM/HR service hotspots remain too large and multi-responsibility
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs (3227 lines)
- src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs (5001 lines)

## P7-010 - There is still no hard architecture closure mechanism preventing the same blockers from being reintroduced
- Current static review still matches the unresolved issues from prior bundles
- No dedicated ArchitectureGuardrail test suite was found in tests/
- No repo-level hard-gate script enforcing closure of repeated blockers was found
