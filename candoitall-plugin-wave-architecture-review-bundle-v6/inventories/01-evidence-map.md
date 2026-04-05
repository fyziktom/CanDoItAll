# Evidence Map

| Finding | Theme | Source | Focus |
| --- | --- | --- | --- |
| `PW6-001` | Workbench parallel truth | `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` | `350-388; 398-425; 1767-1833; 1962-2240` |
| `PW6-002` | Overloaded carrier | `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` | `26-59` |
| `PW6-003` | Fragmented kind semantics | `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs; src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs; src/CanDoItAll.Modules.Workbench/ProjectStructureNodeEditor.cs` | `3-44; 45-120 / 225-377; 45-180 / 385-439` |
| `PW6-004` | In-place reclassification | `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs; tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs` | `944-975; 948-1002` |
| `PW6-005` | Enum-driven plugin platform | `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs; ProviderExecution.cs; WorkspaceModuleServiceCollectionExtensions.cs; ResourceModels.cs` | `10-63; 26-48; 1-17; 1-82 / 401-497` |
| `PW6-006` | Metadata foreign ids | `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs` | `223-246; 290-330; 391; 476` |
| `PW6-007` | Dual hierarchy representation | `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs; ProjectStructureInvariantService.cs` | `483-500; 646-655; 2286-2289; 56-74` |
| `PW6-008` | Compensation-based cross-module seam | `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs; src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs; tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs` | `703-747; 1088-1128; 4684-4749; 1262-1425` |
| `PW6-009` | Incomplete role capability model | `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs; ProjectPartyIntegrationContracts.cs` | `4888-4933; 15-31` |
| `PW6-010` | Typed boundary collapses to raw string / persisted row lookup | `src/CanDoItAll.Modules.Workbench/ProjectNodeScopeBridge.cs; CrmHrBusinessModels.cs; CrmHrServices.cs` | `8-68; 413-428; 4994-5002` |
| `PW6-011` | Hotspots | `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs; src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs` | `3227 LOC; 5001 LOC` |
| `PW6-012` | Missing architecture guardrail tests | `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs; architecture/adrs/ADR-0004-workbench-node-extension-guardrails.md` | `1262-1425; full ADR` |
