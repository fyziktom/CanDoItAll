
# Scope inventory

## Current snapshot inventory

| Metric | Current | Baseline | Delta |
| --- | --- | --- | --- |
| Projects (.csproj) | 42 | 41 | +1 |
| C# files | 599 | 539 | +60 |
| Razor files | 337 | 304 | +33 |
| Suspicious 'manager' markers | 42 | 23 | +19 |
| Suspicious 'god_service' markers | 35 | 35 | +0 |

## High-value files inspected

- `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureDependencyAnalysis.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.RichDefinitions.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeMutations.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs`
- `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs`
- `src/CanDoItAll.Modules.Validation/ValidationModels.cs`
- `src/CanDoItAll.Modules.TestLab/TestLabModels.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectPartyAssignmentIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Components/CrossModuleResponsiblePartyPageTests.cs`
- `tests/CanDoItAll.Tests.Integration/CrmHrCrossModuleIntegrationTests.cs`

## Hotspot file sizes

| File | Approx. lines | Why it matters |
| --- | --- | --- |
| ProjectWorkbenchModels.cs | 2931 | Core sync/read/write/projection hotspot |
| ProjectWorkbenchMetadata.cs | 869 | JSON family semantics and marker duplication |
| ProjectStructurePage.PartyIntegration.cs | 505 | UI writes both metadata and canonical-ish assignments |
| CrmHrServices.cs | 4704 | Cross-module service hotspot; assignment save lacks node integrity checks |
| ProjectStructureCanvasCatalog.RichDefinitions.cs | 529 | UI-owned subtype semantics |
