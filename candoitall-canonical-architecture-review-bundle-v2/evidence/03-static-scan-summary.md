
# Static scan summary

## Strongest evidence points

- `ProjectObjectRecord` remains overloaded (`ProjectWorkbenchModels.cs:26-60`)
- structure/calendar reads still sync and read persisted workbench rows (`ProjectWorkbenchModels.cs:348-376`, `396-414`)
- `SyncGraphAsync` still materializes a persisted parallel graph (`ProjectWorkbenchModels.cs:1666-1943`)
- note/block mutation support is still limited (`ProjectWorkbenchModels.cs:2352-2358`, `ProjectStructurePage.NodeMutations.cs:107-125`)
- dependency analysis still mixes ancestors into prerequisites (`ProjectStructureDependencyAnalysis.cs:81-125`)
- CRM/HR node-scoped party flows write metadata and assignment rows (`ProjectStructurePage.PartyIntegration.cs:240-307`, `325-352`, `360-406`)
- assignment save validates project and party existence but not node integrity (`CrmHrServices.cs:4421-4500`)
- module-local responsible-party fields still exist in Resources / Validation / TestLab
- subtype semantics still live partly in UI catalog (`ProjectStructureCanvasCatalog.RichDefinitions.cs:132-144`)

## Drift acceleration summary

Compared with the earlier snapshot:

- repo size increased materially
- the cross-module actor/responsibility surface widened
- duplicated truth increased in the most important domain area
- the target architecture now needs a stronger explicit answer for node identity, transitions, and scoped assignments
